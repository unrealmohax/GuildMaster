using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Оси характера (ТЗ 04 → «Оси характера»): полюс, крайний и нейтральный полюс, сила влияния эффекта полюса.
    /// Пороги — <see cref="AdventurersBalance"/>, множители стрелок — <see cref="DecisionsBalance.ArrowMultiplier"/>.
    /// </summary>
    public static class AxisMath
    {
        /// <summary>Край шкалы оси: значения −100..+100 (TechJob/README → «Общие соглашения»).</summary>
        public const float Limit = 100f;

        /// <summary>Полюс, к которому склоняется значение. 0 считается положительным — у нуля сила влияния всё равно 0.</summary>
        public static AxisPole PoleOf(float value) => value < 0f ? AxisPole.Negative : AxisPole.Positive;

        /// <summary>Насколько человек у полюса: <c>|значение| / 100</c>, если знак совпадает с полюсом, иначе 0.</summary>
        public static float Strength(float value, AxisPole pole)
        {
            bool matches = pole == AxisPole.Negative ? value < 0f : value > 0f;
            return matches ? Math.Min(Math.Abs(value), Limit) / Limit : 0f;
        }

        /// <summary>
        /// Множитель эффекта полюса: <c>1 + t × (множитель на крайнем полюсе − 1)</c>, <c>t</c> — <see cref="Strength"/>.
        /// Пример: трус −50, «Безопасность ↑↑» (× 2) → × 1,5.
        /// </summary>
        public static float Multiplier(float value, AxisPole pole, float extremeMultiplier) =>
            1f + Strength(value, pole) * (extremeMultiplier - 1f);

        /// <summary>То же для эффекта со стрелкой: множитель стрелки — из <see cref="DecisionsBalance"/>.</summary>
        public static float ArrowMultiplier(float value, AxisPole pole, Arrow arrow, DecisionsBalance decisions) =>
            Multiplier(value, pole, decisions.ArrowMultiplier(arrow));

        /// <summary>Крайний полюс: <c>|значение| ≥ extremePoleThreshold</c> (70) — для правил «только у крайнего».</summary>
        public static bool IsExtreme(float value, AdventurersBalance balance) => Math.Abs(value) >= balance.ExtremePoleThreshold;

        /// <summary>Человек на крайнем полюсе <paramref name="pole"/> (трус ≤ −70, безрассудный ≥ 70…).</summary>
        public static bool IsOnExtremePole(float value, AxisPole pole, AdventurersBalance balance) =>
            IsExtreme(value, balance) && PoleOf(value) == pole;

        /// <summary>Нейтральная ось: <c>|значение| &lt; neutralThreshold</c> (30) — раскрывать нечего, раскрывается как «уравновешен».</summary>
        public static bool IsNeutral(float value, AdventurersBalance balance) => Math.Abs(value) < balance.NeutralThreshold;

        /// <summary>Значение в пределах шкалы.</summary>
        public static float Clamp(float value) => Math.Max(-Limit, Math.Min(Limit, value));
    }
}
