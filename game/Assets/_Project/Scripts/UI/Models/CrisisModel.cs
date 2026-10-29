using System.Collections.Generic;
using System.Globalization;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Проект поселения строкой вкладки «Поселение»: цена, срок, эффект, стадия; можно ли заказать.</summary>
    public sealed class ProjectRow
    {
        public string DefinitionId;
        public string Name;
        public int Cost;
        public int BuildDays;
        public string Effect;
        public string State;
        public bool Ready;

        /// <summary>Не построен и не в очереди — можно строить.</summary>
        public bool CanOrder;
    }

    /// <summary>
    /// Кризисы и проекты поселения для интерфейса: время года, ближайший кризис для верхней панели, текущие и объявленные кризисы
    /// с последствиями (вкладка «Поселение»), проекты поселения, эффект Арсенала, события календаря. Только чтение мира.
    /// </summary>
    public static class CrisisModel
    {
        public static string SeasonName(Season season)
        {
            switch (season)
            {
                case Season.Spring: return UiStrings.SeasonSpring;
                case Season.Summer: return UiStrings.SeasonSummer;
                case Season.Autumn: return UiStrings.SeasonAutumn;
                default: return UiStrings.SeasonWinter;
            }
        }

        public static string SeasonOf(ISimulationClient client) =>
            SeasonName(CrisisRules.SeasonOf(client.World.Time.Month, client.Calendar.MonthsPerYear));

        public static string KindName(CrisisKind kind)
        {
            switch (kind)
            {
                case CrisisKind.Raid: return UiStrings.CrisisRaid;
                case CrisisKind.Tax: return UiStrings.CrisisTax;
                case CrisisKind.Winter: return UiStrings.CrisisWinter;
                default: return UiStrings.CrisisPlague;
            }
        }

        /// <summary>
        /// Строка верхней панели: ближайший объявленный кризис («Налог через 12 дн.: ~1 400»); не объявлено — идущий («Мор»,
        /// «Зима: ещё 40 дн.»); нет ничего — пусто.
        /// </summary>
        public static string TopLine(ISimulationClient client)
        {
            WorldState world = client.World;
            long now = world.Time.TotalHours;
            Crisis next = world.Crises.GetNextWarned();
            if (next != null)
            {
                int days = UiFormat.DaysLeft(next.StartsAtHours, now, client.Calendar);
                return next.Kind == CrisisKind.Tax
                    ? string.Format(UiStrings.CrisisSoonTaxFormat, days, UiFormat.Money(next.Amount))
                    : string.Format(UiStrings.CrisisSoonFormat, KindName(next.Kind), days);
            }
            foreach (Crisis crisis in world.Crises.All)
            {
                if (crisis.IsActive) return Active(client, crisis);
            }
            return string.Empty;
        }

        /// <summary>Идущий кризис строкой: что идёт и до когда.</summary>
        private static string Active(ISimulationClient client, Crisis crisis)
        {
            int days = UiFormat.DaysLeft(crisis.EndsAtHours, client.World.Time.TotalHours, client.Calendar);
            return string.Format(UiStrings.CrisisActiveFormat, KindName(crisis.Kind), days);
        }

        /// <summary>
        /// Текущие и объявленные кризисы с последствиями — для вкладки «Поселение»: набег (заказ на доске, что будет, если не отбить),
        /// зима и мор (до когда и что меняют), объявленные налог, набег, зима.
        /// </summary>
        public static List<string> Lines(ISimulationClient client)
        {
            var lines = new List<string>();
            WorldState world = client.World;
            CrisesBalance crises = client.Data.Balance.Crises;
            long now = world.Time.TotalHours;
            foreach (Crisis crisis in world.Crises.All)
            {
                if (crisis.IsActive)
                {
                    int days = UiFormat.DaysLeft(crisis.EndsAtHours, now, client.Calendar);
                    switch (crisis.Kind)
                    {
                        case CrisisKind.Raid:
                            lines.Add(string.Format(UiStrings.CrisisRaidActiveFormat, days, UiFormat.Number(crises.RaidLostSecurity),
                                UiFormat.Percent(crises.RaidLostPopulationShare)));
                            break;
                        case CrisisKind.Winter:
                            lines.Add(string.Format(UiStrings.CrisisWinterActiveFormat, days, Multiplier(crises.WinterFoodPriceMultiplier),
                                UiFormat.Number(crises.WinterCityHousing), Multiplier(crises.WinterOrdersMultiplier), Multiplier(crises.WinterDecayMultiplier)));
                            break;
                        case CrisisKind.Plague:
                            lines.Add(string.Format(UiStrings.CrisisPlagueActiveFormat, days, crises.PlaguePopulationPerWeek, crises.PlagueWithHealerPerWeek));
                            break;
                    }
                }
                else if (crisis.Status == CrisisStatus.Warned)
                {
                    int days = UiFormat.DaysLeft(crisis.StartsAtHours, now, client.Calendar);
                    switch (crisis.Kind)
                    {
                        case CrisisKind.Raid:
                            lines.Add(string.Format(UiStrings.CrisisRaidSoonFormat, days, crises.RaidRankFor(crisis.Year)));
                            break;
                        case CrisisKind.Tax:
                            lines.Add(string.Format(UiStrings.CrisisTaxSoonFormat, days, UiFormat.Money(crisis.Amount), UiFormat.Number(crises.TaxUnpaidReputation),
                                UiFormat.Number(crises.TaxUnpaidSecurity)));
                            break;
                        case CrisisKind.Winter:
                            lines.Add(string.Format(UiStrings.CrisisWinterSoonFormat, days));
                            break;
                    }
                }
            }
            return lines;
        }

        /// <summary>Проекты поселения из данных: построенные, строящиеся, в очереди, не начатые.</summary>
        public static List<ProjectRow> Projects(ISimulationClient client)
        {
            var rows = new List<ProjectRow>();
            WorldState world = client.World;
            long now = world.Time.TotalHours;
            foreach (BuildingDefinition definition in client.Data.All<BuildingDefinition>())
            {
                if (!definition.SettlementProject) continue;
                var row = new ProjectRow
                {
                    DefinitionId = definition.Id,
                    Name = definition.DisplayName,
                    Cost = definition.Cost,
                    BuildDays = definition.BuildDays,
                    Effect = ProjectEffect(client.Data, definition.Function),
                };
                if (world.Buildings.TryGetByDefinition(definition.Id, out Building building))
                {
                    row.Ready = building.IsReady;
                    row.State = building.IsReady
                        ? UiText.Render(client.Data, UiTextKeys.BuildingReady)
                        : building.State == BuildingState.UnderConstruction
                            ? UiText.Render(client.Data, UiTextKeys.BuildingConstruction,
                                new UiTextSource().Set("число", UiFormat.DaysLeft(building.ConstructionEndsAtHours, now, client.Calendar)))
                            : UiText.Render(client.Data, UiTextKeys.BuildingQueued);
                }
                else
                {
                    row.State = UiText.Render(client.Data, UiTextKeys.BuildingNotBuilt);
                    row.CanOrder = true;
                }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>Эффект проекта поселения словами (числа — из баланса).</summary>
        public static string ProjectEffect(DataRegistry data, BuildingFunction function)
        {
            CrisesBalance crises = data.Balance.Crises;
            switch (function)
            {
                case BuildingFunction.Palisade:
                    return string.Format(UiStrings.ProjectPalisadeFormat, UiFormat.Number(crises.PalisadeAttackBelow), Multiplier(crises.PalisadeDamageMultiplier));
                case BuildingFunction.Watchtower:
                    return string.Format(UiStrings.ProjectWatchtowerFormat, crises.WatchtowerDecayReduction.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU")));
                case BuildingFunction.Well:
                    return string.Format(UiStrings.ProjectWellFormat, crises.WellPopulation, UiFormat.Percent(crises.WellGrowth));
                case BuildingFunction.Road:
                    return string.Format(UiStrings.ProjectRoadFormat, crises.RoadPopulation, crises.RoadOrdersPerDay.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU")));
                default:
                    return string.Empty;
            }
        }

        /// <summary>Эффект Арсенала уровня <paramref name="level"/>: «профиль на задании +10%».</summary>
        public static string ArmoryEffect(DataRegistry data, int level) =>
            string.Format(UiStrings.ArmoryEffectFormat, UiFormat.Percent(data.Balance.Crises.ArmoryProfilePerLevel * level));

        /// <summary>Множитель словами: «×1,5».</summary>
        public static string Multiplier(float value) => "×" + value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));

        /// <summary>Кризисы в календаре: объявленные — начало, идущие — конец (у набега — срок заказа на доске).</summary>
        public sealed class CalendarSource : ICalendarSource
        {
            public void Collect(ISimulationClient client, List<CalendarItem> items)
            {
                foreach (Crisis crisis in client.World.Crises.All)
                {
                    if (crisis.Status == CrisisStatus.Warned)
                    {
                        string text = crisis.Kind == CrisisKind.Tax
                            ? string.Format(UiStrings.CalendarTaxFormat, UiFormat.Money(crisis.Amount))
                            : KindName(crisis.Kind);
                        items.Add(new CalendarItem { AtHours = crisis.StartsAtHours, Text = text, Destination = Destination.ToGuild(GuildTab.Settlement) });
                    }
                    else if (crisis.IsActive)
                    {
                        bool raid = crisis.Kind == CrisisKind.Raid;
                        items.Add(new CalendarItem
                        {
                            AtHours = crisis.EndsAtHours,
                            Text = string.Format(raid ? UiStrings.CalendarRaidDueFormat : UiStrings.CalendarCrisisEndsFormat, KindName(crisis.Kind)),
                            Destination = raid ? Destination.ToScreen(ScreenId.Board, crisis.OrderId) : Destination.ToGuild(GuildTab.Settlement),
                        });
                    }
                }
            }
        }
    }
}
