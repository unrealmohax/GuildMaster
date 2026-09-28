using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 12 такта, до построек: персонал (<see cref="StaffService"/>).
    /// <list type="number">
    /// <item>Каждый час — кандидаты, не дождавшиеся ответа, уходят.</item>
    /// <item>В 00:00 — долги по зарплате, на которые хватает денег, гасятся; в начале месяца после этого уходят те, чей долг
    /// висит <c>unpaidMonthsToQuit</c> платёжных сроков ([В], автопауза). Новые зарплаты — позже в этом такте
    /// (<see cref="SalarySystem"/>, после содержания построек).</item>
    /// <item>Утром — кандидаты на каждую вакансию (<see cref="StaffRules.IsVacant"/>): первые — в первое утро вакансии, дальше
    /// каждые <c>candidateIntervalDays</c> суток, пока вакансия открыта.</item>
    /// </list>
    /// Пока вакансий нет, бросков нет. Гильдия закрыта — ничего.
    /// </summary>
    public sealed class StaffSystem : ISimSystem
    {
        public string Name => nameof(StaffSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions || ctx.World.Treasury.IsClosed) return;

            GameTime time = ctx.World.Time;
            StaffService.ExpireCandidates(ctx);

            if (time.Hour == 0)
            {
                StaffService.RepayDebts(ctx);
                if (time.Day == 1) StaffService.QuitUnpaid(ctx);
            }

            if (time.Hour == ctx.Data.Balance.Time.MorningHour) InviteCandidates(ctx);
        }

        private static void InviteCandidates(SimContext ctx)
        {
            StaffRoster roster = ctx.World.Staff;
            long now = ctx.World.Time.TotalHours;
            foreach (StaffRoleDefinition role in ctx.Data.All<StaffRoleDefinition>())
            {
                if (!StaffRules.IsVacant(ctx.World, role))
                {
                    roster.ClearNextCandidatesAt(role.Id);
                    continue;
                }
                if (roster.TryGetNextCandidatesAt(role.Id, out long next) && now < next) continue;

                StaffService.AddCandidates(ctx, role);
                roster.SetNextCandidatesAt(role.Id, now + ctx.Calendar.DaysToHours(ctx.Data.Balance.Staff.CandidateIntervalDays));
            }
        }
    }

    /// <summary>
    /// Шаг 12 такта, после построек: в начале месяца — зарплаты сотрудникам (<see cref="StaffService.PaySalaries"/>). Так
    /// в начале месяца сначала гасятся долги, потом содержание построек (обязательный расход), потом зарплаты — только
    /// на то, что осталось.
    /// </summary>
    public sealed class SalarySystem : ISimSystem
    {
        public string Name => nameof(SalarySystem);

        public void Tick(SimContext ctx)
        {
            GameTime time = ctx.World.Time;
            if (time.Hour != 0 || time.Day != 1 || !ctx.Data.HasDefinitions || ctx.World.Treasury.IsClosed) return;
            StaffService.PaySalaries(ctx);
        }
    }
}
