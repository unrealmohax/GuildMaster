using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GuildMaster.Core;
using GuildMaster.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Проверка данных из <see cref="GameConfig"/>: пустые ссылки, дубли id, значения вне диапазонов,
    /// шаблоны текстов с неизвестными подстановками, связи между определениями.
    /// Меню — GuildMaster → Validate Data (<c>DataValidatorMenu</c>).
    /// </summary>
    public static class DataValidator
    {
        private static readonly Regex IdFormat = new Regex("^[A-Za-z][A-Za-z0-9_]*$");
        private const float ChanceSumTolerance = 0.001f;

        /// <summary>Сколько определений каждого вида в прототипе. Другое число — предупреждение.</summary>
        private static readonly Dictionary<Type, int> PrototypeCounts = new Dictionary<Type, int>
        {
            [typeof(AxisDefinition)] = 6,
            [typeof(SpecialTraitDefinition)] = 12,
            [typeof(ArchetypeDefinition)] = 7,
            [typeof(QuestTypeDefinition)] = 5,
            [typeof(RandomEventDefinition)] = 2,
            [typeof(DiscoveryDefinition)] = 1,
            [typeof(BuildingDefinition)] = 5,
            [typeof(StaffRoleDefinition)] = 3,
            [typeof(DecreeDefinition)] = 4,
            [typeof(DilemmaDefinition)] = 6,
        };

        public static DataValidationReport Validate(GameConfig config)
        {
            var report = new DataValidationReport();
            if (config == null)
            {
                report.Error(null, string.Empty, "нет GameConfig");
                return report;
            }

            new Run(config, report).Execute();
            return report;
        }

        private sealed class Run
        {
            private readonly GameConfig config;
            private readonly DataValidationReport report;
            private readonly HashSet<string> feedKeys = new HashSet<string>(StringComparer.Ordinal);

            public Run(GameConfig config, DataValidationReport report)
            {
                this.config = config;
                this.report = report;
            }

            public void Execute()
            {
                foreach (Object asset in Assets()) SerializedFieldCheck.Run(asset, report);

                if (config.FeedTemplates != null)
                {
                    foreach (FeedTemplate template in config.FeedTemplates.Templates)
                    {
                        if (template != null && !string.IsNullOrEmpty(template.Key)) feedKeys.Add(template.Key);
                    }
                }

                CheckIds();
                CheckBalance(config.Balance);
                CheckStatCatalog(config.StatCatalog);
                CheckAxes();
                foreach (SpecialTraitDefinition trait in Live(config.SpecialTraits)) CheckTrait(trait);
                CheckArchetypes();
                foreach (QuestTypeDefinition questType in Live(config.QuestTypes)) CheckQuestType(questType);
                foreach (RandomEventDefinition randomEvent in Live(config.RandomEvents)) CheckRandomEvent(randomEvent);
                foreach (DiscoveryDefinition discovery in Live(config.Discoveries)) CheckDiscovery(discovery);
                foreach (BuildingDefinition building in Live(config.Buildings)) CheckBuilding(building);
                CheckBuildingFunctions();
                foreach (StaffRoleDefinition role in Live(config.StaffRoles)) CheckStaffRole(role);
                CheckDecrees();
                CheckDilemmas();
                CheckFeed(config.FeedTemplates);
                CheckNames(config.NameList);
                CheckOrderTexts(config.OrderTexts);
            }

            private IEnumerable<Object> Assets()
            {
                var seen = new HashSet<Object>();
                var roots = new Object[] { config, config.Balance, config.StatCatalog, config.FeedTemplates, config.NameList, config.OrderTexts };
                foreach (Object asset in roots.Concat(config.AllDefinitions()))
                {
                    if (asset != null && seen.Add(asset)) yield return asset;
                }
            }

            // ---------- id ----------

            private void CheckIds()
            {
                var byType = new Dictionary<Type, Dictionary<string, Definition>>();
                foreach (Definition definition in config.AllDefinitions())
                {
                    if (definition == null) continue;
                    Type type = definition.GetType();
                    if (!byType.TryGetValue(type, out Dictionary<string, Definition> ids))
                    {
                        ids = new Dictionary<string, Definition>(StringComparer.Ordinal);
                        byType.Add(type, ids);
                    }

                    if (string.IsNullOrEmpty(definition.DisplayName)) report.Error(definition, "displayName", "нет названия для игрока");
                    if (string.IsNullOrEmpty(definition.Id))
                    {
                        report.Error(definition, "id", "нет id");
                        continue;
                    }
                    if (!IdFormat.IsMatch(definition.Id)) report.Error(definition, "id", $"id «{definition.Id}» — только латиница, цифры и _, с буквы");
                    if (ids.TryGetValue(definition.Id, out Definition other))
                        report.Error(definition, "id", $"дубль id «{definition.Id}» у {type.Name}: уже есть в {other.name}");
                    else
                        ids.Add(definition.Id, definition);
                }

                foreach (KeyValuePair<Type, int> expected in PrototypeCounts)
                {
                    int actual = byType.TryGetValue(expected.Key, out Dictionary<string, Definition> ids) ? ids.Count : 0;
                    if (actual != expected.Value)
                        report.Warning(config, expected.Key.Name, $"в прототипе {expected.Value}, в GameConfig {actual}");
                }
            }

            // ---------- BalanceSettings ----------

            private void CheckBalance(BalanceSettings balance)
            {
                if (balance == null) return;

                TimeBalance time = balance.Time;
                int[] hours = { time.StartHour, time.MorningHour, time.DayHour, time.EveningHour, time.NightHour, time.CampHour, time.LatestDepartureHour, balance.Dilemmas.CheckHour };
                if (hours.Any(h => h >= time.HoursPerDay)) report.Error(balance, "time", "час вне суток");
                if (!(time.MorningHour < time.DayHour && time.DayHour < time.EveningHour && time.EveningHour < time.NightHour))
                    report.Error(balance, "time", "фазы дня не по порядку: утро < день < вечер < ночь");
                if (!(time.MorningHour <= time.LatestDepartureHour && time.LatestDepartureHour < time.CampHour))
                    report.Error(balance, "time", "нужно: начало утра ≤ последний час выхода < начало ночлега");
                if (time.SpeedMultipliers.Count == 0 || time.SpeedMultipliers.Any(s => s < 1))
                    report.Error(balance, "time.speedMultipliers", "скорости — непустой список, каждая не меньше 1");

                IReadOnlyList<RankEntry> ranks = balance.Ranks.Ranks;
                if (ranks.Count != Vocabulary.RankCount)
                {
                    report.Error(balance, "ranks.ranks", $"нужно {Vocabulary.RankCount} строк (G–C), есть {ranks.Count}");
                }
                else
                {
                    for (int i = 0; i < ranks.Count; i++)
                    {
                        if (ranks[i] == null) continue;
                        if (ranks[i].Rank != (GuildRank)i) report.Error(balance, $"ranks.ranks[{i}]", $"ожидается ранг {(GuildRank)i}, стоит {ranks[i].Rank}");
                        if (i < ranks.Count - 1 && ranks[i].PointsToNext <= 0) report.Error(balance, $"ranks.ranks[{i}].pointsToNext", "нужно число очков для повышения");
                    }
                }

                IReadOnlyList<RankMixEntry> mix = balance.Orders.RankMix;
                if (mix.Count == 0) report.Error(balance, "orders.rankMix", "пусто");
                for (int i = 0; i < mix.Count; i++)
                {
                    RankMixEntry entry = mix[i];
                    if (entry == null) continue;
                    string path = $"orders.rankMix[{i}]";
                    if (i == 0 && entry.FromReputation != 0) report.Error(balance, path, "первая строка — с репутации 0");
                    if (i > 0 && mix[i - 1] != null && entry.FromReputation <= mix[i - 1].FromReputation) report.Error(balance, path, "строки — по возрастанию репутации");
                    if (entry.Weights.Count != Vocabulary.RankCount) report.Error(balance, path, $"нужно {Vocabulary.RankCount} весов (G–C)");
                    else if (entry.Weights.Any(w => w < 0f) || entry.Weights.Sum() <= 0f) report.Error(balance, path, "веса не отрицательные, сумма больше 0");
                }

                if (balance.Rounds.FailureLadder.Count != 5) report.Error(balance, "rounds.failureLadder", "нужно 5 ступеней");

                AdventurersBalance adventurers = balance.Adventurers;
                float roleWeights = adventurers.RoleMainWeight + adventurers.RoleSecondaryWeight + adventurers.RoleRestWeight;
                if (Mathf.Abs(roleWeights - 1f) > ChanceSumTolerance) report.Warning(balance, "adventurers", $"веса оценки роли в сумме {roleWeights}, ожидается 1");
                if (adventurers.NeutralThreshold >= adventurers.ExtremePoleThreshold) report.Error(balance, "adventurers", "порог нейтральной оси должен быть ниже порога крайнего полюса");
                if (adventurers.StartRankF.Max > adventurers.StartAdventurers) report.Error(balance, "adventurers.startRankF", "больше, чем стартовых людей");
                IReadOnlyList<GenerationLevel> levels = adventurers.Levels;
                if (levels.Count != 3 || levels.Where((l, i) => l == null || l.Level != (AdventurerLevel)i).Any())
                    report.Error(balance, "adventurers.levels", "нужно 3 строки по порядку: Weak, Medium, Strong");
                else if (levels.Sum(l => l.Weight) <= 0f)
                    report.Error(balance, "adventurers.levels", "у всех уровней шанс 0");

                IReadOnlyList<float> words = balance.State.LoyaltyWordThresholds;
                for (int i = 1; i < words.Count; i++)
                {
                    if (words[i] <= words[i - 1]) report.Error(balance, "state.loyaltyWordThresholds", "границы — по возрастанию");
                }

                EconomyBalance economy = balance.Economy;
                if (economy.DefaultCommission < economy.CommissionLimits.Min || economy.DefaultCommission > economy.CommissionLimits.Max)
                    report.Error(balance, "economy.defaultCommission", "вне пределов комиссии");
            }

            // ---------- Параметры, оси, черты, архетипы ----------

            private void CheckStatCatalog(StatCatalog catalog)
            {
                if (catalog == null) return;

                foreach (StatId stat in Enum.GetValues(typeof(StatId)))
                {
                    StatInfo[] entries = catalog.Stats.Where(s => s != null && s.Stat == stat).ToArray();
                    if (entries.Length != 1)
                    {
                        report.Error(catalog, "stats", $"{stat}: записей {entries.Length}, нужна одна");
                        continue;
                    }
                    if (string.IsNullOrEmpty(entries[0].DisplayName)) report.Error(catalog, "stats", $"{stat}: нет названия");
                    if (entries[0].IsSkill == Vocabulary.IsCharacteristic(stat)) report.Error(catalog, "stats", $"{stat}: неверно отмечено «навык»");
                }

                IReadOnlyList<StatId> radar = catalog.RadarOrder;
                if (radar.Count != Vocabulary.StatCount - 1 || radar.Distinct().Count() != radar.Count || radar.Contains(StatId.Cohesion))
                    report.Error(catalog, "radarOrder", "нужно 13 разных осей без Слаженности");
            }

            private void CheckAxes()
            {
                foreach (AxisId axis in Enum.GetValues(typeof(AxisId)))
                {
                    int count = Live(config.Axes).Count(a => a.Axis == axis);
                    if (count != 1) report.Error(config, "axes", $"ось {axis}: определений {count}, нужно одно");
                }

                foreach (AxisDefinition axis in Live(config.Axes))
                {
                    CheckPole(axis, "negativePole", axis.NegativePole);
                    CheckPole(axis, "positivePole", axis.PositivePole);
                    RequireFeedKey(axis, "balancedFeedKey", axis.BalancedFeedKey);
                }
            }

            private void CheckPole(AxisDefinition axis, string path, AxisPoleDefinition pole)
            {
                if (pole == null) return;
                RequireText(axis, path + ".name", pole.Name);
                RequireText(axis, path + ".nameFemale", pole.NameFemale);
                if (pole.RevealTrigger == RevealTrigger.None) report.Error(axis, path + ".revealTrigger", "нет триггера раскрытия");
                RequireFeedKey(axis, path + ".revealFeedKey", pole.RevealFeedKey);
                CheckEffects(axis, path + ".effects", pole.Effects, isAxis: true);
            }

            private void CheckTrait(SpecialTraitDefinition trait)
            {
                if (trait.IsAcquired != (trait.Category == TraitCategory.Acquired))
                    report.Error(trait, "isAcquired", "приобретённая черта ⇔ категория Acquired");
                if (trait.RequiresPartner && trait.Category != TraitCategory.Relation)
                    report.Warning(trait, "requiresPartner", "черта с партнёром вне категории «Отношения»");
                if (trait.RevealTrigger == RevealTrigger.None) report.Error(trait, "revealTrigger", "нет триггера раскрытия");
                RequireFeedKey(trait, "revealFeedKey", trait.RevealFeedKey);
                CheckEffects(trait, "effects", trait.Effects, isAxis: false);

                foreach (SpecialTraitDefinition other in Live(trait.IncompatibleWith))
                {
                    if (other == trait) report.Error(trait, "incompatibleWith", "черта несовместима сама с собой");
                    else if (!other.IncompatibleWith.Contains(trait))
                        report.Warning(trait, "incompatibleWith", $"несовместимость с {other.Id} не взаимна");
                }
            }

            private void CheckEffects(Object asset, string path, IReadOnlyList<TraitEffect> effects, bool isAxis)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    TraitEffect effect = effects[i];
                    if (effect == null) continue;
                    string at = $"{path}[{i}]";

                    switch (effect.Kind)
                    {
                        case EffectKind.MotiveWeight:
                            if (effect.Arrow == Arrow.None) report.Error(asset, at, "вес мотива без стрелки");
                            break;
                        case EffectKind.StateRate:
                            if (effect.Arrow == Arrow.None && effect.Value <= 0f) report.Error(asset, at, "скорость показателя: нужна стрелка или множитель > 0");
                            if (effect.Arrow != Arrow.None && effect.Value != 0f) report.Warning(asset, at, "заданы и стрелка, и множитель — действует стрелка");
                            break;
                        case EffectKind.RiskPerception:
                        case EffectKind.TensionModifier:
                            if (effect.Value == 0f) report.Warning(asset, at, "эффект с нулевым числом");
                            break;
                        case EffectKind.PayContentmentSensitivity:
                            if (effect.Value <= 0f) report.Error(asset, at, "множитель должен быть больше 0");
                            break;
                        case EffectKind.PartyPreference:
                            if (effect.PartyPreference == PartySizePreference.None) report.Error(asset, at, "не задано предпочтение");
                            break;
                        case EffectKind.ProfileMultiplier:
                            if (effect.Stats.Count == 0) report.Error(asset, at, "нет параметра");
                            if (effect.Value <= 0f) report.Error(asset, at, "множитель должен быть больше 0");
                            break;
                    }

                    if (effect.Condition == EffectCondition.StressAbove && effect.ConditionThreshold <= 0f)
                        report.Error(asset, at, "условие «стресс выше» без порога");
                    if (effect.Condition == EffectCondition.ExtremePole && !isAxis)
                        report.Error(asset, at, "условие «крайний полюс» — только у осей");
                }
            }

            private void CheckArchetypes()
            {
                ArchetypeDefinition[] archetypes = Live(config.Archetypes).ToArray();
                if (archetypes.Count(a => a.Kind == ArchetypeKind.JackOfAllTrades) != 1) report.Error(config, "archetypes", "нужен ровно один Мастер на все руки");
                if (archetypes.Count(a => a.Kind == ArchetypeKind.Novice) != 1) report.Error(config, "archetypes", "нужен ровно один Новичок");
                if (archetypes.Sum(a => a.GenerationWeight) <= 0f) report.Error(config, "archetypes", "у всех архетипов вес генерации 0");

                foreach (ArchetypeDefinition archetype in archetypes.Where(a => a.Kind == ArchetypeKind.Role))
                {
                    if (archetype.MainStats.Count != 2) report.Error(archetype, "mainStats", "у роли 2 основных параметра");
                    if (archetype.SecondaryStats.Count < 1 || archetype.SecondaryStats.Count > 2) report.Error(archetype, "secondaryStats", "у роли 1–2 вспомогательных");
                    if (archetype.MainStats.Intersect(archetype.SecondaryStats).Any()) report.Error(archetype, "secondaryStats", "параметр и основной, и вспомогательный");
                }
            }

            // ---------- Задания, события, находки ----------

            private void CheckQuestType(QuestTypeDefinition questType)
            {
                if (questType.MainAxes.Count == 0) report.Error(questType, "mainAxes", "нет главных осей");
                CheckRadarAxes(questType, "mainAxes", questType.MainAxes);
                CheckRadarAxes(questType, "secondaryAxes", questType.SecondaryAxes);
                if (questType.MainAxes.Intersect(questType.SecondaryAxes).Any()) report.Error(questType, "secondaryAxes", "ось и главная, и второстепенная");

                for (int i = 0; i < questType.StatCeilings.Count; i++)
                {
                    StatCeiling ceiling = questType.StatCeilings[i];
                    if (ceiling != null && ceiling.Stat == StatId.Cohesion) report.Error(questType, $"statCeilings[{i}]", "Слаженность — не ось");
                }

                RequireFeedKey(questType, "roundSuccessFeedKey", questType.RoundSuccessFeedKey);
                RequireFeedKey(questType, "roundFailFeedKey", questType.RoundFailFeedKey);
            }

            private void CheckEncounter(EncounterDefinition encounter)
            {
                if (encounter.Axes.Count == 0) report.Error(encounter, "axes", "нет осей профиля");
                CheckRadarAxes(encounter, "axes", encounter.Axes);
                CheckOutcomes(encounter, "successOutcomes", encounter.SuccessOutcomes, required: false);
                CheckOutcomes(encounter, "failureOutcomes", encounter.FailureOutcomes, required: true);
            }

            private void CheckOutcomes(Object asset, string path, IReadOnlyList<OutcomeChance> outcomes, bool required)
            {
                if (outcomes.Count == 0)
                {
                    if (required) report.Error(asset, path, "нет исходов");
                    return;
                }

                float sum = outcomes.Where(o => o != null).Sum(o => o.Chance);
                if (Mathf.Abs(sum - 1f) > ChanceSumTolerance) report.Error(asset, path, $"шансы исходов в сумме {sum}, нужно 1");
                for (int i = 0; i < outcomes.Count; i++)
                {
                    if (outcomes[i] != null && outcomes[i].Kind == OutcomeKind.Loot && outcomes[i].LootShareOfReward.Max <= 0f)
                        report.Error(asset, $"{path}[{i}]", "добыча без доли награды");
                }
            }

            private void CheckRandomEvent(RandomEventDefinition randomEvent)
            {
                CheckEncounter(randomEvent);
                if (randomEvent.Enemies.Count == 0) report.Error(randomEvent, "enemies", "нет противников для {враг}");
                CheckNouns(randomEvent, "enemies", randomEvent.Enemies);
                RequireFeedKey(randomEvent, "startFeedKey", randomEvent.StartFeedKey);
                RequireFeedKey(randomEvent, "successFeedKey", randomEvent.SuccessFeedKey);
                RequireFeedKey(randomEvent, "failFeedKey", randomEvent.FailFeedKey);
            }

            private void CheckDiscovery(DiscoveryDefinition discovery)
            {
                CheckEncounter(discovery);
                RequireText(discovery, "eventQuestTitle", discovery.EventQuestTitle);
                TextMarkupCheck.Run(discovery.EventQuestTitle, false, discovery, "eventQuestTitle", report);
                RequireFeedKey(discovery, "foundFeedKey", discovery.FoundFeedKey);
                RequireFeedKey(discovery, "exploreFeedKey", discovery.ExploreFeedKey);
                RequireFeedKey(discovery, "skipFeedKey", discovery.SkipFeedKey);
                RequireFeedKey(discovery, "emptyFeedKey", discovery.EmptyFeedKey);
                RequireFeedKey(discovery, "lootFeedKey", discovery.LootFeedKey);
                RequireFeedKey(discovery, "failFeedKey", discovery.FailFeedKey);
                RequireFeedKey(discovery, "reportedFeedKey", discovery.ReportedFeedKey);
            }

            private void CheckRadarAxes(Object asset, string path, IReadOnlyList<StatId> axes)
            {
                if (axes.Contains(StatId.Cohesion)) report.Error(asset, path, "Слаженность — множитель, не ось диаграммы");
                if (axes.Distinct().Count() != axes.Count) report.Error(asset, path, "ось повторяется");
            }

            // ---------- Постройки, персонал, распоряжения, дилеммы ----------

            private void CheckBuilding(BuildingDefinition building)
            {
                CheckNoun(building, "nameForms", building.NameForms);
                if (building.StaffRole != null && building.StaffRole.RequiredBuilding != building)
                    report.Error(building, "staffRole", $"{building.StaffRole.Id} работает в другой постройке");
                if (!building.BuiltAtStart && building.BuildDays == 0) report.Warning(building, "buildDays", "стройка за 0 дней");
            }

            /// <summary>Правила находят постройку по назначению: у каждого назначения — не больше одной постройки.</summary>
            private void CheckBuildingFunctions()
            {
                var seen = new HashSet<BuildingFunction>();
                foreach (BuildingDefinition building in Live(config.Buildings))
                {
                    if (building.Function == BuildingFunction.None) continue;
                    if (!seen.Add(building.Function)) report.Error(building, "function", $"назначение {building.Function} уже есть у другой постройки");
                }
                foreach (BuildingFunction function in new[] { BuildingFunction.Dormitory, BuildingFunction.Infirmary, BuildingFunction.TrainingYard })
                {
                    if (!seen.Contains(function)) report.Warning(config, "buildings", $"нет постройки с назначением {function}");
                }
            }

            private void CheckStaffRole(StaffRoleDefinition role)
            {
                if (role.RequiredBuilding != null && role.RequiredBuilding.StaffRole != role)
                    report.Error(role, "requiredBuilding", $"у постройки {role.RequiredBuilding.Id} не указана эта должность");
                if (role.BaseSalary <= 0) report.Warning(role, "baseSalary", "зарплата 0");
                if (role.HiredAtStart && role.RequiredBuilding != null && !role.RequiredBuilding.BuiltAtStart)
                    report.Error(role, "hiredAtStart", "на старте есть, а постройки на старте нет");
            }

            private void CheckDecrees()
            {
                var numbers = new HashSet<int>();
                foreach (DecreeDefinition decree in Live(config.Decrees))
                {
                    if (!numbers.Add(decree.LawNumber)) report.Error(decree, "lawNumber", $"номер закона {decree.LawNumber} повторяется");
                    CheckNoun(decree, "nameForms", decree.NameForms);
                    RequireText(decree, "description", decree.Description);
                    if (decree.AllowedDurations.Count == 0) report.Error(decree, "allowedDurations", "нет допустимых сроков");
                    if (decree.ScopeKind == DecreeScopeKind.Ranks && decree.DefaultRanks.Count == 0) report.Error(decree, "defaultRanks", "область по рангам без рангов по умолчанию");
                }
            }

            private void CheckDilemmas()
            {
                var triggers = new HashSet<DilemmaTrigger>();
                foreach (DilemmaDefinition dilemma in Live(config.Dilemmas))
                {
                    if (!triggers.Add(dilemma.Trigger)) report.Error(dilemma, "trigger", $"триггер {dilemma.Trigger} уже у другой дилеммы");
                    RequireText(dilemma, "bodyTemplate", dilemma.BodyTemplate);
                    TextMarkupCheck.Run(dilemma.BodyTemplate, true, dilemma, "bodyTemplate", report);

                    bool usesReason = dilemma.BodyTemplate != null && dilemma.BodyTemplate.Contains("{причина}");
                    if (usesReason && !dilemma.Reasons.Any(r => r != null && r.RequiresTrait == null))
                        report.Error(dilemma, "reasons", "в тексте {причина}, но нет нейтральной причины");
                    if (!usesReason && dilemma.Reasons.Count > 0) report.Warning(dilemma, "reasons", "причины есть, а {причина} в тексте нет");
                    for (int i = 0; i < dilemma.Reasons.Count; i++)
                    {
                        if (dilemma.Reasons[i] == null) continue;
                        RequireText(dilemma, $"reasons[{i}].text", dilemma.Reasons[i].Text);
                        TextMarkupCheck.Run(dilemma.Reasons[i].Text, true, dilemma, $"reasons[{i}].text", report);
                    }

                    CheckDilemmaEffects(dilemma, "arrivalEffects", dilemma.ArrivalEffects);
                    CheckDilemmaOptions(dilemma);
                }
            }

            private void CheckDilemmaOptions(DilemmaDefinition dilemma)
            {
                IReadOnlyList<DilemmaOption> options = dilemma.Options;
                int selectable = options.Count(o => o != null && o.PlayerSelectable);
                if (selectable < 2 || selectable > 3) report.Error(dilemma, "options", $"вариантов для игрока {selectable}, нужно 2–3");

                if (dilemma.TimeoutOption >= options.Count)
                {
                    report.Error(dilemma, "timeoutOption", "нет такого варианта");
                }
                else if (options[dilemma.TimeoutOption] != null && options[dilemma.TimeoutOption].PlayerSelectable && !options[dilemma.TimeoutOption].IsRefusal)
                {
                    report.Warning(dilemma, "timeoutOption", "без ответа срабатывает не отказ");
                }

                for (int i = 0; i < options.Count; i++)
                {
                    DilemmaOption option = options[i];
                    if (option == null) continue;
                    string path = $"options[{i}]";
                    if (!option.PlayerSelectable && i != dilemma.TimeoutOption)
                        report.Error(dilemma, path, "вариант скрыт от игрока, но не срабатывает без ответа — недостижим");
                    if (option.PlayerSelectable)
                    {
                        RequireText(dilemma, path + ".text", option.Text);
                        RequireText(dilemma, path + ".visibleConsequencesText", option.VisibleConsequencesText);
                    }
                    TextMarkupCheck.Run(option.Text, true, dilemma, path + ".text", report);
                    TextMarkupCheck.Run(option.VisibleConsequencesText, true, dilemma, path + ".visibleConsequencesText", report);
                    RequireFeedKey(dilemma, path + ".answerFeedKey", option.AnswerFeedKey);
                    CheckDilemmaEffects(dilemma, path + ".effects", option.Effects);
                }
            }

            private void CheckDilemmaEffects(DilemmaDefinition dilemma, string path, IReadOnlyList<DilemmaEffect> effects)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    DilemmaEffect effect = effects[i];
                    if (effect == null) continue;
                    if (effect.Kind == DilemmaEffectKind.RevealTrait && effect.Trait == null)
                        report.Error(dilemma, $"{path}[{i}]", "раскрытие черты без черты");
                }
            }

            // ---------- Тексты ----------

            private void CheckFeed(FeedTemplateSet feed)
            {
                if (feed == null) return;
                if (feed.Templates.Count == 0) report.Error(feed, "templates", "нет шаблонов");

                for (int i = 0; i < feed.Templates.Count; i++)
                {
                    FeedTemplate template = feed.Templates[i];
                    if (template == null) continue;
                    string path = $"templates[{i}] «{template.Key}»";
                    if (string.IsNullOrWhiteSpace(template.Key)) report.Error(feed, path, "нет ключа");
                    if (template.Variants.Count == 0) report.Error(feed, path, "нет вариантов строки");
                    bool isReason = LeaveReasons.Keys.Contains(template.Key) || QuestReasons.Keys.Contains(template.Key) || PartyReasons.Keys.Contains(template.Key); // текст встаёт в чужую строку, {имя} — там
                    for (int v = 0; v < template.Variants.Count; v++)
                    {
                        TextMarkupCheck.Run(template.Variants[v], false, feed, $"{path} variants[{v}]", report, isReason);
                    }
                    CheckConditions(feed, path, template.Conditions);
                }

                foreach (string key in FeedKeys.Fixed)
                {
                    if (!feedKeys.Contains(key)) report.Error(feed, "templates", $"нет шаблона «{key}»: его строку даёт событие симуляции");
                }
                foreach (string key in LeaveReasons.Keys)
                {
                    if (!feedKeys.Contains(key)) report.Error(feed, "templates", $"нет шаблона «{key}»: это текст причины ухода");
                }
                foreach (string key in QuestReasons.Keys)
                {
                    if (!feedKeys.Contains(key)) report.Error(feed, "templates", $"нет шаблона «{key}»: это текст причины решения на задании");
                }
                foreach (string key in PartyReasons.Keys)
                {
                    if (!feedKeys.Contains(key)) report.Error(feed, "templates", $"нет шаблона «{key}»: это текст причины отказа идти с группой");
                }
                foreach (string key in MonthReportSections.TextKeys)
                {
                    if (!feedKeys.Contains(key)) report.Error(feed, "templates", $"нет шаблона «{key}»: это текст отчёта месяца");
                }
                foreach (string key in GuildMaster.UI.UiTextKeys.All)
                {
                    if (!feedKeys.Contains(key)) report.Error(feed, "templates", $"нет шаблона «{key}»: это строка интерфейса");
                }
            }

            private void CheckConditions(Object asset, string path, IReadOnlyList<FeedCondition> conditions)
            {
                var kinds = new HashSet<FeedConditionKind>();
                foreach (FeedCondition condition in conditions.Where(c => c != null))
                {
                    kinds.Add(condition.Kind);
                    switch (condition.Kind)
                    {
                        case FeedConditionKind.Archetype when condition.Archetype == null:
                            report.Error(asset, path, "условие «архетип» без архетипа");
                            break;
                        case FeedConditionKind.QuestType when condition.QuestType == null:
                            report.Error(asset, path, "условие «тип задания» без типа");
                            break;
                        case FeedConditionKind.RevealedTrait when condition.Trait == null:
                            report.Error(asset, path, "условие «черта раскрыта» без черты");
                            break;
                    }
                }
                if (kinds.Contains(FeedConditionKind.Solo) && kinds.Contains(FeedConditionKind.Group)) report.Error(asset, path, "условия «соло» и «группа» вместе");
                if (kinds.Contains(FeedConditionKind.Far) && kinds.Contains(FeedConditionKind.Near)) report.Error(asset, path, "условия «далеко» и «близко» вместе");
            }

            private void CheckNames(NameList names)
            {
                if (names == null) return;
                CheckNameList(names, "maleNames", names.MaleNames);
                CheckNameList(names, "femaleNames", names.FemaleNames);
                if (names.GroupAdjectives.Count == 0) report.Error(names, "groupAdjectives", "пусто");
                if (names.GroupNouns.Count == 0) report.Error(names, "groupNouns", "пусто");
            }

            private void CheckNameList(NameList names, string path, IReadOnlyList<NounForms> list)
            {
                const int minNames = 60;
                if (list.Count < minNames) report.Warning(names, path, $"имён {list.Count}, нужно {minNames}+");
                CheckNouns(names, path, list);
                CheckAllCases(names, path, list);
                foreach (IGrouping<string, NounForms> duplicate in list.Where(n => n != null).GroupBy(n => n.Nominative).Where(g => g.Count() > 1))
                {
                    report.Warning(names, path, $"имя «{duplicate.Key}» повторяется");
                }
            }

            private void CheckOrderTexts(OrderTextTemplates texts)
            {
                if (texts == null) return;

                if (texts.Clients.Count == 0) report.Error(texts, "clients", "нет заказчиков");
                CheckNouns(texts, "clients", texts.Clients);
                CheckAllCases(texts, "clients", texts.Clients);
                RequireText(texts, "farHint", texts.FarHint);

                foreach (QuestTypeDefinition questType in Live(config.QuestTypes))
                {
                    int count = texts.QuestTypes.Count(t => t != null && t.QuestType == questType);
                    if (count != 1) report.Error(texts, "questTypes", $"тексты для {questType.Id}: {count}, нужно одни");
                }

                for (int i = 0; i < texts.QuestTypes.Count; i++)
                {
                    QuestTypeTexts typeTexts = texts.QuestTypes[i];
                    if (typeTexts == null) continue;
                    string path = $"questTypes[{i}]";
                    if (typeTexts.Places.Count == 0) report.Error(texts, path + ".places", "нет мест");
                    if (typeTexts.Enemies.Count == 0) report.Error(texts, path + ".enemies", "нет противников");
                    if (typeTexts.Descriptions.Count == 0) report.Error(texts, path + ".descriptions", "нет описаний");
                    CheckNouns(texts, path + ".places", typeTexts.Places);
                    CheckNouns(texts, path + ".enemies", typeTexts.Enemies);
                    CheckNouns(texts, path + ".cargo", typeTexts.Cargo);
                    CheckAllCases(texts, path + ".places", typeTexts.Places);
                    CheckAllCases(texts, path + ".enemies", typeTexts.Enemies);
                    CheckAllCases(texts, path + ".cargo", typeTexts.Cargo);
                    for (int d = 0; d < typeTexts.Descriptions.Count; d++)
                    {
                        TextMarkupCheck.Run(typeTexts.Descriptions[d], false, texts, $"{path}.descriptions[{d}]", report);
                        if (typeTexts.Descriptions[d] != null && typeTexts.Descriptions[d].Contains("{груз") && typeTexts.Cargo.Count == 0)
                            report.Error(texts, $"{path}.descriptions[{d}]", "в описании {груз}, а грузов нет");
                    }
                }

                IEnumerable<StatId> radar = config.StatCatalog != null ? config.StatCatalog.RadarOrder : Enumerable.Empty<StatId>();
                foreach (StatId stat in radar)
                {
                    if (!texts.Hints.Any(h => h != null && h.Stat == stat && h.Lines.Count > 0))
                        report.Warning(texts, "hints", $"нет намёков на ось {stat}");
                }
                for (int i = 0; i < texts.Hints.Count; i++)
                {
                    if (texts.Hints[i] == null) continue;
                    for (int l = 0; l < texts.Hints[i].Lines.Count; l++)
                    {
                        TextMarkupCheck.Run(texts.Hints[i].Lines[l], false, texts, $"hints[{i}].lines[{l}]", report);
                    }
                }
            }

            // ---------- Помощники ----------

            private void CheckNouns(Object asset, string path, IReadOnlyList<NounForms> nouns)
            {
                for (int i = 0; i < nouns.Count; i++)
                {
                    CheckNoun(asset, $"{path}[{i}]", nouns[i]);
                }
            }

            /// <summary>У каждого слова заполнены все шесть падежей: их подставляет лента.</summary>
            private void CheckAllCases(Object asset, string path, IReadOnlyList<NounForms> nouns)
            {
                for (int i = 0; i < nouns.Count; i++)
                {
                    NounForms noun = nouns[i];
                    if (noun == null || string.IsNullOrWhiteSpace(noun.Nominative)) continue;
                    foreach (GrammaticalCase grammaticalCase in Enum.GetValues(typeof(GrammaticalCase)))
                    {
                        if (string.IsNullOrWhiteSpace(noun.Get(grammaticalCase)))
                            report.Error(asset, $"{path}[{i}]", $"«{noun.Nominative}»: нет формы {grammaticalCase}");
                    }
                }
            }

            private void CheckNoun(Object asset, string path, NounForms noun)
            {
                if (noun == null || string.IsNullOrWhiteSpace(noun.Nominative)) report.Error(asset, path, "нет именительного падежа");
                else if (noun.Gender == GrammaticalGender.Unspecified) report.Error(asset, path, $"«{noun.Nominative}»: не задан род");
            }

            private void RequireText(Object asset, string path, string text)
            {
                if (string.IsNullOrWhiteSpace(text)) report.Error(asset, path, "пустой текст");
            }

            private void RequireFeedKey(Object asset, string path, string key)
            {
                if (string.IsNullOrWhiteSpace(key)) report.Error(asset, path, "нет ключа ленты");
                else if (config.FeedTemplates != null && !feedKeys.Contains(key)) report.Error(asset, path, $"ключа ленты «{key}» нет в FeedTemplateSet");
            }

            /// <summary>Непустые элементы списка: пустые ссылки уже отмечены обходом полей.</summary>
            private static IEnumerable<T> Live<T>(IEnumerable<T> items) where T : Object => items.Where(i => i != null);
        }
    }
}
