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
        /// строки ленты гильдии по важности, казна на конец месяца, доходы и расходы, банкротство, заказы, репутация на конец месяца,
        /// задания и группы, постройки (готовые, Общежитие, Лазарет, двор), персонал и долги по зарплате.</summary>
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
            new MonthColumn("Заказов взято", m => m.Count(SimEventType.OrderTaken, e => !IsPromotion(e)), SummaryTotal.Sum, "0"),
            new MonthColumn("Выполнено", m => Quests(m, e => e.TryGet("done", out bool done) && done), SummaryTotal.Sum, "0"),
            new MonthColumn("Не выполнено", m => Quests(m, e => e.TryGet("done", out bool done) && !done), SummaryTotal.Sum, "0"),
            new MonthColumn("Блестяще", m => Quests(m, e => IsResult(e, "brilliant")), SummaryTotal.Sum, "0"),
            new MonthColumn("Успех", m => Quests(m, e => IsResult(e, "success")), SummaryTotal.Sum, "0"),
            new MonthColumn("Частично", m => Quests(m, e => IsResult(e, "partial")), SummaryTotal.Sum, "0"),
            new MonthColumn("Провал", m => Quests(m, e => IsResult(e, "fail")), SummaryTotal.Sum, "0"),
            new MonthColumn("Катастрофа", m => Quests(m, e => IsResult(e, "catastrophe")), SummaryTotal.Sum, "0"),
            new MonthColumn("Гибелей", m => m.Count(SimEventType.AdventurerDied), SummaryTotal.Sum, "0"),
            new MonthColumn("Бегств", m => m.Count(SimEventType.AdventurerFled), SummaryTotal.Sum, "0"),
            new MonthColumn("Раскрытий на заданиях", m => m.Count(SimEventType.AxisRevealed, OnQuest) + m.Count(SimEventType.TraitRevealed, OnQuest),
                SummaryTotal.Sum, "0"),
            new MonthColumn("Шанс раунда", RoundChance, SummaryTotal.Mean),
            new MonthColumn("Экзаменов сдано", m => m.Count(SimEventType.PromotionExam, e => e.TryGet("passed", out bool passed) && passed), SummaryTotal.Sum, "0"),
            new MonthColumn("Заданий соло", m => Quests(m, e => Departed(e) == 1), SummaryTotal.Sum, "0"),
            new MonthColumn("Заданий в группе", m => Quests(m, e => Departed(e) > 1), SummaryTotal.Sum, "0"),
            new MonthColumn("Средний размер группы", MeanPartySize, SummaryTotal.Mean),
            new MonthColumn("Отказов идти с группой", m => m.Count(SimEventType.InvitationDeclined), SummaryTotal.Sum, "0"),
            new MonthColumn("Постоянных групп", m => m.World.Parties.GetPermanentCount(), SummaryTotal.Last, "0"),
            new MonthColumn("Групп сложилось", m => m.Count(SimEventType.PermanentPartyFormed), SummaryTotal.Sum, "0"),
            new MonthColumn("Групп распалось", m => m.Count(SimEventType.PermanentPartyDisbanded), SummaryTotal.Sum, "0"),
            new MonthColumn("Построек готово", m => ReadyBuildings(m.World), SummaryTotal.Last, "0"),
            new MonthColumn("Построено", m => m.Count(SimEventType.BuildingReady), SummaryTotal.Sum, "0"),
            new MonthColumn("В Общежитии", m => BuildingRules.DormitoryResidents(m.World), SummaryTotal.Last, "0"),
            new MonthColumn("В Лазарете, %", m => m.ActivityShare(Activity.Infirmary), SummaryTotal.Mean, "0.#"),
            new MonthColumn("Легли в Лазарет", m => m.Count(SimEventType.InfirmaryAdmitted), SummaryTotal.Sum, "0"),
            new MonthColumn("Вылечено в Лазарете", m => m.Count(SimEventType.WoundHealed, e => e.TryGet("infirmary", out bool bed) && bed), SummaryTotal.Sum, "0"),
            new MonthColumn("На дворе, %", m => m.ActivityShare(Activity.Training), SummaryTotal.Mean, "0.#"),
            new MonthColumn("Тренировок", m => m.Count(SimEventType.TrainingStarted), SummaryTotal.Sum, "0"),
            new MonthColumn("Персонал", m => m.World.Staff.Members.Count, SummaryTotal.Last, "0"),
            new MonthColumn("Нанято", m => m.Count(SimEventType.StaffHired), SummaryTotal.Sum, "0"),
            new MonthColumn("Персонал ушёл", m => m.Count(SimEventType.StaffQuit) + m.Count(SimEventType.StaffDismissed), SummaryTotal.Sum, "0"),
            new MonthColumn("Невыплат жалованья", m => m.Count(SimEventType.SalaryUnpaid), SummaryTotal.Sum, "0"),
            new MonthColumn("Долг по зарплате", m => UnpaidSalaries(m.World), SummaryTotal.Last, "0"),
        };

        private static int ReadyBuildings(WorldState world)
        {
            int count = 0;
            foreach (Building building in world.Buildings.All)
            {
                if (building.IsReady) count++;
            }
            return count;
        }

        private static int UnpaidSalaries(WorldState world)
        {
            int total = 0;
            foreach (StaffMember member in world.Staff.Members) total += member.UnpaidSalary;
            return total;
        }

        private static bool IsPromotion(SimEvent simEvent) => simEvent.TryGet("promotion", out bool promotion) && promotion;

        /// <summary>Законченные задания месяца (без экзаменов), подходящие под условие.</summary>
        private static int Quests(MonthRecord month, Func<SimEvent, bool> condition) =>
            month.Count(SimEventType.QuestReturned, e => !IsPromotion(e) && condition(e));

        private static bool IsResult(SimEvent simEvent, string kind) => simEvent.TryGet("kind", out string value) && value == kind;

        /// <summary>Раскрытие случилось на задании (его опубликовала система заданий).</summary>
        private static bool OnQuest(SimEvent simEvent) => simEvent.Source == nameof(QuestSystem);

        /// <summary>Сколько человек вышло на законченное задание.</summary>
        private static int Departed(SimEvent simEvent) => simEvent.TryGet("total", out int total) ? total : 0;

        /// <summary>Средний размер группы на законченных за месяц групповых заданиях; таких не было — 0.</summary>
        private static string PermanentParty(PersonRecord person) =>
            person.Adventurer.PermanentPartyId != 0 && person.Game.World.Parties.TryGetParty(person.Adventurer.PermanentPartyId, out Party party) ? party.Name : string.Empty;

        private static double MeanPartySize(MonthRecord month)
        {
            double sum = 0;
            int count = 0;
            foreach (SimEvent simEvent in month.Events)
            {
                if (simEvent.Type != SimEventType.QuestReturned || IsPromotion(simEvent) || Departed(simEvent) < 2) continue;
                sum += Departed(simEvent);
                count++;
            }
            return count > 0 ? sum / count : 0;
        }

        /// <summary>Средний шанс раундов заданий за месяц; раундов не было — 0.</summary>
        private static double RoundChance(MonthRecord month)
        {
            double sum = 0;
            int count = 0;
            foreach (SimEvent simEvent in month.Events)
            {
                if ((simEvent.Type != SimEventType.RoundSuccess && simEvent.Type != SimEventType.RoundFail) || !simEvent.TryGet("chance", out float chance)) continue;
                sum += chance;
                count++;
            }
            return count > 0 ? sum / count : 0;
        }

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

        /// <summary>По людям: архетип, ранг, раны, задания выполнено / нет, жив ли, в гильдии ли на конец, какие черты раскрылись и когда.</summary>
        public static readonly IReadOnlyList<PersonColumn> People = new[]
        {
            new PersonColumn("Id", p => p.Adventurer.Id.ToString()),
            new PersonColumn("Имя", p => p.Adventurer.Name),
            new PersonColumn("Архетип", p => ArchetypeName(p)),
            new PersonColumn("Ранг", p => p.Adventurer.GuildRank.ToString()),
            new PersonColumn("Ран", p => p.Count(SimEventType.AdventurerWounded).ToString()),
            new PersonColumn("Выполнено", p => p.Adventurer.QuestsCompleted.ToString()),
            new PersonColumn("Не выполнено", p => p.Adventurer.QuestsFailed.ToString()),
            new PersonColumn("Жив", p => p.Adventurer.LeaveReason == LeaveReason.Died ? "нет" : "да"),
            new PersonColumn("Постоянная группа", p => PermanentParty(p)),
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
