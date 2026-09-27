using System;
using System.Collections.Generic;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Столбцы сводки прогона. Новый показатель — строка в <see cref="Monthly"/> или <see cref="People"/>:
    /// имя и функция от месяца или человека. Столбцы статей журнала казны строятся по списку статей
    /// (<see cref="LedgerCategories.All"/>) — <see cref="MonthlyFor"/>.
    /// </summary>
    public static class SummaryColumns
    {
        /// <summary>По месяцам: люди, приход и уход, срывы, раны, раскрытия, средние показатели на конец месяца, кошельки,
        /// строки ленты гильдии по важности, казна на конец месяца, доходы и расходы, банкротство, заказы, репутация на конец месяца.</summary>
        public static readonly IReadOnlyList<MonthColumn> Monthly = new[]
        {
            new MonthColumn("Людей", m => m.World.Adventurers.Active.Count, SummaryTotal.Last, "0"),
            new MonthColumn("Кандидатов", m => m.Count(SimEventType.CandidateArrived), SummaryTotal.Sum, "0"),
            new MonthColumn("Пришло", m => m.Count(SimEventType.AdventurerJoined), SummaryTotal.Sum, "0"),
            new MonthColumn("Ушло", m => m.Count(SimEventType.AdventurerLeft), SummaryTotal.Sum, "0"),
            new MonthColumn("Срывов", m => m.Count(SimEventType.Breakdown), SummaryTotal.Sum, "0"),
            new MonthColumn("Ран", m => m.Count(SimEventType.AdventurerWounded), SummaryTotal.Sum, "0"),
            new MonthColumn("Раскрыто осей", m => m.Count(SimEventType.AxisRevealed) + m.Count(SimEventType.AxisBalanced), SummaryTotal.Sum, "0"),
            new MonthColumn("Раскрыто черт", m => m.Count(SimEventType.TraitRevealed), SummaryTotal.Sum, "0"),
            new MonthColumn("Усталость", m => Average(m, a => a.State.Fatigue), SummaryTotal.Mean),
            new MonthColumn("Стресс", m => Average(m, a => a.State.Stress), SummaryTotal.Mean),
            new MonthColumn("Довольство", m => Average(m, a => a.State.Contentment), SummaryTotal.Mean),
            new MonthColumn("Лояльность", m => Average(m, a => a.State.Loyalty), SummaryTotal.Mean),
            new MonthColumn("Кошелёк", m => Average(m, a => a.State.Wallet), SummaryTotal.Mean),
            new MonthColumn("Пустых кошельков", m => CountActive(m, a => a.State.IsWalletEmpty), SummaryTotal.Last, "0"),
            new MonthColumn("Отдых, %", m => m.ActivityShare(Activity.Resting), SummaryTotal.Mean, "0.#"),
            new MonthColumn("Таверна, %", m => m.ActivityShare(Activity.Tavern), SummaryTotal.Mean, "0.#"),
            new MonthColumn("Лента [О]", m => m.FeedLines(EventImportance.Normal), SummaryTotal.Sum, "0"),
            new MonthColumn("Лента [З]", m => m.FeedLines(EventImportance.Notable), SummaryTotal.Sum, "0"),
            new MonthColumn("Лента [В]", m => m.FeedLines(EventImportance.Important), SummaryTotal.Sum, "0"),
            new MonthColumn("Казна", m => m.World.Treasury.Money, SummaryTotal.Last, "0"),
            new MonthColumn("Доходы", m => m.Ledger(LedgerFlow.Income), SummaryTotal.Sum, "0"),
            new MonthColumn("Расходы", m => -m.Ledger(LedgerFlow.Expense), SummaryTotal.Sum, "0"),
            new MonthColumn("Банкротство", m => m.World.Treasury.Bankruptcy.Active ? 1 : 0, SummaryTotal.Last, "0"),
            new MonthColumn("Заказов пришло", m => m.Count(SimEventType.OrderArrived), SummaryTotal.Sum, "0"),
            new MonthColumn("На доску", m => m.Count(SimEventType.OrderPosted), SummaryTotal.Sum, "0"),
            new MonthColumn("Отклонено Регистратором", m => m.Count(SimEventType.OrderDeclinedByRegistrar), SummaryTotal.Sum, "0"),
            new MonthColumn("Отклонено игроком", m => m.Count(SimEventType.OrderDeclinedByPlayer), SummaryTotal.Sum, "0"),
            new MonthColumn("Снято по сроку", m => m.Count(SimEventType.OrderExpired), SummaryTotal.Sum, "0"),
            new MonthColumn("Репутация", m => m.World.Guild.Reputation, SummaryTotal.Last, "0.#"),
        };

        /// <summary>
        /// Столбцы месяцев с доходами и расходами по каждой статье журнала: «Доход: Таверна», «Расход: …» (названия статей —
        /// из шаблонов данных; без данных — код статьи).
        /// </summary>
        public static IReadOnlyList<MonthColumn> MonthlyFor(DataRegistry data)
        {
            var columns = new List<MonthColumn>(Monthly);
            foreach (LedgerCategory category in LedgerCategories.All)
            {
                string name = MonthReportText.Label(data, category.TextKey);
                if (category.Flow == LedgerFlow.Income)
                    columns.Add(new MonthColumn("Доход: " + name, m => m.Ledger(LedgerFlow.Income, category), SummaryTotal.Sum, "0"));
                else
                    columns.Add(new MonthColumn("Расход: " + name, m => -m.Ledger(LedgerFlow.Expense, category), SummaryTotal.Sum, "0"));
            }
            return columns;
        }

        /// <summary>По людям: архетип, ранг, раны, в гильдии ли на конец, какие черты раскрылись и когда.</summary>
        public static readonly IReadOnlyList<PersonColumn> People = new[]
        {
            new PersonColumn("Id", p => p.Adventurer.Id.ToString()),
            new PersonColumn("Имя", p => p.Adventurer.Name),
            new PersonColumn("Архетип", p => ArchetypeName(p)),
            new PersonColumn("Ранг", p => p.Adventurer.GuildRank.ToString()),
            new PersonColumn("Ран", p => p.Count(SimEventType.AdventurerWounded).ToString()),
            new PersonColumn("В гильдии", p => InGuild(p)),
            new PersonColumn("Раскрыто", p => Reveals(p)),
        };

        /// <summary>Среднее по людям в гильдии; никого нет — 0.</summary>
        public static double Average(MonthRecord month, Func<Adventurer, double> value)
        {
            IReadOnlyList<Adventurer> active = month.World.Adventurers.Active;
            if (active.Count == 0) return 0;
            double sum = 0;
            foreach (Adventurer adventurer in active) sum += value(adventurer);
            return sum / active.Count;
        }

        public static int CountActive(MonthRecord month, Func<Adventurer, bool> condition)
        {
            int count = 0;
            foreach (Adventurer adventurer in month.World.Adventurers.Active)
            {
                if (condition(adventurer)) count++;
            }
            return count;
        }

        private static string ArchetypeName(PersonRecord person) =>
            person.Game.Data.TryGet(person.Adventurer.ArchetypeId, out ArchetypeDefinition archetype) ? archetype.DisplayName : person.Adventurer.ArchetypeId;

        private static string InGuild(PersonRecord person)
        {
            Adventurer adventurer = person.Adventurer;
            if (adventurer.LeaveReason == LeaveReason.None) return "да";
            return "нет: " + adventurer.LeaveReason + " " + person.Game.Calendar.At(adventurer.LeftAtHours);
        }

        /// <summary>Раскрытия по порядку: «Трус 1.2.3 14:00; Пьяница 1.4.1 07:00; Деньги — уравновешен 1.3.1 00:00».</summary>
        private static string Reveals(PersonRecord person)
        {
            var text = new StringBuilder();
            DataRegistry data = person.Game.Data;
            bool female = person.Adventurer.Gender == Gender.Female;
            foreach (SimEvent simEvent in person.Events)
            {
                string name;
                switch (simEvent.Type)
                {
                    case SimEventType.AxisRevealed:
                        simEvent.TryGet("axis", out AxisId axis);
                        simEvent.TryGet("pole", out AxisPole pole);
                        AxisPoleDefinition poleDefinition = data.Axis(axis).Pole(pole);
                        name = female && !string.IsNullOrEmpty(poleDefinition.NameFemale) ? poleDefinition.NameFemale : poleDefinition.Name;
                        break;
                    case SimEventType.AxisBalanced:
                        simEvent.TryGet("axis", out AxisId balanced);
                        name = data.Axis(balanced).DisplayName + " — уравновешен" + (female ? "а" : string.Empty);
                        break;
                    case SimEventType.TraitRevealed:
                        simEvent.TryGet("trait", out string traitId);
                        name = data.TryGet(traitId, out SpecialTraitDefinition trait) ? trait.DisplayName : traitId;
                        break;
                    default:
                        continue;
                }

                if (text.Length > 0) text.Append("; ");
                text.Append(name).Append(' ').Append(person.Game.Calendar.At(simEvent.TimeHours));
            }
            return text.ToString();
        }
    }
}
