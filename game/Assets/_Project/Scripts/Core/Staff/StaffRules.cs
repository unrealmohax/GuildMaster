using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Чистые правила персонала (<see cref="StaffBalance"/>): эффект уровня возможностей, просимая зарплата, шанс согласия
    /// на меньшую сумму, вакансии. Мир не меняют.
    /// </summary>
    public static class StaffRules
    {
        /// <summary>Эффект уровня возможностей: <c>levelEffectBase + уровень / levelEffectDivisor</c> (уровень 50 — ×1).</summary>
        public static float LevelEffect(int level, StaffBalance staff) => staff.LevelEffectBase + level / staff.LevelEffectDivisor;

        /// <summary>
        /// Множитель должности с эффектом <paramref name="effect"/>: по уровню того, кто на ней служит; никого нет — как уровень 0.
        /// Без определений (тесты времени) — 1.
        /// </summary>
        public static float EffectMultiplier(WorldState world, DataRegistry data, StaffLevelEffect effect)
        {
            if (!data.HasDefinitions) return 1f;
            return TryGetWithEffect(world, data, effect, out StaffMember member)
                ? LevelEffect(member.Level, data.Balance.Staff)
                : LevelEffect(0, data.Balance.Staff);
        }

        /// <summary>Кто служит на должности с этим эффектом.</summary>
        public static bool TryGetWithEffect(WorldState world, DataRegistry data, StaffLevelEffect effect, out StaffMember member)
        {
            foreach (StaffMember entry in world.Staff.Members)
            {
                if (data.TryGet(entry.RoleId, out StaffRoleDefinition role) && role.LevelEffect == effect)
                {
                    member = entry;
                    return true;
                }
            }
            member = null;
            return false;
        }

        /// <summary>Просимая зарплата: базовая × (<c>salaryLevelBase</c> + уровень / <c>salaryLevelDivisor</c>), до целого.</summary>
        public static int AskedSalary(StaffRoleDefinition role, int level, StaffBalance staff) =>
            WalletService.Coins(role.BaseSalary * (staff.SalaryLevelBase + level / staff.SalaryLevelDivisor));

        /// <summary>
        /// Шанс, что кандидат согласится на <paramref name="offer"/>: не меньше просимой — 1; меньше —
        /// <c>1 − (просимая − предложение) / (просимая × negotiationTolerance) + репутация / negotiationReputationDivisor</c>,
        /// в пределах 0..1.
        /// </summary>
        public static float AcceptChance(int asked, int offer, float reputation, StaffBalance staff)
        {
            if (offer >= asked) return 1f;
            if (asked <= 0) return 1f;
            float chance = 1f - (asked - offer) / (asked * staff.NegotiationTolerance) + reputation / staff.NegotiationReputationDivisor;
            return Math.Max(0f, Math.Min(1f, chance));
        }

        /// <summary>Вакансия: должность без человека, а её постройка готова (или должности постройка не нужна).</summary>
        public static bool IsVacant(WorldState world, StaffRoleDefinition role)
        {
            if (world.Staff.HasRole(role.Id)) return false;
            return role.RequiredBuilding == null || world.Buildings.IsReady(role.RequiredBuilding.Id);
        }

        /// <summary>Зарплаты всех сотрудников в месяц.</summary>
        public static int MonthlySalaries(WorldState world)
        {
            int total = 0;
            foreach (StaffMember member in world.Staff.Members) total += member.Salary;
            return total;
        }
    }
}
