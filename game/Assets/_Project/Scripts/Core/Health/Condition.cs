namespace GuildMaster.Core
{
    /// <summary>
    /// Вид раны. Осложнение — свойство тяжёлой раны (<see cref="Condition.IsComplicated"/>: +7 дней,
    /// стресс), увечье — черта «Калека». Болезней в прототипе нет.
    /// </summary>
    public enum ConditionKind
    {
        /// <summary>3–5 дней, профиль × 0,85, задания можно.</summary>
        LightWound,

        /// <summary>14–21 день, задания нельзя.</summary>
        HeavyWound,
    }

    /// <summary>Лёгкая или тяжёлая рана: срок и сколько осталось. Лечение — <see cref="HealthSystem"/>, раны — <see cref="HealthService"/>.</summary>
    public sealed class Condition
    {
        internal Condition(ConditionKind kind, int days, long inflictedAtHours)
        {
            Kind = kind;
            Days = days;
            RemainingDays = days;
            InflictedAtHours = inflictedAtHours;
        }

        public ConditionKind Kind { get; }

        /// <summary>Срок при получении, дней (с осложнением — больше).</summary>
        public int Days { get; internal set; }

        /// <summary>
        /// Сколько осталось, в днях обычного лечения. Раз в сутки уменьшается на 1 × множитель
        /// (в Лазарете — 1 / 0,7, без него — 1 / 1,5), ноль — рана зажила.
        /// </summary>
        public float RemainingDays { get; internal set; }

        /// <summary>Когда получена: при равенстве тяжести койку Лазарета получает раненый раньше.</summary>
        public long InflictedAtHours { get; }

        /// <summary>Тяжёлая рана с осложнением.</summary>
        public bool IsComplicated { get; internal set; }

        /// <summary>Бросок на осложнение уже был (один на тяжёлую рану).</summary>
        internal bool ComplicationChecked { get; set; }
    }
}
