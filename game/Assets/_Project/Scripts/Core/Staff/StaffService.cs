using System;
using System.Collections.Generic;
using System.Globalization;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Изменения персонала: стартовый персонал, кандидаты на вакансии, переговоры и найм, увольнение и уход, зарплаты и долг.
    /// <list type="bullet">
    /// <item>Сотрудник и кандидат — пол по <c>maleChance</c>, имя из списка имён, не занятое людьми, кандидатами и персоналом;
    /// уровень возможностей — <c>candidateLevel</c>; просимая зарплата — <see cref="StaffRules.AskedSalary"/>. Стартовый персонал
    /// получает просимую.</item>
    /// <item>Одна должность — один человек. Нанят — остальные кандидаты на эту должность уходят.</item>
    /// <item>Зарплата — раз в месяц, только если в казне хватает денег; не хватает — долг по зарплате. Долг гасится целиком,
    /// как только хватает денег; долг висит <c>unpaidMonthsToQuit</c> платёжных сроков — сотрудник уходит, долг пропадает.</item>
    /// </list>
    /// </summary>
    public static class StaffService
    {
        public const string StartStreamName = "StaffStart";

        /// <summary>Персонал «есть на старте» — без событий, своим потоком случайных чисел.</summary>
        internal static void ApplyStart(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions || ctx.Data.Names == null) return;

            long now = ctx.World.Time.TotalHours;
            foreach (StaffRoleDefinition role in ctx.Data.All<StaffRoleDefinition>())
            {
                if (!role.HiredAtStart || ctx.World.Staff.HasRole(role.Id)) continue;
                Generate(ctx, out string name, out Gender gender, out int level);
                int salary = StaffRules.AskedSalary(role, level, ctx.Data.Balance.Staff);
                ctx.World.Staff.AddMember(new StaffMember(ctx.World.Staff.NextId(), name, gender, role.Id, level, salary, now));
            }
        }

        /// <summary>На вакансию пришли кандидаты: <c>candidatesPerVisit</c> человек, ждут <c>candidateWaitDays</c> суток.</summary>
        internal static void AddCandidates(SimContext ctx, StaffRoleDefinition role)
        {
            StaffBalance staff = ctx.Data.Balance.Staff;
            long now = ctx.World.Time.TotalHours;
            long expires = now + ctx.Calendar.DaysToHours(staff.CandidateWaitDays);
            for (int i = 0; i < staff.CandidatesPerVisit; i++)
            {
                Generate(ctx, out string name, out Gender gender, out int level);
                int asked = StaffRules.AskedSalary(role, level, staff);
                var candidate = new StaffCandidate(ctx.World.Staff.NextId(), name, gender, role.Id, level, asked, now, expires);
                ctx.World.Staff.AddCandidate(candidate);
                Publish(ctx, SimEventType.StaffCandidateArrived, EventImportance.Normal, candidate.Id, name, gender, role)
                    .With("level", level)
                    .With("asked", asked)
                    .With("expiresAt", expires);
            }
        }

        /// <summary>
        /// Предложение зарплаты кандидату: согласен — нанят с этой зарплатой; отказ — уходит. Шанс — <see cref="StaffRules.AcceptChance"/>
        /// (бросок из потока, который применяет команду). Нет такого кандидата, должность уже занята, сумма не больше 0 — ничего.
        /// </summary>
        internal static bool Offer(SimContext ctx, int candidateId, int amount)
        {
            StaffRoster roster = ctx.World.Staff;
            if (amount <= 0 || !roster.TryGetCandidate(candidateId, out StaffCandidate candidate)) return false;
            if (roster.HasRole(candidate.RoleId)) return false;

            float chance = StaffRules.AcceptChance(candidate.AskedSalary, amount, ctx.World.Guild.Reputation, ctx.Data.Balance.Staff);
            bool agreed = chance >= 1f || ctx.RollChance(chance, "staff-offer");
            if (ctx.Log.IsOn(SimLogLevel.Info))
            {
                ctx.Log.Write(SimLogLevel.Info, string.Format(CultureInfo.InvariantCulture, "staff offer {0} {1}: asked={2} offered={3} chance={4} => {5}",
                    candidate.RoleId, candidate.Name, candidate.AskedSalary, amount, AdventurerLog.Number(chance), agreed ? "agreed" : "refused"));
            }
            if (!agreed)
            {
                RemoveCandidate(ctx, candidate, CandidateRefused);
                return false;
            }

            Hire(ctx, candidate, amount);
            return true;
        }

        /// <summary>Уволить сотрудника (без выходного пособия, долг по зарплате пропадает).</summary>
        internal static bool Dismiss(SimContext ctx, int staffId)
        {
            if (!ctx.World.Staff.TryGetMember(staffId, out StaffMember member)) return false;
            Leave(ctx, member, StaffLeaveReason.Dismissed);
            return true;
        }

        /// <summary>Кандидаты, которые не дождались ответа, уходят.</summary>
        internal static void ExpireCandidates(SimContext ctx)
        {
            IReadOnlyList<StaffCandidate> candidates = ctx.World.Staff.Candidates;
            long now = ctx.World.Time.TotalHours;
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (now >= candidates[i].ExpiresAtHours) RemoveCandidate(ctx, candidates[i], CandidateExpired);
            }
        }

        /// <summary>Погасить долги по зарплате, на которые хватает денег, — в порядке найма.</summary>
        internal static void RepayDebts(SimContext ctx)
        {
            foreach (StaffMember member in ctx.World.Staff.Members)
            {
                if (member.UnpaidSalary <= 0 || !TreasuryService.CanAfford(ctx.World, member.UnpaidSalary)) continue;

                int amount = member.UnpaidSalary;
                if (!TreasuryService.Debit(ctx, LedgerCategories.Salaries, amount, "debt " + member.RoleId, member.Id)) continue;
                member.UnpaidSalary = 0;
                member.UnpaidMonths = 0;
                Publish(ctx, SimEventType.SalaryDebtPaid, EventImportance.Normal, member).With("amount", amount);
            }
        }

        /// <summary>Кто не получил жалованья <c>unpaidMonthsToQuit</c> платёжных сроков, уходит.</summary>
        internal static void QuitUnpaid(SimContext ctx)
        {
            int limit = ctx.Data.Balance.Staff.UnpaidMonthsToQuit;
            foreach (StaffMember member in new List<StaffMember>(ctx.World.Staff.Members))
            {
                if (member.UnpaidSalary > 0 && member.UnpaidMonths >= limit) Leave(ctx, member, StaffLeaveReason.Quit);
            }
        }

        /// <summary>Зарплаты за месяц — в порядке найма, каждая только если хватает денег; не хватает — долг и строка.</summary>
        internal static void PaySalaries(SimContext ctx)
        {
            foreach (StaffMember member in ctx.World.Staff.Members)
            {
                if (TreasuryService.Debit(ctx, LedgerCategories.Salaries, member.Salary, member.RoleId, member.Id)) continue;

                member.UnpaidSalary += member.Salary;
                member.UnpaidMonths++;
                Publish(ctx, SimEventType.SalaryUnpaid, EventImportance.Important, member)
                    .With("salary", member.Salary)
                    .With("unpaid", member.UnpaidSalary);
            }
        }

        private static void Hire(SimContext ctx, StaffCandidate candidate, int salary)
        {
            StaffRoster roster = ctx.World.Staff;
            roster.RemoveCandidate(candidate);
            var member = new StaffMember(candidate.Id, candidate.Name, candidate.Gender, candidate.RoleId, candidate.Level, salary, ctx.World.Time.TotalHours);
            roster.AddMember(member);
            roster.ClearNextCandidatesAt(candidate.RoleId);
            Publish(ctx, SimEventType.StaffHired, EventImportance.Normal, member)
                .With("salary", salary)
                .With("asked", candidate.AskedSalary);

            foreach (StaffCandidate other in new List<StaffCandidate>(roster.Candidates))
            {
                if (other.RoleId == candidate.RoleId) RemoveCandidate(ctx, other, CandidatePlaceFilled);
            }
        }

        private static void Leave(SimContext ctx, StaffMember member, StaffLeaveReason reason)
        {
            ctx.World.Staff.MoveToFormer(member);
            member.LeftAtHours = ctx.World.Time.TotalHours;
            member.LeaveReason = reason;
            int unpaid = member.UnpaidSalary;
            member.UnpaidSalary = 0;
            if (reason == StaffLeaveReason.Quit)
                Publish(ctx, SimEventType.StaffQuit, EventImportance.Important, member).With("unpaid", unpaid);
            else
                Publish(ctx, SimEventType.StaffDismissed, EventImportance.Notable, member);
        }

        /// <summary>Почему кандидат ушёл (<c>cause</c> события): не дождался ответа, отказался от предложения, место заняли.</summary>
        public const string CandidateExpired = "expired";
        public const string CandidateRefused = "refused";
        public const string CandidatePlaceFilled = "filled";

        private static void RemoveCandidate(SimContext ctx, StaffCandidate candidate, string cause)
        {
            ctx.World.Staff.RemoveCandidate(candidate);
            if (!ctx.Data.TryGet(candidate.RoleId, out StaffRoleDefinition role)) return;
            Publish(ctx, SimEventType.StaffCandidateLeft, EventImportance.Normal, candidate.Id, candidate.Name, candidate.Gender, role)
                .With("cause", cause);
        }

        /// <summary>Пол, свободное имя и уровень возможностей — потоком текущей системы (или команды).</summary>
        private static void Generate(SimContext ctx, out string name, out Gender gender, out int level)
        {
            IntRange levels = ctx.Data.Balance.Staff.CandidateLevel;
            gender = ctx.Rng.Chance(ctx.Data.Balance.Adventurers.MaleChance) ? Gender.Male : Gender.Female;
            name = AdventurerGenerator.PickName(ctx.Rng, ctx.Data.Names, gender, NamesInUse(ctx.World));
            level = ctx.Rng.RangeInclusive(levels.Min, levels.Max);
        }

        private static HashSet<string> NamesInUse(WorldState world)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Adventurer adventurer in world.Adventurers.Active) names.Add(adventurer.Name);
            foreach (Candidate candidate in world.Adventurers.Candidates) names.Add(candidate.Adventurer.Name);
            foreach (StaffMember member in world.Staff.Members) names.Add(member.Name);
            foreach (StaffCandidate candidate in world.Staff.Candidates) names.Add(candidate.Name);
            return names;
        }

        private static SimEvent Publish(SimContext ctx, SimEventType type, EventImportance importance, StaffMember member) =>
            Publish(ctx, type, importance, member.Id, member.Name, member.Gender, ctx.Data.Get<StaffRoleDefinition>(member.RoleId));

        private static SimEvent Publish(SimContext ctx, SimEventType type, EventImportance importance, int staffId, string name, Gender gender,
            StaffRoleDefinition role)
        {
            SimEvent simEvent = ctx.Events.Publish(type, importance)
                .With("staffId", staffId)
                .With("staff", TextValue.Person(name, ctx.Data.NameForms(name), gender))
                .With("role", role.Id);
            if (role.RequiredBuilding != null) simEvent.With("building", role.RequiredBuilding.NameForms);
            return simEvent;
        }
    }
}
