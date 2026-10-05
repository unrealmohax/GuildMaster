using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>Доход или расход: знак суммы в журнале.</summary>
    public enum LedgerFlow
    {
        Income,
        Expense,
    }

    /// <summary>Вид расхода: что делать, если денег в казне меньше суммы.</summary>
    public enum ExpenseKind
    {
        /// <summary>Не расход (статья дохода).</summary>
        None,

        /// <summary>Списывается всегда, даже в минус: такие расходы и ведут к банкротству.</summary>
        Mandatory,

        /// <summary>Не списывается, если денег меньше суммы.</summary>
        IfAffordable,
    }

    /// <summary>
    /// Статья журнала казны: код (латиницей), доход или расход, вид расхода. Название для игрока — шаблон
    /// <see cref="TextKey"/> в наборе шаблонов ленты.
    /// </summary>
    public sealed class LedgerCategory
    {
        public LedgerCategory(string id, LedgerFlow flow, ExpenseKind expenseKind = ExpenseKind.None)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Category id is required", nameof(id));
            if (flow == LedgerFlow.Income && expenseKind != ExpenseKind.None)
                throw new ArgumentException($"Income category {id} cannot have an expense kind", nameof(expenseKind));
            if (flow == LedgerFlow.Expense && expenseKind == ExpenseKind.None)
                throw new ArgumentException($"Expense category {id} needs an expense kind", nameof(expenseKind));

            Id = id;
            Flow = flow;
            ExpenseKind = expenseKind;
        }

        public string Id { get; }
        public LedgerFlow Flow { get; }
        public ExpenseKind ExpenseKind { get; }

        /// <summary>Ключ шаблона с названием статьи: <c>ledger.{id}</c>.</summary>
        public string TextKey => "ledger." + Id;

        public override string ToString() => Id;
    }

    /// <summary>
    /// Статьи журнала казны. Новая статья — поле здесь и строка в <see cref="All"/>; её название — шаблон
    /// <see cref="LedgerCategory.TextKey"/> (его проверит валидатор данных).
    /// </summary>
    public static class LedgerCategories
    {
        /// <summary>Таверна: доля гильдии с каждой еды и выпивки.</summary>
        public static readonly LedgerCategory Tavern = new LedgerCategory("tavern", LedgerFlow.Income);

        /// <summary>Комиссия гильдии с награды выполненного заказа.</summary>
        public static readonly LedgerCategory Commission = new LedgerCategory("commission", LedgerFlow.Income);

        /// <summary>Погашение долга авантюриста из его доли награды; связанный объект — человек.</summary>
        public static readonly LedgerCategory DebtRepayment = new LedgerCategory("debtRepayment", LedgerFlow.Income);

        /// <summary>Доплата гильдии к выполненному заказу — обещана, платится даже в минус.</summary>
        public static readonly LedgerCategory Surcharges = new LedgerCategory("surcharges", LedgerFlow.Expense, ExpenseKind.Mandatory);

        /// <summary>Награда за выполненное событийное задание — его платит гильдия.</summary>
        public static readonly LedgerCategory EventQuests = new LedgerCategory("eventQuests", LedgerFlow.Expense, ExpenseKind.Mandatory);

        /// <summary>Награда группе за сообщение о находке.</summary>
        public static readonly LedgerCategory Discoveries = new LedgerCategory("discoveries", LedgerFlow.Expense, ExpenseKind.Mandatory);

        /// <summary>Место в Общежитии: плата жильцов за сутки.</summary>
        public static readonly LedgerCategory Dormitory = new LedgerCategory("dormitory", LedgerFlow.Income);

        /// <summary>Лечение в Лазарете: плата больных за сутки (сколько смогли заплатить).</summary>
        public static readonly LedgerCategory Infirmary = new LedgerCategory("infirmary", LedgerFlow.Income);

        /// <summary>Тренировочный двор: плата за день тренировки.</summary>
        public static readonly LedgerCategory TrainingYard = new LedgerCategory("trainingYard", LedgerFlow.Income);

        /// <summary>Стройка: цена постройки, списывается, когда стройка начинается, — только если денег хватает.</summary>
        public static readonly LedgerCategory Construction = new LedgerCategory("construction", LedgerFlow.Expense, ExpenseKind.IfAffordable);

        /// <summary>Содержание построек в начале месяца — даже в минус.</summary>
        public static readonly LedgerCategory Upkeep = new LedgerCategory("upkeep", LedgerFlow.Expense, ExpenseKind.Mandatory);

        /// <summary>Зарплата сотруднику — только если денег хватает, иначе долг по зарплате; связанный объект — сотрудник.</summary>
        public static readonly LedgerCategory Salaries = new LedgerCategory("salaries", LedgerFlow.Expense, ExpenseKind.IfAffordable);

        /// <summary>
        /// Расходы по распоряжениям — даже в минус: еда и жильё новичков, компенсация за ранение. Комментарий — id распоряжения,
        /// связанный объект — человек.
        /// </summary>
        public static readonly LedgerCategory Decrees = new LedgerCategory("decrees", LedgerFlow.Expense, ExpenseKind.Mandatory);

        /// <summary>Деньги, добавленные отладочной панелью.</summary>
        public static readonly LedgerCategory DebugIncome = new LedgerCategory("debugIncome", LedgerFlow.Income);

        /// <summary>Деньги, снятые отладочной панелью — даже в минус.</summary>
        public static readonly LedgerCategory DebugExpense = new LedgerCategory("debugExpense", LedgerFlow.Expense, ExpenseKind.Mandatory);

        public static IReadOnlyList<LedgerCategory> All { get; } = new[]
        {
            Tavern, Commission, DebtRepayment, Dormitory, Infirmary, TrainingYard,
            Surcharges, EventQuests, Discoveries, Construction, Upkeep, Salaries, Decrees, DebugIncome, DebugExpense,
        };
    }
}
