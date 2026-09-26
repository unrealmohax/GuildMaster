using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Эффективные значения параметров: базовое × модификаторы.
    /// <list type="bullet">
    /// <item><b>Постоянные</b> модификаторы (Калека −30%) не опускают параметр ниже естественного минимума (10) и входят
    /// в расчёт архетипа.</item>
    /// <item><b>Временные</b> (лёгкая рана, усталость выше 70, паника) могут опустить ниже 10 и в архетип не входят.</item>
    /// </list>
    /// Новый модификатор — одна строка в <see cref="PermanentModifiers"/> или <see cref="TemporaryModifiers"/>.
    /// </summary>
    public static class AdventurerStats
    {
        /// <summary>Множитель параметра от одного источника; 1 — не влияет.</summary>
        internal delegate float StatModifier(Adventurer adventurer, StatId stat, DataRegistry data);

        private static readonly StatModifier[] PermanentModifiers =
        {
            TraitProfileMultiplier,
        };

        private static readonly StatModifier[] TemporaryModifiers =
        {
            LightWoundMultiplier,
            FatigueMultiplier,
        };

        /// <summary>Базовое × постоянные модификаторы, не ниже естественного минимума. По нему считается архетип.</summary>
        public static float Permanent(Adventurer adventurer, StatId stat, DataRegistry data)
        {
            float value = adventurer.GetStat(stat);
            float modified = value * Product(PermanentModifiers, adventurer, stat, data);
            float floor = Math.Min(value, data.Balance.Adventurers.NaturalMinimum);
            return Math.Max(modified, floor);
        }

        /// <summary>Значение для расчётов заданий: постоянное × временные модификаторы.</summary>
        public static float Effective(Adventurer adventurer, StatId stat, DataRegistry data) =>
            Permanent(adventurer, stat, data) * Product(TemporaryModifiers, adventurer, stat, data);

        /// <summary>Все 14 параметров с постоянными модификаторами, по <see cref="StatId"/>.</summary>
        public static float[] PermanentProfile(Adventurer adventurer, DataRegistry data)
        {
            var values = new float[Vocabulary.StatCount];
            for (int i = 0; i < values.Length; i++) values[i] = Permanent(adventurer, (StatId)i, data);
            return values;
        }

        private static float Product(StatModifier[] modifiers, Adventurer adventurer, StatId stat, DataRegistry data)
        {
            float product = 1f;
            foreach (StatModifier modifier in modifiers) product *= modifier(adventurer, stat, data);
            return product;
        }

        /// <summary>Лёгкая рана: весь профиль × <c>lightWoundProfileMultiplier</c> (0,85).</summary>
        private static float LightWoundMultiplier(Adventurer adventurer, StatId stat, DataRegistry data) =>
            adventurer.State.HasLightWound() ? data.Balance.Health.LightWoundProfileMultiplier : 1f;

        /// <summary>Усталость выше <c>fatigueProfileThreshold</c>: весь профиль × <c>fatigueProfileMultiplier</c> (0,8).</summary>
        private static float FatigueMultiplier(Adventurer adventurer, StatId stat, DataRegistry data) =>
            StateRules.IsFatigueLowersProfile(adventurer.State, data.Balance.State) ? data.Balance.State.FatigueProfileMultiplier : 1f;

        /// <summary>Эффект черты <c>ProfileMultiplier</c> на выбранный при появлении параметр (Калека).</summary>
        private static float TraitProfileMultiplier(Adventurer adventurer, StatId stat, DataRegistry data)
        {
            float product = 1f;
            foreach (TraitInstance trait in adventurer.Traits)
            {
                if (trait.AffectedStat != stat || !data.TryGet(trait.TraitId, out SpecialTraitDefinition definition)) continue;
                foreach (TraitEffect effect in definition.Effects)
                {
                    if (effect.Kind == EffectKind.ProfileMultiplier) product *= effect.Value;
                }
            }
            return product;
        }
    }
}
