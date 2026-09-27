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

        public static IReadOnlyList<LedgerCategory> All { get; } = new[]
        {
            Tavern,
        };
    }
}
