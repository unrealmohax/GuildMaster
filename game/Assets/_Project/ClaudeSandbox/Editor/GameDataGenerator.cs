using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GuildMaster.Data;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Заводит и заполняет ассеты данных прототипа по документам: numbers.md, trait-effects.md,
    /// archetypes.md, quests.md, event-feed.md. Меню: GuildMaster → Sandbox → Generate Game Data.
    /// <para>
    /// Перезаписывает определения и тексты целиком (правки, сделанные руками в этих ассетах, пропадут).
    /// BalanceSettings не перезаписывает: только сохраняет на диск новые разделы с числами по умолчанию из кода.
    /// </para>
    /// Приватные поля ставятся через отражение — у классов данных нет сеттеров, и так и должно быть.
    /// </summary>
    public static partial class GameDataGenerator
    {
        private const string Root = "Assets/_Project/Data";

        private static readonly List<UnityEngine.Object> Touched = new List<UnityEngine.Object>();

        [MenuItem("GuildMaster/Sandbox/Generate Game Data")]
        public static void Generate()
        {
            Touched.Clear();

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>($"{Root}/GameConfig.asset");
            var balance = AssetDatabase.LoadAssetAtPath<BalanceSettings>($"{Root}/BalanceSettings.asset");
            if (config == null || balance == null)
            {
                Debug.LogError("[GuildMaster] Generate Game Data: нет GameConfig.asset или BalanceSettings.asset в " + Root);
                return;
            }

            var catalog = Asset<StatCatalog>("", "StatCatalog");
            var axes = Enum.GetValues(typeof(AxisId)).Cast<AxisId>().ToDictionary(a => a, a => Asset<AxisDefinition>("Axes", a.ToString()));
            var traits = TraitIds.ToDictionary(id => id, id => Asset<SpecialTraitDefinition>("Traits", id));
            var archetypes = ArchetypeIds.ToDictionary(id => id, id => Asset<ArchetypeDefinition>("Archetypes", id));
            var questTypes = QuestTypeIds.ToDictionary(id => id, id => Asset<QuestTypeDefinition>("QuestTypes", id));
            var ambush = Asset<RandomEventDefinition>("Encounters", "Ambush");
            var beasts = Asset<RandomEventDefinition>("Encounters", "Beasts");
            var cave = Asset<DiscoveryDefinition>("Encounters", "Cave");
            var buildings = BuildingIds.ToDictionary(id => id, id => Asset<BuildingDefinition>("Buildings", id));
            var staff = StaffIds.ToDictionary(id => id, id => Asset<StaffRoleDefinition>("Staff", id));
            var decrees = DecreeIds.ToDictionary(id => id, id => Asset<DecreeDefinition>("Decrees", id));
            var dilemmas = DilemmaIds.ToDictionary(id => id, id => Asset<DilemmaDefinition>("Dilemmas", id));
            var feed = Asset<FeedTemplateSet>("Text", "FeedTemplates");
            var names = Asset<NameList>("Text", "NameList");
            var orderTexts = Asset<OrderTextTemplates>("Text", "OrderTextTemplates");

            FillStatCatalog(catalog);
            FillAxes(axes);
            FillTraits(traits);
            FillArchetypes(archetypes);
            FillQuestTypes(questTypes);
            FillEncounters(ambush, beasts, cave, questTypes);
            FillBuildings(buildings, staff);
            FillStaff(staff, buildings);
            FillDecrees(decrees);
            FillDilemmas(dilemmas, traits);
            FillFeed(feed, questTypes, traits);
            FillNames(names);
            FillOrderTexts(orderTexts, questTypes);

            Set(config, "balance", balance);
            Set(config, "statCatalog", catalog);
            Set(config, "axes", axes.Values.ToList());
            Set(config, "specialTraits", TraitIds.Select(id => traits[id]).ToList());
            Set(config, "archetypes", ArchetypeIds.Select(id => archetypes[id]).ToList());
            Set(config, "questTypes", QuestTypeIds.Select(id => questTypes[id]).ToList());
            Set(config, "randomEvents", new List<RandomEventDefinition> { ambush, beasts });
            Set(config, "discoveries", new List<DiscoveryDefinition> { cave });
            Set(config, "buildings", BuildingIds.Select(id => buildings[id]).ToList());
            Set(config, "staffRoles", StaffIds.Select(id => staff[id]).ToList());
            Set(config, "decrees", DecreeIds.Select(id => decrees[id]).ToList());
            Set(config, "dilemmas", DilemmaIds.Select(id => dilemmas[id]).ToList());
            Set(config, "feedTemplates", feed);
            Set(config, "nameList", names);
            Set(config, "orderTexts", orderTexts);

            // Баланс: новые разделы уже в памяти со стартовыми числами из кода — записать на диск, ничего не меняя.
            Touched.Add(config);
            Touched.Add(balance);
            foreach (UnityEngine.Object asset in Touched) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GuildMaster] Generate Game Data: записано ассетов — {Touched.Count}");
        }

        // ---------- Параметры ----------

        private static void FillStatCatalog(StatCatalog catalog)
        {
            Set(catalog, "stats", new List<StatInfo>
            {
                Stat(StatId.Strength, "Сила", "Урон в ближнем бою, тяжёлое оружие и броня"),
                Stat(StatId.Endurance, "Выносливость", "Здоровье, сколько урона держит, сопротивление усталости"),
                Stat(StatId.Agility, "Ловкость", "Скорость, уклонение, точные движения"),
                Stat(StatId.Reaction, "Реакция", "Кто действует первым, защита от засад"),
                Stat(StatId.Perception, "Наблюдательность", "Следы, ловушки, засады, «прочитать» человека"),
                Stat(StatId.Composure, "Хладнокровие", "Стойкость под давлением: не бежит и не паникует"),
                Stat(StatId.Charisma, "Обаяние", "Переговоры, влияние на людей, лидерство"),
                Stat(StatId.Cohesion, "Слаженность", "Насколько хорошо вклинивается в команду. От неё зависит, насколько эффективно работают его характеристики в группе"),
                Stat(StatId.Marksmanship, "Меткость", "Дальний бой, точные удары"),
                Stat(StatId.Stealth, "Скрытность", "Остаться незамеченным"),
                Stat(StatId.Knowledge, "Знания", "Монстры, травы, местность, слабые места"),
                Stat(StatId.Survival, "Выживание", "Поход, ориентирование, лагерь, еда"),
                Stat(StatId.Medicine, "Медицина", "Лечение ран на задании"),
                Stat(StatId.Crafting, "Ремесло", "Починка, изготовление смесей и снаряжения"),
            });
            Set(catalog, "radarOrder", new List<StatId>
            {
                StatId.Strength, StatId.Reaction, StatId.Marksmanship, StatId.Stealth, StatId.Agility, StatId.Medicine, StatId.Composure,
                StatId.Charisma, StatId.Perception, StatId.Survival, StatId.Knowledge, StatId.Crafting, StatId.Endurance,
            });
        }

        private static StatInfo Stat(StatId stat, string name, string description) =>
            Make<StatInfo>(("stat", stat), ("displayName", name), ("description", description), ("isSkill", !Vocabulary.IsCharacteristic(stat)));

        // ---------- Оси ----------

        private static void FillAxes(Dictionary<AxisId, AxisDefinition> axes)
        {
            Axis(axes[AxisId.Risk], "Риск",
                Pole("Трус", "Трусиха", RevealTrigger.CowardPanicOrFlee, "reveal.risk.negative",
                    Motive(Data.Motive.Safety, Arrow.StrongUp),
                    Motive(Data.Motive.Glory, Arrow.Down),
                    Rate(StateStat.Stress, RateDirection.Growth, Arrow.StrongUp),
                    Risk(0.3f),
                    Tension(TensionKind.Panic, 0.2f),
                    Tension(TensionKind.Flee, 0.15f, EffectCondition.ExtremePole)),
                Pole("Безрассудный", "Безрассудная", RevealTrigger.RecklessRush, "reveal.risk.positive",
                    Motive(Data.Motive.Glory, Arrow.Up),
                    Motive(Data.Motive.Safety, Arrow.StrongDown),
                    Risk(-0.3f),
                    Tension(TensionKind.Rush, 0.15f, EffectCondition.ExtremePole)));

            Axis(axes[AxisId.Money], "Деньги",
                Pole("Бескорыстный", "Бескорыстная", RevealTrigger.SelflessUnderpaidOrder, "reveal.money.negative",
                    Motive(Data.Motive.Money, Arrow.Down)),
                Pole("Жадный", "Жадная", RevealTrigger.GreedyRefusedPay, "reveal.money.positive",
                    Motive(Data.Motive.Money, Arrow.StrongUp),
                    Pay(2f),
                    Party(PartySizePreference.SmallGroup)));

            Axis(axes[AxisId.People], "Люди",
                Pole("Одиночка", "Одиночка", RevealTrigger.LonerWentSolo, "reveal.people.negative",
                    Motive(Data.Motive.Companions, Arrow.Down),
                    Party(PartySizePreference.Solo),
                    Rate(StateStat.Stress, RateDirection.Growth, Arrow.Up, EffectCondition.InGroup),
                    Rate(StateStat.Contentment, RateDirection.Decay, Arrow.Up, EffectCondition.InGroup)),
                Pole("Командный", "Командная", RevealTrigger.TeamRefusedSolo, "reveal.people.positive",
                    Motive(Data.Motive.Companions, Arrow.StrongUp),
                    Party(PartySizePreference.Group),
                    Rate(StateStat.Stress, RateDirection.Growth, Arrow.Up, EffectCondition.Solo)));

            Axis(axes[AxisId.Work], "Труд",
                Pole("Ленивый", "Ленивая", RevealTrigger.LazyIdleDays, "reveal.work.negative",
                    Motive(Data.Motive.Rest, Arrow.StrongUp)),
                Pole("Амбициозный", "Амбициозная", RevealTrigger.AmbitiousTopOrder, "reveal.work.positive",
                    Motive(Data.Motive.Glory, Arrow.StrongUp)));

            Axis(axes[AxisId.Loyalty], "Верность",
                Pole("Наёмник", "Наёмница", RevealTrigger.MercenaryLeaveCheck, "reveal.loyalty.negative",
                    Rate(StateStat.Loyalty, RateDirection.Growth, Arrow.Down),
                    Rate(StateStat.Loyalty, RateDirection.Decay, Arrow.StrongUp)),
                Pole("Преданный", "Преданная", RevealTrigger.LoyalStayedInHardTimes, "reveal.loyalty.positive",
                    Rate(StateStat.Loyalty, RateDirection.Growth, Arrow.StrongUp),
                    Rate(StateStat.Loyalty, RateDirection.Decay, Arrow.Down)));

            Axis(axes[AxisId.Principles], "Принципы",
                Pole("Беспринципный", "Беспринципная", RevealTrigger.UnprincipledCaughtSkimming, "reveal.principles.negative"),
                Pole("Честный", "Честная", RevealTrigger.HonestChoice, "reveal.principles.positive"));
        }

        private static void Axis(AxisDefinition asset, string displayName, AxisPoleDefinition negative, AxisPoleDefinition positive)
        {
            AxisId axis = (AxisId)Enum.Parse(typeof(AxisId), asset.name);
            Def(asset, asset.name, displayName);
            Set(asset, "axis", axis);
            Set(asset, "negativePole", negative);
            Set(asset, "positivePole", positive);
            Set(asset, "balancedFeedKey", $"reveal.{axis.ToString().ToLowerInvariant()}.balanced");
        }

        private static AxisPoleDefinition Pole(string name, string nameFemale, RevealTrigger trigger, string feedKey, params TraitEffect[] effects) =>
            Make<AxisPoleDefinition>(("name", name), ("nameFemale", nameFemale), ("effects", effects.ToList()),
                ("revealTrigger", trigger), ("revealFeedKey", feedKey));

        // ---------- Особые черты ----------

        private static readonly string[] TraitIds =
        {
            "Veteran", "Deserter", "Drunkard", "Braggart", "IronNerves", "Family", "Rival", "Lover",
            "Nightmares", "Maimed", "Grieving", "Tested",
        };

        private static void FillTraits(Dictionary<string, SpecialTraitDefinition> t)
        {
            Trait(t["Veteran"], "Ветеран войны", "", TraitCategory.Past, RevealTrigger.VeteranFirstTension, "reveal.trait.veteran",
                new[] { TraitHook.VeteranBinge },
                Tension(TensionKind.Panic, -0.2f));
            Trait(t["Deserter"], "Бывший дезертир", "", TraitCategory.Past, RevealTrigger.DeserterFled, "reveal.trait.deserter",
                new TraitHook[0],
                Tension(TensionKind.Flee, 0.1f, EffectCondition.StressAbove, 60f));
            Trait(t["Drunkard"], "Пьяница", "", TraitCategory.Habit, RevealTrigger.DrunkardSkippedDay, "reveal.trait.drunkard",
                new[] { TraitHook.DrunkardSkipsDay, TraitHook.DrunkardComfortOnlyInTavern },
                Motive(Data.Motive.Comfort, Arrow.StrongUp));
            Trait(t["Braggart"], "Хвастун", "Хвастунья", TraitCategory.Habit, RevealTrigger.BraggartOverreached, "reveal.trait.braggart",
                new[] { TraitHook.BraggartReputation },
                Risk(-0.4f));
            Trait(t["IronNerves"], "Железные нервы", "", TraitCategory.Strength, RevealTrigger.IronNervesHeld, "reveal.trait.ironNerves",
                new[] { TraitHook.IronNervesComradeDeath },
                Tension(TensionKind.Panic, -0.3f));
            Trait(t["Family"], "Семейный", "Семейная", TraitCategory.Relation, RevealTrigger.FamilyRefusedDanger, "reveal.trait.family",
                new[] { TraitHook.FamilySendsMoneyHome, TraitHook.FamilyLeaveChance },
                Motive(Data.Motive.Safety, Arrow.Up));
            Trait(t["Rival"], "Соперник", "Соперница", TraitCategory.Relation, RevealTrigger.RivalFirstClash, "reveal.trait.rival",
                new[] { TraitHook.RivalPartner });
            Trait(t["Lover"], "Влюблённый", "Влюблённая", TraitCategory.Relation, RevealTrigger.ByDilemma, "reveal.trait.lover",
                new[] { TraitHook.LoverPartner });
            Trait(t["Nightmares"], "Кошмары", "", TraitCategory.Acquired, RevealTrigger.NightmaresRefusedOrder, "reveal.trait.nightmares",
                new[] { TraitHook.NightmaresRefuseSimilar },
                RateTimes(StateStat.Fatigue, RateDirection.Growth, 1.3f));
            Trait(t["Maimed"], "Калека", "", TraitCategory.Acquired, RevealTrigger.OnAcquire, "reveal.trait.maimed",
                new TraitHook[0],
                Profile(0.7f, StatId.Strength, StatId.Endurance, StatId.Agility, StatId.Reaction));
            Trait(t["Grieving"], "Потерявший товарища", "Потерявшая товарища", TraitCategory.Acquired, RevealTrigger.GrievingAfterDecline, "reveal.trait.grieving",
                new[] { TraitHook.GrievingOutcome },
                Rate(StateStat.Stress, RateDirection.Decay, Arrow.Down));
            Trait(t["Tested"], "Проверенный", "Проверенная", TraitCategory.Acquired, RevealTrigger.OnAcquire, "reveal.trait.tested",
                new[] { TraitHook.TestedOnAcquire });

            Set(t["Rival"], "requiresPartner", true);
            Set(t["Lover"], "requiresPartner", true);
            Set(t["IronNerves"], "incompatibleWith", new List<SpecialTraitDefinition> { t["Nightmares"] });
            Set(t["Nightmares"], "incompatibleWith", new List<SpecialTraitDefinition> { t["IronNerves"] });
        }

        private static void Trait(SpecialTraitDefinition asset, string name, string nameFemale, TraitCategory category, RevealTrigger trigger,
            string feedKey, TraitHook[] hooks, params TraitEffect[] effects)
        {
            Def(asset, asset.name, name);
            Set(asset, "displayNameFemale", nameFemale);
            Set(asset, "category", category);
            Set(asset, "isAcquired", category == TraitCategory.Acquired);
            Set(asset, "effects", effects.ToList());
            Set(asset, "codeHooks", hooks.ToList());
            Set(asset, "requiresPartner", false);
            Set(asset, "incompatibleWith", new List<SpecialTraitDefinition>());
            Set(asset, "revealTrigger", trigger);
            Set(asset, "revealFeedKey", feedKey);
        }

        // ---------- Эффекты черт ----------

        private static TraitEffect Motive(Motive motive, Arrow arrow) =>
            Make<TraitEffect>(("kind", EffectKind.MotiveWeight), ("motive", motive), ("arrow", arrow));

        private static TraitEffect Rate(StateStat stat, RateDirection direction, Arrow arrow, EffectCondition condition = EffectCondition.Always) =>
            Make<TraitEffect>(("kind", EffectKind.StateRate), ("stateStat", stat), ("direction", direction), ("arrow", arrow), ("condition", condition));

        private static TraitEffect RateTimes(StateStat stat, RateDirection direction, float multiplier) =>
            Make<TraitEffect>(("kind", EffectKind.StateRate), ("stateStat", stat), ("direction", direction), ("value", multiplier));

        private static TraitEffect Risk(float share) =>
            Make<TraitEffect>(("kind", EffectKind.RiskPerception), ("value", share));

        private static TraitEffect Tension(TensionKind tension, float chance, EffectCondition condition = EffectCondition.Always, float threshold = 0f) =>
            Make<TraitEffect>(("kind", EffectKind.TensionModifier), ("tension", tension), ("value", chance),
                ("condition", condition), ("conditionThreshold", threshold));

        private static TraitEffect Pay(float multiplier) =>
            Make<TraitEffect>(("kind", EffectKind.PayContentmentSensitivity), ("value", multiplier));

        private static TraitEffect Party(PartySizePreference preference) =>
            Make<TraitEffect>(("kind", EffectKind.PartyPreference), ("partyPreference", preference));

        private static TraitEffect Profile(float multiplier, params StatId[] stats) =>
            Make<TraitEffect>(("kind", EffectKind.ProfileMultiplier), ("value", multiplier), ("stats", stats.ToList()));

        // ---------- Архетипы ----------

        private static readonly string[] ArchetypeIds = { "Shield", "Fighter", "Marksman", "Scout", "Medic", "JackOfAllTrades", "Novice" };

        private static void FillArchetypes(Dictionary<string, ArchetypeDefinition> a)
        {
            Role(a["Shield"], "Щит", "", 10f, "Принимает удар на себя, держит врагов, прикрывает остальных",
                new[] { StatId.Endurance, StatId.Strength }, StatId.Composure, StatId.Cohesion);
            Role(a["Fighter"], "Боец", "", 10f, "Основной урон в ближнем бою, добивает тех, кого держит Щит",
                new[] { StatId.Strength, StatId.Reaction }, StatId.Endurance, StatId.Agility);
            Role(a["Marksman"], "Стрелок", "", 10f, "Бьёт издалека, снимает опасные цели, прикрывает отход",
                new[] { StatId.Marksmanship, StatId.Reaction }, StatId.Perception, StatId.Composure);
            Role(a["Scout"], "Разведчик", "Разведчица", 10f, "Идёт впереди, находит цель, замечает засады и ловушки, справляется со слабыми",
                new[] { StatId.Agility, StatId.Stealth }, StatId.Perception, StatId.Reaction);
            Role(a["Medic"], "Лекарь", "", 10f, "Перевязывает, вправляет, выхаживает раненых на задании",
                new[] { StatId.Medicine, StatId.Composure }, StatId.Knowledge, StatId.Agility);
            Other(a["JackOfAllTrades"], "Мастер на все руки", ArchetypeKind.JackOfAllTrades, 5f, "Хорош во многом, ни в чём не лучший");
            Other(a["Novice"], "Новичок", ArchetypeKind.Novice, 45f, "Роль ещё не сложилась");
        }

        // Временные веса типа при генерации: Новичок 45, роли по 10, Мастер на все руки 5; потом — от уровня и репутации гильдии.
        private static void Role(ArchetypeDefinition asset, string name, string nameFemale, float weight, string description, StatId[] main, params StatId[] secondary)
        {
            Def(asset, asset.name, name);
            Set(asset, "displayNameFemale", nameFemale);
            Set(asset, "description", description);
            Set(asset, "kind", ArchetypeKind.Role);
            Set(asset, "mainStats", main.ToList());
            Set(asset, "secondaryStats", secondary.ToList());
            Set(asset, "generationWeight", weight);
        }

        private static void Other(ArchetypeDefinition asset, string name, ArchetypeKind kind, float weight, string description)
        {
            Def(asset, asset.name, name);
            Set(asset, "displayNameFemale", "");
            Set(asset, "description", description);
            Set(asset, "kind", kind);
            Set(asset, "mainStats", new List<StatId>());
            Set(asset, "secondaryStats", new List<StatId>());
            Set(asset, "generationWeight", weight);
        }

        // ---------- Типы заданий ----------

        // Исследование пещеры — событийное задание из находки: обычным заказом не приходит (вес 0).
        private static readonly string[] QuestTypeIds = { "Hunt", "Extermination", "Escort", "Delivery", "CaveExploration" };

        private static void FillQuestTypes(Dictionary<string, QuestTypeDefinition> q)
        {
            QuestType(q["Hunt"], "Охота", 3, 0.3f, "hunt",
                new[] { StatId.Perception, StatId.Knowledge, StatId.Marksmanship }, StatId.Survival, StatId.Strength);
            QuestType(q["Extermination"], "Истребление", 2, 0.25f, "extermination",
                new[] { StatId.Strength, StatId.Endurance, StatId.Composure }, StatId.Reaction, StatId.Medicine);
            QuestType(q["Escort"], "Сопровождение", 6, 0.2f, "escort",
                new[] { StatId.Endurance, StatId.Perception, StatId.Reaction }, StatId.Survival, StatId.Charisma);
            QuestType(q["Delivery"], "Доставка", 6, 0.25f, "delivery",
                new[] { StatId.Survival, StatId.Agility, StatId.Endurance }, StatId.Stealth);
            // Новый тип (TechJob/11-quests.md, ❔): оси — как у пещеры, раунд 3 часа.
            QuestType(q["CaveExploration"], "Исследование пещеры", 3, 0f, "cave",
                new[] { StatId.Perception, StatId.Composure, StatId.Strength, StatId.Endurance });
        }

        private static void QuestType(QuestTypeDefinition asset, string name, int roundHours, float weight, string feedSuffix, StatId[] main, params StatId[] secondary)
        {
            Def(asset, asset.name, name);
            Set(asset, "mainAxes", main.ToList());
            Set(asset, "secondaryAxes", secondary.ToList());
            Set(asset, "roundHours", roundHours);
            Set(asset, "maxPartySizeCeiling", 0);
            Set(asset, "statCeilings", new List<StatCeiling>());
            Set(asset, "generationWeight", weight);
            Set(asset, "roundSuccessFeedKey", "quest.round.success." + feedSuffix);
            Set(asset, "roundFailFeedKey", "quest.round.fail." + feedSuffix);
        }

        // ---------- События в пути и находка ----------

        private static void FillEncounters(RandomEventDefinition ambush, RandomEventDefinition beasts, DiscoveryDefinition cave,
            Dictionary<string, QuestTypeDefinition> questTypes)
        {
            var travelFailure = new List<OutcomeChance>
            {
                Outcome(OutcomeKind.LightWound, 0.7f),
                Outcome(OutcomeKind.HeavyWound, 0.3f, turnsBack: true),
            };

            Encounter(ambush, "Засада разбойников", RankValueSource.SecondaryAxes, new IntRange(0, 0),
                new List<OutcomeChance>(), travelFailure,
                StatId.Reaction, StatId.Composure, StatId.Perception, StatId.Strength);
            Set(ambush, "enemies", Nouns(M, "разбойничий отряд", "главарь разбойников"));
            Set(ambush, "startFeedKey", "quest.event.ambush.start");
            Set(ambush, "successFeedKey", "quest.event.ambush.success");
            Set(ambush, "failFeedKey", "quest.event.ambush.fail");

            Encounter(beasts, "Нападение зверей", RankValueSource.SecondaryAxes, new IntRange(0, 0),
                new List<OutcomeChance>(), travelFailure.ToList(),
                StatId.Strength, StatId.Endurance, StatId.Reaction);
            Set(beasts, "enemies", Nouns(M, "голодный волк", "медведь-шатун", "секач"));
            Set(beasts, "startFeedKey", "quest.event.beasts.start");
            Set(beasts, "successFeedKey", "quest.event.beasts.success");
            Set(beasts, "failFeedKey", "quest.event.beasts.fail");

            Encounter(cave, "Пещера", RankValueSource.MainAxes, new IntRange(-1, 2),
                new List<OutcomeChance>
                {
                    Outcome(OutcomeKind.Nothing, 0.4f),
                    Outcome(OutcomeKind.Loot, 0.6f, loot: new FloatRange(0.5f, 1.5f)),
                },
                new List<OutcomeChance>
                {
                    Outcome(OutcomeKind.HeavyWound, 0.5f),
                    Outcome(OutcomeKind.Death, 0.2f),
                    Outcome(OutcomeKind.LightWound, 0.3f, count: 2),
                },
                StatId.Perception, StatId.Composure, StatId.Strength, StatId.Endurance);
            Set(cave, "spotterStat", StatId.Perception);
            Set(cave, "reportRewardShare", 0.1f);
            Set(cave, "eventQuestTitle", "Исследовать пещеру у {место:р}");
            Set(cave, "eventQuestType", questTypes["CaveExploration"]);
            Set(cave, "foundFeedKey", "quest.discovery.found");
            Set(cave, "exploreFeedKey", "quest.discovery.explore");
            Set(cave, "skipFeedKey", "quest.discovery.skip");
            Set(cave, "emptyFeedKey", "quest.discovery.empty");
            Set(cave, "lootFeedKey", "quest.discovery.loot");
            Set(cave, "failFeedKey", "quest.discovery.fail");
            Set(cave, "reportedFeedKey", "quest.discovery.reported");
        }

        private static void Encounter(EncounterDefinition asset, string name, RankValueSource source, IntRange rankOffset,
            List<OutcomeChance> success, List<OutcomeChance> failure, params StatId[] axes)
        {
            Def(asset, asset.name, name);
            Set(asset, "axes", axes.ToList());
            Set(asset, "valueSource", source);
            Set(asset, "rankOffset", rankOffset);
            Set(asset, "successOutcomes", success);
            Set(asset, "failureOutcomes", failure);
        }

        private static OutcomeChance Outcome(OutcomeKind kind, float chance, int count = 1, bool turnsBack = false, FloatRange loot = default) =>
            Make<OutcomeChance>(("kind", kind), ("chance", chance), ("count", count), ("turnsBack", turnsBack), ("lootShareOfReward", loot));

        // ---------- Постройки и персонал ----------

        private static readonly string[] BuildingIds = { "GuildHall", "Tavern", "Dormitory", "Infirmary", "TrainingYard" };
        private static readonly string[] StaffIds = { "Registrar", "Innkeeper", "Healer" };

        private static void FillBuildings(Dictionary<string, BuildingDefinition> b, Dictionary<string, StaffRoleDefinition> s)
        {
            Building(b["GuildHall"], "Зал гильдии", M, cost: 0, days: 0, upkeep: 50, capacity: 0, atStart: true, s["Registrar"]);
            Building(b["Tavern"], "Таверна", F, cost: 0, days: 0, upkeep: 40, capacity: 0, atStart: true, s["Innkeeper"]);
            Building(b["Dormitory"], "Общежитие", N, cost: 800, days: 10, upkeep: 40, capacity: 10, atStart: false, null);
            Building(b["Infirmary"], "Лазарет", M, cost: 1000, days: 14, upkeep: 60, capacity: 4, atStart: false, s["Healer"]);
            Building(b["TrainingYard"], "Тренировочный двор", M, cost: 600, days: 7, upkeep: 30, capacity: 6, atStart: false, null);
        }

        private static void Building(BuildingDefinition asset, string name, GrammaticalGender gender, int cost, int days, int upkeep, int capacity, bool atStart, StaffRoleDefinition role)
        {
            Def(asset, asset.name, name);
            Set(asset, "nameForms", new NounForms(name, gender));
            Set(asset, "cost", cost);
            Set(asset, "buildDays", days);
            Set(asset, "upkeepPerMonth", upkeep);
            Set(asset, "capacity", capacity);
            Set(asset, "builtAtStart", atStart);
            Set(asset, "staffRole", role);
            Set(asset, "levelCostMultiplier", 1.5f);
            Set(asset, "levelTimeMultiplier", 1.5f);
        }

        private static void FillStaff(Dictionary<string, StaffRoleDefinition> s, Dictionary<string, BuildingDefinition> b)
        {
            StaffRole(s["Registrar"], "Регистратор", 150, b["GuildHall"], StaffLevelEffect.None,
                "Нет эффекта в прототипе: работает по правилам игрока", atStart: true);
            StaffRole(s["Innkeeper"], "Трактирщик", 120, b["Tavern"], StaffLevelEffect.TavernStressRelief,
                "Снятие стресса в таверне × (0,8 + уровень / 250)", atStart: true);
            StaffRole(s["Healer"], "Лекарь гильдии", 250, b["Infirmary"], StaffLevelEffect.HealingSpeed,
                "Скорость лечения × (0,8 + уровень / 250); без Лекаря Лазарет не лечит", atStart: false);
        }

        private static void StaffRole(StaffRoleDefinition asset, string name, int salary, BuildingDefinition building, StaffLevelEffect effect, string description, bool atStart)
        {
            Def(asset, asset.name, name);
            Set(asset, "baseSalary", salary);
            Set(asset, "requiredBuilding", building);
            Set(asset, "levelEffect", effect);
            Set(asset, "levelEffectDescription", description);
            Set(asset, "hiredAtStart", atStart);
        }

        // ---------- Распоряжения ----------

        private static readonly string[] DecreeIds = { "FreeLodgingForNewcomers", "GroupOnlyFromRank", "InjuryCompensation", "Prohibition" };

        private static void FillDecrees(Dictionary<string, DecreeDefinition> d)
        {
            Decree(d["FreeLodgingForNewcomers"], 2, "Еда и жильё для новичков", P,
                "Гильдия платит за еду и жильё тех, кто в гильдии недавно.",
                "Приток людей из других поселений", "Постоянные расходы, халявщики",
                DecreeScopeKind.None, isBenefit: true);
            Decree(d["GroupOnlyFromRank"], 5, "Задания от ранга C — только группой", P,
                "Заказы выбранных рангов нельзя брать в одиночку.",
                "Меньше смертей", "Одиночки недовольны, выполняется меньше заданий",
                DecreeScopeKind.Ranks, isBenefit: false, GuildRank.C);
            Decree(d["InjuryCompensation"], 11, "Компенсация за ранение", F,
                "За каждую рану гильдия платит раненому: за лёгкую, тяжёлую и увечье — по-разному.",
                "Лояльность, охотнее рискуют", "Расходы",
                DecreeScopeKind.None, isBenefit: true);
            Decree(d["Prohibition"], 14, "Сухой закон", M,
                "В таверне гильдии не подают выпивку.",
                "Пьяницы не срывают задания", "Пьяницы уходят, таверна приносит меньше",
                DecreeScopeKind.None, isBenefit: false);
        }

        private static void Decree(DecreeDefinition asset, int law, string name, GrammaticalGender gender, string description, string plus, string minus,
            DecreeScopeKind scope, bool isBenefit, params GuildRank[] ranks)
        {
            Def(asset, asset.name, name);
            Set(asset, "lawNumber", law);
            Set(asset, "nameForms", new NounForms(name, gender));
            Set(asset, "description", description);
            Set(asset, "plusText", plus);
            Set(asset, "minusText", minus);
            Set(asset, "scopeKind", scope);
            Set(asset, "defaultRanks", ranks.ToList());
            Set(asset, "allowedDurations", new List<DecreeDuration> { DecreeDuration.Permanent, DecreeDuration.Week, DecreeDuration.Month });
            Set(asset, "isBenefit", isBenefit);
        }

        // ---------- Помощники ----------

        private const GrammaticalGender M = GrammaticalGender.Masculine;
        private const GrammaticalGender F = GrammaticalGender.Feminine;
        private const GrammaticalGender N = GrammaticalGender.Neuter;
        private const GrammaticalGender P = GrammaticalGender.Plural;

        /// <summary>Слова одного рода.</summary>
        private static List<NounForms> Nouns(GrammaticalGender gender, params string[] nominatives) =>
            nominatives.Select(n => new NounForms(n, gender)).ToList();

        /// <summary>Слова разного рода: пары «слово, род».</summary>
        private static List<NounForms> Nouns(params (string nominative, GrammaticalGender gender)[] nouns) =>
            nouns.Select(n => new NounForms(n.nominative, n.gender)).ToList();

        private static T Asset<T>(string folder, string name) where T : ScriptableObject
        {
            string directory = folder.Length == 0 ? Root : $"{Root}/{folder}";
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder(Root, folder);

            string path = $"{directory}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            Touched.Add(asset);
            return asset;
        }

        private static void Def(Definition asset, string id, string displayName)
        {
            Set(asset, "id", id);
            Set(asset, "displayName", displayName);
        }

        private static T Make<T>(params (string field, object value)[] fields) where T : new()
        {
            var instance = new T();
            foreach ((string field, object value) in fields) Set(instance, field, value);
            return instance;
        }

        private static void Set(object target, string field, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo info = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }
    }
}
