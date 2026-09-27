using System;
using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GuildMaster.Tests
{
    /// <summary>Реальные ассеты данных и их копии в памяти для порчи. Копии уничтожаются в <see cref="Dispose"/>.</summary>
    internal sealed class GameData : IDisposable
    {
        public const string ConfigPath = "Assets/_Project/Data/GameConfig.asset";

        private readonly List<Object> copies = new List<Object>();

        public GameData()
        {
            Config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(Config, $"Нет {ConfigPath}");
        }

        public GameConfig Config { get; }

        /// <summary>Копия ассета в памяти: портить можно, файл на диске не меняется.</summary>
        public T Copy<T>(T original) where T : Object
        {
            T copy = Object.Instantiate(original);
            copy.name = original.name;
            copies.Add(copy);
            return copy;
        }

        public void Dispose()
        {
            foreach (Object copy in copies) Object.DestroyImmediate(copy);
        }

        /// <summary>Правка сериализованного поля так же, как в инспекторе.</summary>
        public static void Edit(Object target, string path, Action<SerializedProperty> change)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.IsNotNull(property, $"{target.name}: нет поля {path}");
            change(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public sealed class DataValidatorTests
    {
        private GameData data;

        [SetUp]
        public void SetUp() => data = new GameData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void RealConfig_HasNoErrors()
        {
            DataValidationReport report = DataValidator.Validate(data.Config);

            Assert.IsFalse(report.HasErrors, report.ToString());
            CollectionAssert.IsEmpty(report.Warnings.Where(w => w.Message.Contains("скобк")).Select(w => w.ToString()), "предупреждения о скобках рода");
            TestContext.WriteLine(report.ToString());
        }

        [Test]
        public void DuplicateId_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            ArchetypeDefinition twin = data.Copy(config.Archetypes[0]);
            GameData.Edit(config, "archetypes", list =>
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = twin;
            });

            DataValidationReport report = DataValidator.Validate(config);

            AssertError(report, $"дубль id «{twin.Id}»");
        }

        [Test]
        public void EmptyReference_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            GameData.Edit(config, "statCatalog", p => p.objectReferenceValue = null);

            DataValidationReport report = DataValidator.Validate(config);

            Assert.IsTrue(report.Errors.Any(i => i.Asset == config && i.Path == "statCatalog" && i.Message == "пустая ссылка"), report.ToString());
        }

        [Test]
        public void EmptyReferenceInList_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            GameData.Edit(config, "dilemmas.Array.data[0]", p => p.objectReferenceValue = null);

            DataValidationReport report = DataValidator.Validate(config);

            Assert.IsTrue(report.Errors.Any(i => i.Path == "dilemmas[0]" && i.Message == "пустая ссылка"), report.ToString());
        }

        [Test]
        public void UnknownPlaceholder_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            FeedTemplateSet feed = data.Copy(config.FeedTemplates);
            GameData.Edit(feed, "templates.Array.data[0].variants.Array.data[0]", p => p.stringValue = "{чужак} вышел за ворота");
            GameData.Edit(config, "feedTemplates", p => p.objectReferenceValue = feed);

            DataValidationReport report = DataValidator.Validate(config);

            AssertError(report, "неизвестная подстановка {чужак}");
        }

        [Test]
        public void UnknownCase_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            FeedTemplateSet feed = data.Copy(config.FeedTemplates);
            GameData.Edit(feed, "templates.Array.data[0].variants.Array.data[0]", p => p.stringValue = "{имя:х} вышел за ворота");
            GameData.Edit(config, "feedTemplates", p => p.objectReferenceValue = feed);

            DataValidationReport report = DataValidator.Validate(config);

            AssertError(report, "неизвестный падеж в {имя:х}");
        }

        [TestCase("{постройка} готов[|а|о]", "у человека — ровно две формы")]
        [TestCase("{постройка} готов[|а]@постройка", "у названия — три или четыре формы")]
        [TestCase("Всё готово[|а|о]@постройка", "метки {постройка} в строке нет")]
        [TestCase("{число} готов[|а|о]@число", "после @ нужна метка с родом")]
        public void BadGenderBracket_IsError(string variant, string error)
        {
            GameConfig config = data.Copy(data.Config);
            FeedTemplateSet feed = data.Copy(config.FeedTemplates);
            GameData.Edit(feed, "templates.Array.data[0].variants.Array.data[0]", p => p.stringValue = variant);
            GameData.Edit(config, "feedTemplates", p => p.objectReferenceValue = feed);

            DataValidationReport report = DataValidator.Validate(config);

            AssertError(report, error);
        }

        [Test]
        public void NounWithoutGender_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            BuildingDefinition dormitory = data.Copy(config.Buildings.First(b => b.Id == "Dormitory"));
            GameData.Edit(dormitory, "nameForms.gender", p => p.enumValueIndex = (int)GrammaticalGender.Unspecified);
            GameData.Edit(config, $"buildings.Array.data[{config.Buildings.ToList().FindIndex(b => b.Id == "Dormitory")}]",
                p => p.objectReferenceValue = dormitory);

            DataValidationReport report = DataValidator.Validate(config);

            AssertError(report, "«Общежитие»: не задан род");
        }

        [Test]
        public void MissingFeedKey_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            QuestTypeDefinition questType = data.Copy(config.QuestTypes[0]);
            GameData.Edit(questType, "roundSuccessFeedKey", p => p.stringValue = "quest.no.such.key");
            GameData.Edit(config, "questTypes.Array.data[0]", p => p.objectReferenceValue = questType);

            DataValidationReport report = DataValidator.Validate(config);

            AssertError(report, "ключа ленты «quest.no.such.key» нет");
        }

        [Test]
        public void ValueOutOfRange_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            BalanceSettings balance = data.Copy(config.Balance);
            GameData.Edit(balance, "orders.farChance", p => p.floatValue = 1.5f);
            GameData.Edit(balance, "orders.boardDays.min", p => p.intValue = 9);
            GameData.Edit(config, "balance", p => p.objectReferenceValue = balance);

            DataValidationReport report = DataValidator.Validate(config);

            Assert.IsTrue(report.Errors.Any(i => i.Path == "orders.farChance"), report.ToString());
            Assert.IsTrue(report.Errors.Any(i => i.Path == "orders.boardDays" && i.Message.Contains("min больше max")), report.ToString());
        }

        [Test]
        public void ChanceSumNotOne_IsError()
        {
            GameConfig config = data.Copy(data.Config);
            RandomEventDefinition ambush = data.Copy(config.RandomEvents[0]);
            GameData.Edit(ambush, "failureOutcomes.Array.data[0].chance", p => p.floatValue = 0.9f);
            GameData.Edit(config, "randomEvents.Array.data[0]", p => p.objectReferenceValue = ambush);

            DataValidationReport report = DataValidator.Validate(config);

            AssertError(report, "шансы исходов в сумме");
        }

        private static void AssertError(DataValidationReport report, string fragment) =>
            Assert.IsTrue(report.Errors.Any(i => i.Message.Contains(fragment)), $"Нет ошибки «{fragment}».\n{report}");
    }

    public sealed class DataRegistryTests
    {
        private GameData data;
        private DataRegistry registry;

        [SetUp]
        public void SetUp()
        {
            data = new GameData();
            registry = DataRegistry.FromConfig(data.Config);
        }

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void FindsDefinitionsById()
        {
            Assert.AreEqual("Щит", registry.Get<ArchetypeDefinition>("Shield").DisplayName);
            Assert.AreEqual(45f, registry.Get<ArchetypeDefinition>("Novice").GenerationWeight);
            Assert.AreEqual(TraitCategory.Acquired, registry.Get<SpecialTraitDefinition>("Maimed").Category);
            Assert.AreEqual(6, registry.Get<QuestTypeDefinition>("Delivery").RoundHours);
            Assert.AreEqual(800, registry.Get<BuildingDefinition>("Dormitory").Cost);
            Assert.AreEqual(GrammaticalGender.Neuter, registry.Get<BuildingDefinition>("Dormitory").NameForms.Gender);
            Assert.AreEqual(14, registry.Get<DecreeDefinition>("Prohibition").LawNumber);
            Assert.AreEqual(DilemmaTrigger.TavernFeast, registry.Get<DilemmaDefinition>("TavernFeast").Trigger);
            Assert.AreSame(registry.Get<BuildingDefinition>("Infirmary"), registry.Get<StaffRoleDefinition>("Healer").RequiredBuilding);
        }

        [Test]
        public void UnknownId_NotFound()
        {
            Assert.IsFalse(registry.TryGet("NoSuchArchetype", out ArchetypeDefinition _));
            Assert.Throws<KeyNotFoundException>(() => registry.Get<ArchetypeDefinition>("NoSuchArchetype"));
            Assert.IsFalse(registry.TryGet("Shield", out QuestTypeDefinition _), "id ищется среди определений своего вида");
        }

        [Test]
        public void ListsAxesTemplatesAndCatalogs()
        {
            Assert.AreEqual(Vocabulary.AxisCount, registry.All<AxisDefinition>().Count);
            Assert.AreEqual(5, registry.All<QuestTypeDefinition>().Count);
            Assert.AreEqual("Трус", registry.Axis(AxisId.Risk).NegativePole.Name);
            Assert.AreEqual(2, registry.FeedTemplates("quest.departed").Count);
            Assert.IsEmpty(registry.FeedTemplates("no.such.key"));
            Assert.AreEqual(13, registry.Stats.RadarOrder.Count);
            Assert.IsNotEmpty(registry.Names.MaleNames);
            Assert.IsNotEmpty(registry.OrderTexts.Hints);
        }

        [Test]
        public void DuplicateId_Throws()
        {
            GameConfig config = data.Copy(data.Config);
            BuildingDefinition twin = data.Copy(config.Buildings[0]);
            GameData.Edit(config, "buildings", list =>
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = twin;
            });

            Assert.Throws<ArgumentException>(() => DataRegistry.FromConfig(config));
        }
    }

    public sealed class BalanceRuntimeTests
    {
        [Test]
        public void RealAsset_HoldsNumbersFromDocs()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceSettings>("Assets/_Project/Data/BalanceSettings.asset");
            var registry = new DataRegistry(balance);

            Assert.AreEqual(2000, registry.Balance.Guild.StartMoney);
            Assert.AreEqual(0.2f, registry.Balance.Economy.DefaultCommission);
            Assert.AreEqual(90, registry.Balance.Ranks.For(GuildRank.D).PointsToNext);
            Assert.AreEqual(2f, registry.Balance.Decisions.ArrowMultiplier(Arrow.StrongUp));
            Assert.AreEqual(AdventurerLevel.Medium, registry.Balance.Adventurers.StartMaxLevel);
        }

        [Test]
        public void ChangedNumber_IsReadWithoutRecompile()
        {
            using (var test = new TestData())
            {
                Assert.AreEqual(250, test.Registry.Balance.Orders.ImportantRewardThreshold);

                GameData.Edit(test.Balance, "orders.importantRewardThreshold", p => p.intValue = 400);

                Assert.AreEqual(400, test.Registry.Balance.Orders.ImportantRewardThreshold);
            }
        }

        [Test]
        public void ChangedNumber_ChangesSimulation()
        {
            using (var test = new TestData())
            {
                GameData.Edit(test.Balance, "time.daysPerMonth", p => p.intValue = 10);
                Simulation simulation = Simulation.CreateDefault(test.Registry, 1u);

                long tenDays = simulation.Calendar.DaysToHours(10);
                for (long i = 0; i < tenDays; i++) simulation.Tick();

                Assert.AreEqual(2, simulation.World.Time.Month, "Месяц из 10 дней: через 10 дней — второй месяц");
                Assert.AreEqual(1, simulation.World.Time.Day);
            }
        }
    }
}
