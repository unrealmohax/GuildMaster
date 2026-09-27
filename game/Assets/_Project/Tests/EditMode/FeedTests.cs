using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Значения меток из словаря — для проверки движка подстановки без событий.</summary>
    internal sealed class DictionarySource : ITextSource
    {
        private readonly Dictionary<string, TextValue> values = new Dictionary<string, TextValue>();

        public DictionarySource Set(string label, TextValue value)
        {
            values[label] = value;
            return this;
        }

        public bool TryGet(string label, out TextValue value) => values.TryGetValue(label, out value);
    }

    /// <summary>Шаблоны ленты в памяти: поля ставятся отражением, как у генератора данных.</summary>
    internal static class FeedTestTemplates
    {
        public static FeedTemplate Make(string key, FeedImportance importance, FeedCondition[] conditions, params string[] variants) =>
            Fill(new FeedTemplate(), ("key", key), ("feed", FeedKind.Guild), ("importance", importance),
                ("conditions", conditions.ToList()), ("variants", variants.ToList()));

        public static FeedCondition Condition(FeedConditionKind kind, params (string field, object value)[] fields) =>
            Fill(new FeedCondition(), new[] { ("kind", (object)kind) }.Concat(fields).ToArray());

        private static T Fill<T>(T target, params (string field, object value)[] fields)
        {
            foreach ((string field, object value) in fields)
            {
                typeof(T).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
            }
            return target;
        }
    }

    public sealed class TextRendererTests
    {
        private static readonly NounForms Yan = new NounForms("Ян", "Яна", "Яну", "Яна", "Яном", "Яне", GrammaticalGender.Masculine);
        private static readonly NounForms Mara = new NounForms("Мара", "Мары", "Маре", "Мару", "Марой", "Маре", GrammaticalGender.Feminine);

        private static TextValue Man => TextValue.Person("Ян", Yan, Gender.Male);
        private static TextValue Woman => TextValue.Person("Мара", Mara, Gender.Female);

        private static string Render(string template, DictionarySource source, List<string> errors = null)
        {
            errors = errors ?? new List<string>();
            string text = TextRenderer.Render(template, source, errors);
            return text;
        }

        [Test]
        public void Cases_TakeFormsFromNames()
        {
            var source = new DictionarySource().Set("имя", Man).Set("напарник", Woman);

            Assert.AreEqual("У Яна рассечена бровь", Render("У {имя:р} рассечена бровь", source));
            Assert.AreEqual("Ян всё время держался рядом с Марой", Render("{имя} всё время держал[ся|ась] рядом с {напарник:т}", source));
            Assert.AreEqual("Ян закрыл собой Мару", Render("{имя} закрыл[|а] собой {напарник:в}", source));
            Assert.AreEqual("На обратном пути Яну стало хуже", Render("На обратном пути {имя:д} стало хуже", source));
            Assert.AreEqual("Думали о Яне", Render("Думали о {имя:п}", source));
        }

        [Test]
        public void Gender_NearestPreviousPersonInSentence_ElseNext()
        {
            var source = new DictionarySource().Set("имя", Woman).Set("напарник", Man).Set("решающий", Woman);

            Assert.AreEqual("Мара рвалась вперёд, Ян — назад. Решила Мара",
                Render("{имя} рвал[ся|ась] вперёд, {напарник} — назад. Решил[|а] {решающий}", source));
            Assert.AreEqual("Мара и Ян: он спорил", Render("{имя} и {напарник}: [он|она] спорил", source));
        }

        [Test]
        public void Gender_ExplicitOwner()
        {
            var source = new DictionarySource().Set("имя", Woman).Set("напарник", Man);

            Assert.AreEqual("Мара тяжело ранена. Ян тащит её на себе",
                Render("{имя} тяжело ранен[|а]. {напарник} тащит [его|её]@имя на себе", source));
            Assert.AreEqual("После гибели Яна Мара другая", Render("После гибели {напарник:р} {имя} друг[ой|ая]@имя", source));
        }

        [TestCase(GrammaticalGender.Masculine, "Лазарет готов")]
        [TestCase(GrammaticalGender.Feminine, "Таверна готова")]
        [TestCase(GrammaticalGender.Neuter, "Общежитие готово")]
        [TestCase(GrammaticalGender.Plural, "Склады готовы")]
        public void Gender_OfNames_FourForms(GrammaticalGender gender, string expected)
        {
            string name = expected.Substring(0, expected.IndexOf(' '));
            var source = new DictionarySource().Set("постройка", TextValue.Noun(new NounForms(name, gender)));

            Assert.AreEqual(expected, Render("{постройка} готов[|а|о|ы]@постройка", source));
        }

        [Test]
        public void Gender_OfNames_ThreeForms_PluralTakesMasculine()
        {
            var source = new DictionarySource().Set("враг", TextValue.Noun(new NounForms("волки", GrammaticalGender.Plural)));

            Assert.AreEqual("Волки напал на привале", Render("{враг} напал[|а|о]@враг на привале", source));
        }

        [Test]
        public void MissingLabel_IsError_AndStaysAsIs()
        {
            var errors = new List<string>();
            string text = Render("{имя} взял[|а] охоту у {место:р}", new DictionarySource().Set("имя", Woman), errors);

            Assert.AreEqual("Мара взяла охоту у {место:р}", text);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("{место:р}", errors[0]);
        }

        [Test]
        public void BracketWithoutPerson_IsError_AndTakesMasculine()
        {
            var errors = new List<string>();
            string text = Render("{имя} ушла. Сказал[|а], что так быстрее", new DictionarySource().Set("имя", Woman), errors);

            Assert.AreEqual("Мара ушла. Сказал, что так быстрее", text);
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void ValueAtSentenceStart_IsCapitalized_NumbersAsIs()
        {
            var source = new DictionarySource()
                .Set("враг", TextValue.Noun(new NounForms("медведь-шатун", GrammaticalGender.Masculine)))
                .Set("число", TextValue.Number(3))
                .Set("всего", TextValue.Number(5));

            Assert.AreEqual("Медведь-шатун напал на привале", Render("{враг} напал[|а|о|и]@враг на привале", source));
            Assert.AreEqual("Вернулись 3 из 5. Задание провалено", Render("Вернулись {число} из {всего}. Задание провалено", source));
        }

        [Test]
        public void MissingCaseForm_TakesNominative()
        {
            var source = new DictionarySource().Set("место", TextValue.Noun(new NounForms("старая мельница", GrammaticalGender.Feminine)));

            Assert.AreEqual("У старая мельница ждали", Render("У {место:р} ждали", source));
        }
    }

    public sealed class FeedTemplateTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        /// <summary>Тестовые названия всех четырёх родов: формы помечены падежом, чтобы было видно, какой взят.</summary>
        private static NounForms TestNoun(string word, GrammaticalGender gender) =>
            new NounForms(word, word + "-р", word + "-д", word + "-в", word + "-т", word + "-п", gender);

        /// <summary>Склейки, которые дают скобки от мужской формы с беглой гласной: «ушёл[|ла]» → «ушёлла».</summary>
        private static readonly string[] BrokenForms = { "ёлла", "елла", "шелла", "одина", "ёна " };

        private static readonly string[] NounLabels = { "группа", "место", "враг", "заказчик", "груз", "распоряжение", "постройка" };

        [Test]
        public void AllTemplates_BothGenders_AllNounGenders_RenderWithoutMarkup()
        {
            NounForms male = data.Registry.Names.MaleNames[1];
            NounForms female = data.Registry.Names.FemaleNames[1];
            var problems = new List<string>();
            int rendered = 0;

            foreach (FeedTemplate template in data.Registry.Config.FeedTemplates.Templates)
            foreach (string variant in template.Variants)
            foreach (Gender gender in new[] { Gender.Male, Gender.Female })
            foreach (GrammaticalGender nounGender in new[] { GrammaticalGender.Masculine, GrammaticalGender.Feminine, GrammaticalGender.Neuter, GrammaticalGender.Plural })
            {
                NounForms subject = gender == Gender.Male ? male : female;
                NounForms other = gender == Gender.Male ? female : male;
                var source = new DictionarySource()
                    .Set("имя", TextValue.Person(subject.Nominative, subject, gender))
                    .Set("напарник", TextValue.Person(other.Nominative, other, gender == Gender.Male ? Gender.Female : Gender.Male))
                    .Set("лекарь", TextValue.Person(other.Nominative, other, gender == Gender.Male ? Gender.Female : Gender.Male))
                    .Set("щит", TextValue.Person(subject.Nominative, subject, gender))
                    .Set("решающий", TextValue.Person(subject.Nominative, subject, gender))
                    .Set("число", TextValue.Number(2)).Set("всего", TextValue.Number(4)).Set("сумма", TextValue.Number(50))
                    .Set("доход", TextValue.Number(300)).Set("расход", TextValue.Number(200))
                    .Set("архетип", TextValue.Word(gender == Gender.Male ? "Разведчик" : "Разведчица"))
                    .Set("причина", TextValue.Word("устал"))
                    .Set("название", TextValue.Word("Серые волки"));
                foreach (string label in NounLabels) source.Set(label, TextValue.Noun(TestNoun(label, label == "группа" ? GrammaticalGender.Feminine : nounGender)));

                var errors = new List<string>();
                string text = TextRenderer.Render(variant, source, errors);
                rendered++;
                if (errors.Count > 0 || text.IndexOfAny(new[] { '{', '}', '[', ']', '@', '|' }) >= 0 || BrokenForms.Any(text.Contains))
                    problems.Add($"{template.Key} {gender} {nounGender}: «{text}» {string.Join("; ", errors)}");
            }

            Assert.Greater(rendered, 0);
            CollectionAssert.IsEmpty(problems);
        }

        [Test]
        public void SampleTemplates_RightGenderAndCases()
        {
            NounForms yan = data.Registry.NameForms("Ян");
            NounForms mara = data.Registry.NameForms("Мара");
            Assert.IsNotNull(yan);
            Assert.IsNotNull(mara);

            string Render(string key, int variant, bool female, GrammaticalGender nounGender = GrammaticalGender.Masculine)
            {
                var source = new DictionarySource()
                    .Set("имя", female ? TextValue.Person("Мара", mara, Gender.Female) : TextValue.Person("Ян", yan, Gender.Male))
                    .Set("напарник", female ? TextValue.Person("Ян", yan, Gender.Male) : TextValue.Person("Мара", mara, Gender.Female))
                    .Set("враг", TextValue.Noun(TestNoun("враг", nounGender)))
                    .Set("заказчик", TextValue.Noun(TestNoun("заказчик", nounGender)));
                FeedTemplate template = data.Registry.FeedTemplates(key)[0];
                return TextRenderer.Render(template.Variants[variant], source, null);
            }

            Assert.AreEqual("Мара тяжело ранена. Ян тащит её на себе", Render("quest.wound.heavy", 0, true));
            Assert.AreEqual("Ян тяжело ранен. Мара тащит его на себе", Render("quest.wound.heavy", 0, false));
            Assert.AreEqual("У Мары рассечена бровь. Кровь заливает глаз, но она держится", Render("quest.wound.light", 1, true));
            Assert.AreEqual("Ян закрыл собой Мару", Render("quest.tension.hero", 0, false));
            Assert.AreEqual("Мара кинулась на враг-в одна, не дожидаясь остальных", Render("quest.tension.rush", 0, true));
            Assert.AreEqual("Когда враг вышли из темноты, Мары уже не было рядом", Render("quest.tension.flee", 1, true, GrammaticalGender.Plural));
            Assert.AreEqual("Заказчик добралась до места целой. Расплатилась не торгуясь",
                Render("quest.round.success.escort", 0, false, GrammaticalGender.Feminine));
            Assert.AreEqual("В гильдию пришла новенькая — Мара", Render(FeedKeys.AdventurerJoined, 0, true));
            Assert.AreEqual("После гибели Яна Мара другая", Render("reveal.trait.grieving", 0, true));
            Assert.AreEqual("Мара полезла в драку с Яном. Разнимали втроём", Render(FeedKeys.BreakdownBrawl, 0, true));
        }

        [Test]
        public void EveryName_HasAllSixForms()
        {
            foreach (NounForms name in data.Registry.Names.MaleNames.Concat(data.Registry.Names.FemaleNames))
            foreach (GrammaticalCase grammaticalCase in Enum.GetValues(typeof(GrammaticalCase)))
            {
                Assert.IsNotEmpty(name.Get(grammaticalCase), $"{name.Nominative}: {grammaticalCase}");
            }
        }

        [Test]
        public void TraitNamedLine_OnlyAfterReveal()
        {
            SpecialTraitDefinition drunkard = data.Registry.Get<SpecialTraitDefinition>("Drunkard");
            var templates = new[]
            {
                FeedTestTemplates.Make("test.tavern", FeedImportance.Normal, new FeedCondition[0], "{имя} весь день в таверне"),
                FeedTestTemplates.Make("test.tavern", FeedImportance.Normal,
                    new[] { FeedTestTemplates.Condition(FeedConditionKind.RevealedTrait, ("trait", drunkard)) }, "Пьяница {имя} опять в таверне"),
            };

            using (var world = new StateWorld())
            {
                Adventurer adventurer = world.Add("Drunkard");
                SimEvent simEvent = world.Do(ctx => ctx.Events.Publish(SimEventType.Debug, EventImportance.Normal, adventurer.Id));
                var context = new FeedConditionContext(simEvent, adventurer, world.Registry);

                Assert.AreSame(templates[0], FeedConditions.Select(templates, context), "черта скрыта — только поведение");
                adventurer.TryGetTrait("Drunkard", out TraitInstance trait);
                trait.Revealed = true;
                Assert.AreSame(templates[1], FeedConditions.Select(templates, context), "черта раскрыта — строка с названием");
            }
        }

        [Test]
        public void AxisPoleNamedLine_OnlyAfterReveal()
        {
            var templates = new[]
            {
                FeedTestTemplates.Make("test.fear", FeedImportance.Notable, new FeedCondition[0], "{имя} побледнел[|а]"),
                FeedTestTemplates.Make("test.fear", FeedImportance.Notable, new[]
                {
                    FeedTestTemplates.Condition(FeedConditionKind.RevealedAxisPole, ("axis", AxisId.Risk), ("pole", AxisPole.Negative)),
                }, "Трус {имя} побледнел"),
            };

            using (var world = new StateWorld())
            {
                Adventurer adventurer = world.Add();
                adventurer.SetAxis(AxisId.Risk, -80f);
                SimEvent simEvent = world.Do(ctx => ctx.Events.Publish(SimEventType.Debug, EventImportance.Normal, adventurer.Id));
                var context = new FeedConditionContext(simEvent, adventurer, world.Registry);

                Assert.AreSame(templates[0], FeedConditions.Select(templates, context));
                adventurer.SetAxisRevealed(AxisId.Risk);
                Assert.AreSame(templates[1], FeedConditions.Select(templates, context));
            }
        }

        [Test]
        public void ArchetypeCondition_MostSpecificWins()
        {
            ArchetypeDefinition archetype = data.Registry.All<ArchetypeDefinition>()[0];
            var templates = new[]
            {
                FeedTestTemplates.Make("test.any", FeedImportance.Normal, new FeedCondition[0], "общая"),
                FeedTestTemplates.Make("test.any", FeedImportance.Normal,
                    new[] { FeedTestTemplates.Condition(FeedConditionKind.Archetype, ("archetype", archetype)) }, "для архетипа"),
            };

            using (var world = new StateWorld())
            {
                Adventurer adventurer = world.Add();
                SimEvent simEvent = world.Do(ctx => ctx.Events.Publish(SimEventType.Debug, EventImportance.Normal, adventurer.Id));

                adventurer.ArchetypeId = archetype.Id;
                Assert.AreSame(templates[1], FeedConditions.Select(templates, new FeedConditionContext(simEvent, adventurer, world.Registry)));
                adventurer.ArchetypeId = "Other";
                Assert.AreSame(templates[0], FeedConditions.Select(templates, new FeedConditionContext(simEvent, adventurer, world.Registry)));
            }
        }
    }

    public sealed class FeedSystemTests
    {
        private static Adventurer AddNamed(StateWorld world, string name, Gender gender)
        {
            Adventurer adventurer = world.Add();
            adventurer.Name = name;
            adventurer.Gender = gender;
            return adventurer;
        }

        /// <summary>Опубликовать событие как команду на паузе и сделать такт: лента видит его в этом такте.</summary>
        private static FeedEntry Publish(StateWorld world, Func<SimContext, SimEvent> publish)
        {
            int before = world.Simulation.World.Feed.Guild.Count;
            world.Do(ctx => { publish(ctx); });
            world.Simulation.Tick();
            IReadOnlyList<FeedEntry> feed = world.Simulation.World.Feed.Guild;
            Assert.AreEqual(before + 1, feed.Count, "событие дало одну строку");
            return feed[feed.Count - 1];
        }

        private static IEnumerable<TestCaseData> EventCases()
        {
            yield return new TestCaseData(SimEventType.CandidateArrived, EventImportance.Normal).SetName("CandidateArrived");
            yield return new TestCaseData(SimEventType.CandidateLeft, EventImportance.Normal).SetName("CandidateLeft");
            yield return new TestCaseData(SimEventType.AdventurerJoined, EventImportance.Normal).SetName("AdventurerJoined");
            yield return new TestCaseData(SimEventType.AdventurerLeft, EventImportance.Important).SetName("AdventurerLeft");
            yield return new TestCaseData(SimEventType.WalletEmptied, EventImportance.Notable).SetName("WalletEmptied");
            yield return new TestCaseData(SimEventType.WoundHealed, EventImportance.Normal).SetName("WoundHealed");
            yield return new TestCaseData(SimEventType.WoundComplicated, EventImportance.Notable).SetName("WoundComplicated");
        }

        [TestCaseSource(nameof(EventCases))]
        public void Event_GivesLine_OfEventImportance_WithLinks(SimEventType type, EventImportance importance)
        {
            using (var world = new StateWorld())
            {
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                // Причина ухода — текст из события; остальным событиям лишнее поле не мешает.
                FeedEntry entry = Publish(world, ctx => ctx.Events.Publish(type, importance, yan.Id).With("reason", "устал"));

                Assert.AreEqual(importance, entry.Importance);
                Assert.AreEqual(FeedKind.Guild, entry.Feed);
                CollectionAssert.AreEqual(new[] { yan.Id }, entry.Links);
                StringAssert.Contains("Ян", entry.Text);
                StringAssert.DoesNotContain("{", entry.Text);
            }
        }

        [Test]
        public void ArchetypeChanged_NamesArchetype_InPersonGender()
        {
            using (var world = new StateWorld())
            {
                ArchetypeDefinition archetype = world.Registry.All<ArchetypeDefinition>().First(a => a.DisplayNameFemale != a.DisplayName);
                Adventurer mara = AddNamed(world, "Мара", Gender.Female);
                FeedEntry entry = Publish(world, ctx => ctx.Events.Publish(SimEventType.ArchetypeChanged, EventImportance.Normal, mara.Id)
                    .With("from", "x").With("to", archetype.Id));

                Assert.AreEqual($"Мара теперь — {archetype.DisplayNameFemale}", entry.Text);
                Assert.AreEqual(EventImportance.Normal, entry.Importance);
            }
        }

        [Test]
        public void Reveals_GiveImportantLines_FromDataKeys()
        {
            using (var world = new StateWorld())
            {
                Adventurer mara = AddNamed(world, "Мара", Gender.Female);
                mara.SetAxis(AxisId.Loyalty, 90f);
                FeedEntry axis = Publish(world, ctx =>
                {
                    RevealService.TryRevealAxis(ctx, mara, AxisId.Loyalty, world.Registry.Axis(AxisId.Loyalty).PositivePole.RevealTrigger);
                    return null;
                });
                Assert.AreEqual("Все ушли. Мара осталась", axis.Text);
                Assert.AreEqual(EventImportance.Important, axis.Importance);

                FeedEntry balanced = Publish(world, ctx =>
                {
                    RevealService.TryRevealBalanced(ctx, mara, AxisId.Principles);
                    return null;
                });
                Assert.AreEqual("Мара не святая, но и не воровка", balanced.Text);
                Assert.AreEqual(EventImportance.Notable, balanced.Importance);
            }
        }

        [Test]
        public void TraitReveal_WithPartner_LinksBoth()
        {
            using (var world = new StateWorld())
            {
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                Adventurer mara = AddNamed(world, "Мара", Gender.Female);
                SpecialTraitDefinition grieving = world.Registry.All<SpecialTraitDefinition>().First(t => t.RevealFeedKey == "reveal.trait.grieving");
                TraitService.AddAtGeneration(mara, grieving, world.Time.TotalHours, yan.Id);
                mara.TryGetTrait(grieving.Id, out TraitInstance trait);

                FeedEntry entry = Publish(world, ctx =>
                {
                    RevealService.Reveal(ctx, mara, trait, grieving);
                    return null;
                });

                Assert.AreEqual("После гибели Яна Мара другая", entry.Text);
                Assert.AreEqual(EventImportance.Important, entry.Importance);
                CollectionAssert.AreEqual(new[] { mara.Id, yan.Id }, entry.Links);
            }
        }

        [TestCase(BreakdownKind.Binge, FeedKeys.BreakdownBinge)]
        [TestCase(BreakdownKind.Brawl, FeedKeys.BreakdownBrawl)]
        [TestCase(BreakdownKind.RefuseQuests, FeedKeys.BreakdownRefuse)]
        [TestCase(BreakdownKind.Collapse, FeedKeys.BreakdownCollapse)]
        public void Breakdown_EachKind_OwnImportantLine(BreakdownKind kind, string key)
        {
            using (var world = new StateWorld())
            {
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                AddNamed(world, "Мара", Gender.Female);
                FeedEntry entry = Publish(world, ctx => StateService.StartBreakdown(ctx, yan, kind));

                Assert.AreEqual(key, entry.TemplateKey);
                Assert.AreEqual(EventImportance.Important, entry.Importance);
                StringAssert.StartsWith("Ян ", entry.Text);
            }
        }

        [Test]
        public void Brawl_WithNobodyAround_OwnLine()
        {
            using (var world = new StateWorld())
            {
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                FeedEntry entry = Publish(world, ctx => StateService.StartBreakdown(ctx, yan, BreakdownKind.Brawl));

                Assert.AreEqual(FeedKeys.BreakdownBrawlAlone, entry.TemplateKey);
                Assert.AreEqual("Ян разбил кружку о стену и ушёл, не сказав ни слова", entry.Text);
            }
        }

        [Test]
        public void WoundHealed_WhileOtherWoundsRemain_NoLine()
        {
            using (var world = new StateWorld())
            {
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                world.Do(ctx => HealthService.Wound(ctx, yan, ConditionKind.LightWound));
                world.Simulation.Tick();
                int before = world.Simulation.World.Feed.Guild.Count;

                world.Do(ctx => { ctx.Events.Publish(SimEventType.WoundHealed, EventImportance.Normal, yan.Id); });
                world.Simulation.Tick();

                Assert.AreEqual(before, world.Simulation.World.Feed.Guild.Count);
            }
        }

        [Test]
        public void EventsWithoutKey_GiveNoLine()
        {
            using (var world = new StateWorld())
            {
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                world.Do(ctx =>
                {
                    ctx.Events.Publish(SimEventType.CandidateRejected, EventImportance.Normal, yan.Id);
                    ctx.Events.Publish(SimEventType.TraitAcquired, EventImportance.Normal, yan.Id).With("trait", "Drunkard");
                    ctx.Events.Publish(SimEventType.ReputationChanged, EventImportance.Normal, yan.Id);
                });
                world.Simulation.Tick();

                Assert.AreEqual(0, world.Simulation.World.Feed.Guild.Count);
            }
        }

        [Test]
        public void Variant_NeverRepeatsInARow()
        {
            using (var world = new StateWorld())
            {
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                Assert.Greater(world.Registry.FeedTemplates(FeedKeys.WalletEmptied)[0].Variants.Count, 1);

                string previous = null;
                for (int i = 0; i < 30; i++)
                {
                    FeedEntry entry = Publish(world, ctx => ctx.Events.Publish(SimEventType.WalletEmptied, EventImportance.Notable, yan.Id));
                    Assert.AreNotEqual(previous, entry.Text, $"строка {i}");
                    previous = entry.Text;
                }
            }
        }

        [Test]
        public void LineGoesToLog_AsInfo()
        {
            var writer = new StringWriter();
            using (var world = new StateWorld(log: new SimLogger(SimLogLevel.Info, writer)))
            {
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                Publish(world, ctx => ctx.Events.Publish(SimEventType.AdventurerLeft, EventImportance.Important, yan.Id));

                StringAssert.Contains("[FeedSystem] [Info] feed [В] guild.adventurer.left: Ян ушёл из гильдии", writer.ToString());
            }
        }

        [Test]
        public void MissingLabel_IsErrorInLog_LineStillWritten()
        {
            var writer = new StringWriter();
            using (var world = new StateWorld(log: new SimLogger(SimLogLevel.Info, writer)))
            {
                FeedEntry entry = Publish(world, ctx => ctx.Events.Publish(SimEventType.AdventurerLeft, EventImportance.Important));

                Assert.AreEqual("{имя} ушёл из гильдии: {причина}", entry.Text);
                StringAssert.Contains("[FeedSystem] [Error] feed guild.adventurer.left: метка {имя} без источника", writer.ToString());
            }
        }

        [Test]
        public void Feed_KeepsOnlyLimit_CountsAll()
        {
            using (var world = new StateWorld())
            {
                world.Data.Set("feed.guildFeedLimit", 5);
                Adventurer yan = AddNamed(world, "Ян", Gender.Male);
                world.Do(ctx =>
                {
                    for (int i = 0; i < 12; i++) ctx.Events.Publish(SimEventType.WalletEmptied, EventImportance.Notable, yan.Id);
                });
                world.Simulation.Tick();

                Assert.AreEqual(5, world.Simulation.World.Feed.Guild.Count);
                Assert.AreEqual(12, world.Simulation.World.Feed.GetAddedCount(EventImportance.Notable));
            }
        }

        /// <summary>За год: каждое событие с ключом дало строку той же важности, что у события, со ссылками на участников.</summary>
        [Test]
        public void Year_EveryKeyedEvent_GivesLineOfItsImportance()
        {
            using (var data = new PeopleData())
            {
                data.Set("feed.guildFeedLimit", 1000000);
                var expected = new List<(EventImportance importance, int[] links, SimEventType type)>();
                var simulation = Simulation.CreateDefault(data.Registry, 11u);
                simulation.TickCompleted += events =>
                {
                    foreach (SimEvent simEvent in events)
                    {
                        // В ленту гильдии: строка гильдии; строка задания, важная для гильдии (без своей строки гильдии); своя строка
                        // гильдии события задания. Остальные строки задания — в ленте задания.
                        string key = FeedKeys.Of(simEvent, simulation.World, data.Registry);
                        string guildLine = FeedKeys.GuildLineOf(simEvent);
                        bool questLine = key != null && data.Registry.FeedTemplates(key)[0].Feed == FeedKind.Quest;
                        if (key != null && (!questLine || (FeedKeys.IsGuildWorthy(simEvent) && guildLine == null)))
                            expected.Add((Importance(data.Registry, key), simEvent.Participants.ToArray(), simEvent.Type));
                        if (guildLine != null) expected.Add((Importance(data.Registry, guildLine), simEvent.Participants.ToArray(), simEvent.Type));
                    }
                };
                for (int day = 0; day < 360; day++)
                for (int hour = 0; hour < simulation.Calendar.HoursPerDay; hour++)
                {
                    foreach (Candidate candidate in simulation.World.Adventurers.Candidates.ToList())
                        simulation.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
                    simulation.Tick();
                }

                IReadOnlyList<FeedEntry> feed = simulation.World.Feed.Guild;
                Assert.AreEqual(expected.Count, feed.Count);
                var types = new HashSet<SimEventType>();
                for (int i = 0; i < feed.Count; i++)
                {
                    Assert.AreEqual(expected[i].importance, feed[i].Importance, $"{expected[i].type}: «{feed[i].Text}»");
                    CollectionAssert.AreEqual(expected[i].links, feed[i].Links);
                    StringAssert.DoesNotContain("{", feed[i].Text);
                    types.Add(expected[i].type);
                }
                TestContext.WriteLine($"строк: {feed.Count}, типов событий: {string.Join(", ", types)}");
                Assert.GreaterOrEqual(types.Count, 5);
            }
        }

        /// <summary>Важность строки — из шаблона (у события задания она может отличаться от важности события).</summary>
        private static EventImportance Importance(DataRegistry registry, string key) => FeedSystem.ToImportance(registry.FeedTemplates(key)[0].Importance);
    }

    public sealed class FeedDeterminismTests
    {
        private static (string feed, string log) Run(PeopleData data, uint seed, bool withFeed)
        {
            var writer = new StringWriter();
            List<ISimSystem> systems = SimulationSystems.CreateDefault();
            if (!withFeed) systems.RemoveAll(s => s is FeedSystem);
            var simulation = new Simulation(data.Registry, seed, systems);
            string events = SimulationLog.Record(simulation, s =>
            {
                for (int day = 0; day < 180; day++)
                for (int hour = 0; hour < s.Calendar.HoursPerDay; hour++)
                {
                    foreach (Candidate candidate in s.World.Adventurers.Candidates.ToList()) s.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
                    s.Tick();
                }
            });
            string feed = string.Join("\n", simulation.World.Feed.Guild.Select(e => e.TimeHours + " " + e.Text));
            return (feed, events);
        }

        [Test]
        public void SameSeed_SameFeed()
        {
            using (var data = new PeopleData())
            {
                (string feed, string log) first = Run(data, 21u, true);
                (string feed, string log) second = Run(data, 21u, true);

                Assert.IsNotEmpty(first.feed);
                Assert.AreEqual(first.feed, second.feed);
                Assert.AreEqual(first.log, second.log);
            }
        }

        [Test]
        public void FeedSystem_DoesNotShiftOtherSystems()
        {
            using (var data = new PeopleData())
            {
                Assert.AreEqual(Run(data, 22u, false).log, Run(data, 22u, true).log);
            }
        }

        [Test]
        public void Summary_CountsFeedLinesByImportance()
        {
            using (var data = new PeopleData())
            {
                HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 31u, 359, PlayerBots.Simple());
                RunSummary summary = result.Summary;
                FeedState feed = result.Simulation.World.Feed;

                foreach ((string column, EventImportance importance) in new[]
                         {
                             ("Лента [О]", EventImportance.Normal), ("Лента [З]", EventImportance.Notable), ("Лента [В]", EventImportance.Important),
                         })
                {
                    int index = summary.MonthColumns.Select(c => c.Name).ToList().IndexOf(column);
                    Assert.GreaterOrEqual(index, 0, column);
                    Assert.AreEqual(feed.GetAddedCount(importance), summary.Totals[index], column);
                }
                Assert.Greater(feed.GetAddedCount(EventImportance.Normal), 0);
            }
        }
    }
}
