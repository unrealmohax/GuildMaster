using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Эффекты осей и особых черт на скорость показателей состояния (<see cref="EffectKind.StateRate"/>) и чувствительность
    /// довольства к комиссии (<see cref="EffectKind.PayContentmentSensitivity"/>). Какие у кого эффекты — в данных:
    /// трус набирает стресс быстрее, Кошмары — усталость × 1,3, Потерявший товарища снимает стресс × 0,5,
    /// Преданный и Наёмник — лояльность (ТЗ 04, 05). Скрытые черты действуют так же, как раскрытые.
    /// <para>
    /// У осей сила — по формуле оси (<see cref="AxisMath.ArrowMultiplier"/>): от центра к полюсу. У особых черт — полная:
    /// стрелка (<see cref="DecisionsBalance.ArrowMultiplier"/>) или явный множитель эффекта.
    /// </para>
    /// </summary>
    public static class StateRates
    {
        /// <summary>
        /// Множитель изменения показателя: рост (<see cref="RateDirection.Growth"/>) или падение (<see cref="RateDirection.Decay"/>).
        /// Условия эффектов: «в группе» / «один» — по <paramref name="party"/>, «крайний полюс», «стресс выше порога».
        /// </summary>
        public static float Multiplier(Adventurer adventurer, StateStat stat, RateDirection direction, DataRegistry data,
            PartyContext party = PartyContext.None)
        {
            DecisionsBalance decisions = data.Balance.Decisions;
            float product = 1f;

            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                float value = adventurer.GetAxis((AxisId)i);
                AxisPole pole = AxisMath.PoleOf(value);
                foreach (TraitEffect effect in data.Axis((AxisId)i).Pole(pole).Effects)
                {
                    if (!Matches(effect, stat, direction) || !Holds(effect, adventurer, value, party, data)) continue;
                    product *= effect.Arrow != Arrow.None
                        ? AxisMath.ArrowMultiplier(value, pole, effect.Arrow, decisions)
                        : AxisMath.Multiplier(value, pole, effect.Value);
                }
            }

            foreach (TraitInstance trait in adventurer.Traits)
            {
                foreach (TraitEffect effect in data.Get<SpecialTraitDefinition>(trait.TraitId).Effects)
                {
                    if (!Matches(effect, stat, direction) || !Holds(effect, adventurer, null, party, data)) continue;
                    product *= effect.Arrow != Arrow.None ? decisions.ArrowMultiplier(effect.Arrow) : effect.Value;
                }
            }
            return product;
        }

        /// <summary>Множитель вклада комиссии в цель довольства (Жадный — до × 2).</summary>
        public static float PayContentmentSensitivity(Adventurer adventurer, DataRegistry data)
        {
            float product = 1f;
            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                float value = adventurer.GetAxis((AxisId)i);
                AxisPole pole = AxisMath.PoleOf(value);
                foreach (TraitEffect effect in data.Axis((AxisId)i).Pole(pole).Effects)
                {
                    if (effect.Kind == EffectKind.PayContentmentSensitivity && Holds(effect, adventurer, value, PartyContext.None, data))
                        product *= AxisMath.Multiplier(value, pole, effect.Value);
                }
            }

            foreach (TraitInstance trait in adventurer.Traits)
            {
                foreach (TraitEffect effect in data.Get<SpecialTraitDefinition>(trait.TraitId).Effects)
                {
                    if (effect.Kind == EffectKind.PayContentmentSensitivity && Holds(effect, adventurer, null, PartyContext.None, data))
                        product *= effect.Value;
                }
            }
            return product;
        }

        private static bool Matches(TraitEffect effect, StateStat stat, RateDirection direction) =>
            effect.Kind == EffectKind.StateRate && effect.StateStat == stat && effect.Direction == direction;

        /// <param name="axisValue">Значение оси для условия «крайний полюс»; у особой черты — null (полюса нет, условие не мешает).</param>
        private static bool Holds(TraitEffect effect, Adventurer adventurer, float? axisValue, PartyContext party, DataRegistry data)
        {
            switch (effect.Condition)
            {
                case EffectCondition.Always: return true;
                case EffectCondition.InGroup: return party == PartyContext.InGroup;
                case EffectCondition.Solo: return party == PartyContext.Solo;
                case EffectCondition.ExtremePole: return !axisValue.HasValue || AxisMath.IsExtreme(axisValue.Value, data.Balance.Adventurers);
                case EffectCondition.StressAbove: return adventurer.State.Stress > effect.ConditionThreshold;
                default: return false;
            }
        }
    }
}
