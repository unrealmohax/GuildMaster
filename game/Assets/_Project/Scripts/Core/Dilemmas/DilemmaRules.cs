using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Правила обращений без изменения мира: какая дилемма у триггера, сколько стоит вариант, доступен ли он, суммы просьбы
    /// в долг и оплаты лечения. Ими пользуются служба, система, интерфейс и бот.
    /// </summary>
    public static class DilemmaRules
    {
        /// <summary>Дилемма с этим триггером; нет — <c>null</c>.</summary>
        public static DilemmaDefinition Find(DataRegistry data, DilemmaTrigger trigger)
        {
            if (!data.HasDefinitions) return null;
            foreach (DilemmaDefinition dilemma in data.All<DilemmaDefinition>())
            {
                if (dilemma.Trigger == trigger) return dilemma;
            }
            return null;
        }

        /// <summary>Перезарядка дилеммы в днях: своя или общая по умолчанию.</summary>
        public static int CooldownDays(DilemmaDefinition dilemma, DilemmasBalance balance) =>
            dilemma.CooldownDays > 0 ? dilemma.CooldownDays : balance.DefaultCooldownDays;

        /// <summary>Сумма просьбы в долг: расходы на жизнь (обычная еда и жильё) за <c>loanSumDays</c> дней.</summary>
        public static int LoanAmount(Adventurer adventurer, BalanceSettings balance) =>
            balance.Dilemmas.LoanSumDays * WalletService.DailyLivingCost(adventurer, ateInTavern: false, balance.Expenses);

        /// <summary>
        /// Оплатить лечение и дать на неделю: расходы на неделю и плата Лазарета за каждый оставшийся день тяжёлой раны,
        /// если Лазарет готов (иначе лечебная часть — 0).
        /// </summary>
        public static int WeekAndTreatment(WorldState world, DataRegistry data, Adventurer adventurer)
        {
            int total = WalletService.WeeklyExpenses(adventurer, data.Balance.Expenses);
            if (!BuildingRules.IsReady(world, data, BuildingFunction.Infirmary)) return total;
            if (!adventurer.State.TryGetCondition(ConditionKind.HeavyWound, out Condition wound)) return total;
            int days = (int)Math.Ceiling(Math.Max(0f, wound.RemainingDays) - 1e-3f);
            return total + days * WalletService.Coins(data.Balance.Expenses.Infirmary);
        }

        /// <summary>
        /// Сколько вариант заберёт из казны: расход казны, заём (доля запрошенной суммы), оплата лечения. Доход (штраф) не считается.
        /// </summary>
        public static int OptionCost(WorldState world, DataRegistry data, Dilemma dilemma, int optionIndex)
        {
            DilemmaOption option = OptionOf(data, dilemma, optionIndex);
            if (option == null) return 0;

            int cost = 0;
            foreach (DilemmaEffect effect in option.Effects)
            {
                switch (effect.Kind)
                {
                    case DilemmaEffectKind.Treasury:
                        if (effect.Value < 0f) cost += WalletService.Coins(-effect.Value);
                        break;
                    case DilemmaEffectKind.GiveLoan:
                        cost += WalletService.Coins(dilemma.Amount * effect.Value);
                        break;
                    case DilemmaEffectKind.PayWeekAndTreatment:
                        if (world.Adventurers.TryGetActive(dilemma.SubjectId, out Adventurer subject)) cost += WeekAndTreatment(world, data, subject);
                        break;
                }
            }
            return cost;
        }

        /// <summary>Вариант можно выбрать: он для игрока и казна его потянет (в минус ради обращения гильдия не уходит).</summary>
        public static bool IsAvailable(WorldState world, DataRegistry data, Dilemma dilemma, int optionIndex)
        {
            DilemmaOption option = OptionOf(data, dilemma, optionIndex);
            if (option == null || !option.PlayerSelectable) return false;
            int cost = OptionCost(world, data, dilemma, optionIndex);
            return cost <= 0 || TreasuryService.CanAfford(world, cost);
        }

        /// <summary>Вариант не выбрать только из-за денег.</summary>
        public static bool IsUnaffordable(WorldState world, DataRegistry data, Dilemma dilemma, int optionIndex)
        {
            DilemmaOption option = OptionOf(data, dilemma, optionIndex);
            return option != null && option.PlayerSelectable && !IsAvailable(world, data, dilemma, optionIndex);
        }

        /// <summary>Варианты, которые игрок может выбрать сейчас (индексы).</summary>
        public static List<int> AvailableOptions(WorldState world, DataRegistry data, Dilemma dilemma)
        {
            var options = new List<int>();
            if (!data.TryGet(dilemma.DefinitionId, out DilemmaDefinition definition)) return options;
            for (int i = 0; i < definition.Options.Count; i++)
            {
                if (IsAvailable(world, data, dilemma, i)) options.Add(i);
            }
            return options;
        }

        public static DilemmaOption OptionOf(DataRegistry data, Dilemma dilemma, int optionIndex)
        {
            if (!data.TryGet(dilemma.DefinitionId, out DilemmaDefinition definition)) return null;
            return optionIndex >= 0 && optionIndex < definition.Options.Count ? definition.Options[optionIndex] : null;
        }

        /// <summary>Была ли в гильдии гибель за <paramref name="days"/> дней до часа <paramref name="atHours"/> включительно.</summary>
        public static bool HadRecentDeath(WorldState world, Calendar calendar, long atHours, int days)
        {
            long from = atHours - calendar.DaysToHours(days);
            foreach (Adventurer gone in world.Adventurers.Archive)
            {
                if (gone.LeaveReason == LeaveReason.Died && gone.LeftAtHours > from && gone.LeftAtHours <= atHours) return true;
            }
            return false;
        }

        /// <summary>Шанс за сутки, который за неделю (7 проверок) даёт <paramref name="weekly"/>.</summary>
        public static float DailyFromWeekly(float weekly) =>
            weekly <= 0f ? 0f : weekly >= 1f ? 1f : (float)(1.0 - Math.Pow(1.0 - weekly, 1.0 / 7.0));

        /// <summary>Двое не должны ходить в одной группе: гильдия запретила (флаг памяти у любого из них).</summary>
        public static bool AreSeparated(Adventurer a, Adventurer b) =>
            a.Memory.HasFlag(MemoryFlag.LoversSeparated, b.Id) || b.Memory.HasFlag(MemoryFlag.LoversSeparated, a.Id);

        /// <summary>Прибавка к оценке напарника: гильдия разрешила этим двоим ходить вместе; иначе 0.</summary>
        public static float PartnerBonus(Adventurer initiator, Adventurer candidate) =>
            initiator.Memory.TryGetFlag(MemoryFlag.LoversTogether, candidate.Id, out MemoryEntry entry) ? entry.Value : 0f;
    }
}
