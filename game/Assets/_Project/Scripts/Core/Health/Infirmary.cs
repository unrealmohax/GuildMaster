namespace GuildMaster.Core
{
    /// <summary>
    /// Запрос к постройкам (ТЗ 11): сколько коек в Лазарете, есть ли Лекарь гильдии и насколько он ускоряет лечение.
    /// До ТЗ 11 — <see cref="NoInfirmary"/>: Лазарета нет, все лечатся со сроком × 1,5 и броском на осложнение
    /// (решение 2026-09-26). Тесты подменяют запрос своим.
    /// </summary>
    public interface IInfirmary
    {
        /// <summary>Коек в Лазарете (0 — Лазарета нет).</summary>
        int Beds(SimContext ctx);

        /// <summary>❔ Есть Лекарь гильдии: без него Лазарет не лечит.</summary>
        bool HasMedic(SimContext ctx);

        /// <summary>❔ Ускорение лечения в Лазарете уровнем возможностей Лекаря (1 — не ускоряет).</summary>
        float HealingSpeed(SimContext ctx);
    }

    /// <summary>Заглушка до ТЗ 11: «свободной койки и Лекаря нет».</summary>
    public sealed class NoInfirmary : IInfirmary
    {
        public static readonly NoInfirmary Instance = new NoInfirmary();

        public int Beds(SimContext ctx) => 0;

        public bool HasMedic(SimContext ctx) => false;

        public float HealingSpeed(SimContext ctx) => 1f;
    }
}
