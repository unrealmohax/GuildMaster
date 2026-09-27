using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Что в состоянии человека усилило мотив.</summary>
    public enum StateFactor
    {
        /// <summary>Кошелёк меньше расходов на неделю: Деньги.</summary>
        LowWallet,

        /// <summary>Усталость выше порога: Отдых.</summary>
        Fatigue,

        /// <summary>Стресс выше порога: Утешение и Безопасность.</summary>
        Stress,

        /// <summary>Лёгкая рана: Безопасность.</summary>
        LightWound,
    }

    /// <summary>Веса шести мотивов и факторы состояния, которые их усилили.</summary>
    public sealed class MotiveWeights
    {
        private readonly float[] weights = new float[Vocabulary.MotiveCount];
        private readonly float[] traitWeights = new float[Vocabulary.MotiveCount];
        private readonly List<(StateFactor Factor, Motive Motive, float Value)> factors = new List<(StateFactor, Motive, float)>();

        public float this[Motive motive] => weights[(int)motive];

        /// <summary>Вес только от черт, без состояния.</summary>
        public float TraitWeight(Motive motive) => traitWeights[(int)motive];

        /// <summary>Факторы состояния: что, какой мотив усилило, значение показателя (для причин и лога).</summary>
        public IReadOnlyList<(StateFactor Factor, Motive Motive, float Value)> Factors => factors;

        internal void Set(Motive motive, float value) => weights[(int)motive] = value;

        internal void Multiply(Motive motive, float multiplier) => weights[(int)motive] *= multiplier;

        internal void SaveTraitWeights() => weights.CopyTo(traitWeights, 0);

        internal void AddFactor(StateFactor factor, Motive motive, float value) => factors.Add((factor, motive, value));

        /// <summary>Фактор состояния, который усилил мотив; нет — <c>false</c>.</summary>
        public bool TryGetFactor(Motive motive, out StateFactor factor, out float value)
        {
            foreach ((StateFactor f, Motive m, float v) in factors)
            {
                if (m != motive) continue;
                factor = f;
                value = v;
                return true;
            }
            factor = default;
            value = 0f;
            return false;
        }
    }

    /// <summary>
    /// Вес мотива = вес по умолчанию × множители черт × множители состояния.
    /// <list type="bullet">
    /// <item>Черты — эффекты <see cref="EffectKind.MotiveWeight"/> из данных: у осей сила плавно растёт от центра к полюсу
    /// (<see cref="AxisMath.ArrowMultiplier"/>), у особых черт — полная стрелка. Скрытые черты действуют так же, как раскрытые.
    /// Утешение Пьяницы (<see cref="TraitHook.DrunkardComfortOnlyInTavern"/>) действует только на таверну.</item>
    /// <item>Состояние: кошелёк меньше расходов на неделю — Деньги × 2; усталость выше 50 — Отдых × (1 + (усталость − 50) / 25);
    /// стресс выше 40 — Утешение × (1 + (стресс − 40) / 30) и Безопасность × 1,3; лёгкая рана — Безопасность × 1,3.</item>
    /// </list>
    /// </summary>
    public static class Motives
    {
        /// <summary>Веса мотивов для варианта: <paramref name="inTavern"/> — вариант в таверне (для эффектов «только таверна»).</summary>
        public static MotiveWeights Weigh(Adventurer adventurer, DataRegistry data, bool inTavern)
        {
            var weights = new MotiveWeights();
            DecisionsBalance decisions = data.Balance.Decisions;
            for (int i = 0; i < Vocabulary.MotiveCount; i++) weights.Set((Motive)i, decisions.DefaultMotiveWeight);

            ApplyTraits(weights, adventurer, data, inTavern);
            weights.SaveTraitWeights();
            ApplyState(weights, adventurer, data.Balance);
            return weights;
        }

        private static void ApplyTraits(MotiveWeights weights, Adventurer adventurer, DataRegistry data, bool inTavern)
        {
            DecisionsBalance decisions = data.Balance.Decisions;
            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                float value = adventurer.GetAxis((AxisId)i);
                AxisPole pole = AxisMath.PoleOf(value);
                foreach (TraitEffect effect in data.Axis((AxisId)i).Pole(pole).Effects)
                {
                    if (effect.Kind != EffectKind.MotiveWeight || !Holds(effect, adventurer, value, data)) continue;
                    weights.Multiply(effect.Motive, AxisMath.ArrowMultiplier(value, pole, effect.Arrow, decisions));
                }
            }

            foreach (TraitInstance trait in adventurer.Traits)
            {
                SpecialTraitDefinition definition = data.Get<SpecialTraitDefinition>(trait.TraitId);
                bool comfortOnlyInTavern = definition.HasHook(TraitHook.DrunkardComfortOnlyInTavern);
                foreach (TraitEffect effect in definition.Effects)
                {
                    if (effect.Kind != EffectKind.MotiveWeight || !Holds(effect, adventurer, null, data)) continue;
                    if (comfortOnlyInTavern && effect.Motive == Motive.Comfort && !inTavern) continue;
                    weights.Multiply(effect.Motive, decisions.ArrowMultiplier(effect.Arrow));
                }
            }
        }

        private static void ApplyState(MotiveWeights weights, Adventurer adventurer, BalanceSettings balance)
        {
            AdventurerState state = adventurer.State;
            DecisionsBalance decisions = balance.Decisions;

            if (WalletService.IsBelowWeeklyExpenses(adventurer, balance.Expenses))
            {
                weights.Multiply(Motive.Money, WalletService.MoneyMotiveMultiplier(adventurer, balance));
                weights.AddFactor(StateFactor.LowWallet, Motive.Money, state.Wallet);
            }
            if (state.Fatigue > decisions.FatigueRestThreshold)
            {
                weights.Multiply(Motive.Rest, 1f + (state.Fatigue - decisions.FatigueRestThreshold) / decisions.FatigueRestDivisor);
                weights.AddFactor(StateFactor.Fatigue, Motive.Rest, state.Fatigue);
            }
            if (state.Stress > decisions.StressComfortThreshold)
            {
                weights.Multiply(Motive.Comfort, 1f + (state.Stress - decisions.StressComfortThreshold) / decisions.StressComfortDivisor);
                weights.Multiply(Motive.Safety, decisions.StressSafetyMultiplier);
                weights.AddFactor(StateFactor.Stress, Motive.Comfort, state.Stress);
                weights.AddFactor(StateFactor.Stress, Motive.Safety, state.Stress);
            }
            if (state.HasLightWound())
            {
                weights.Multiply(Motive.Safety, decisions.LightWoundSafetyMultiplier);
                weights.AddFactor(StateFactor.LightWound, Motive.Safety, 1f);
            }
        }

        /// <param name="axisValue">Значение оси для условия «крайний полюс»; у особой черты — null.</param>
        private static bool Holds(TraitEffect effect, Adventurer adventurer, float? axisValue, DataRegistry data)
        {
            switch (effect.Condition)
            {
                case EffectCondition.Always: return true;
                case EffectCondition.ExtremePole: return !axisValue.HasValue || AxisMath.IsExtreme(axisValue.Value, data.Balance.Adventurers);
                case EffectCondition.StressAbove: return adventurer.State.Stress > effect.ConditionThreshold;
                default: return false; // «в группе» / «один» — только на задании
            }
        }
    }
}
