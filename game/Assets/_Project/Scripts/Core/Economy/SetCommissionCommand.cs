using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Игрок меняет комиссию гильдии — долю с наград заказов. Значение обрезается до пределов <c>commissionLimits</c>.
    /// Новая комиссия действует на награды, выплаченные после изменения, и на цель довольства со следующего расчёта (00:00).
    /// </summary>
    public sealed class SetCommissionCommand : ICommand
    {
        private const float Epsilon = 1e-5f;

        public SetCommissionCommand(float commission)
        {
            Commission = commission;
        }

        public float Commission { get; }

        public void Apply(SimContext ctx)
        {
            if (float.IsNaN(Commission)) return;

            FloatRange limits = ctx.Data.Balance.Economy.CommissionLimits;
            float value = Math.Min(limits.Max, Math.Max(limits.Min, Commission));
            Treasury treasury = ctx.World.Treasury;
            if (Math.Abs(treasury.Commission - value) < Epsilon) return;

            float from = treasury.Commission;
            treasury.Commission = value;
            ctx.Events.Publish(SimEventType.CommissionChanged)
                .With("from", from)
                .With("to", value);
        }
    }
}
