using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Запрос к Лазарету: сколько в нём коек, есть ли Лекарь гильдии и насколько он ускоряет лечение. В игре —
    /// <see cref="BuildingInfirmary"/> (постройка и персонал мира); тесты подменяют запрос своим.
    /// </summary>
    public interface IInfirmary
    {
        /// <summary>Коек в Лазарете (0 — Лазарета нет или он ещё строится).</summary>
        int Beds(SimContext ctx);

        /// <summary>Есть Лекарь гильдии: без него Лазарет не лечит.</summary>
        bool HasMedic(SimContext ctx);

        /// <summary>Ускорение лечения в Лазарете уровнем возможностей Лекаря (1 — не ускоряет).</summary>
        float HealingSpeed(SimContext ctx);
    }

    /// <summary>
    /// Лазарет мира: койки — вместимость готовой постройки с назначением «Лазарет», Лекарь — сотрудник на должности
    /// с эффектом «скорость лечения», ускорение — эффект его уровня (<see cref="StaffRules.LevelEffect"/>).
    /// Лазарета нет — коек 0: все лечатся со сроком × 1,5 и броском на осложнение.
    /// </summary>
    public sealed class BuildingInfirmary : IInfirmary
    {
        public static readonly BuildingInfirmary Instance = new BuildingInfirmary();

        public int Beds(SimContext ctx) =>
            BuildingRules.IsReady(ctx.World, ctx.Data, BuildingFunction.Infirmary, out BuildingDefinition infirmary) ? BuildingRules.Capacity(infirmary) : 0;

        public bool HasMedic(SimContext ctx) =>
            ctx.Data.HasDefinitions && StaffRules.TryGetWithEffect(ctx.World, ctx.Data, StaffLevelEffect.HealingSpeed, out _);

        public float HealingSpeed(SimContext ctx) =>
            StaffRules.TryGetWithEffect(ctx.World, ctx.Data, StaffLevelEffect.HealingSpeed, out StaffMember medic)
                ? StaffRules.LevelEffect(medic.Level, ctx.Data.Balance.Staff)
                : 1f;
    }
}
