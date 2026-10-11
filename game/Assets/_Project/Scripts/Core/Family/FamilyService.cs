using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Жизнь семьи за кадром: недельная плата и нужда, болезнь и смерть родных, весть человеку, траур. Броски — поток вызывающей
    /// системы (<see cref="FamilySystem"/>, отладочные команды). Человек на задании узнаёт о болезни и смерти по возвращении
    /// (<see cref="DeliverNews"/>): событие, стресс и траур — в час, когда он снова в гильдии.
    /// </summary>
    public static class FamilyService
    {
        /// <summary>
        /// Плата семье за неделю (<see cref="FamilyRules.Payment"/>): деньги уходят из мира. Заплачено меньше нормы — семья в нужде
        /// (начало — событие <see cref="SimEventType.FamilyNeedStarted"/>), полная плата нужду снимает. Возвращает, заплачена ли норма
        /// полностью. Нет живых родных — ничего (и нужды нет).
        /// </summary>
        internal static bool PayWeek(SimContext ctx, Adventurer adventurer)
        {
            FamilyBalance balance = ctx.Data.Balance.Family;
            AdventurerState state = adventurer.State;
            int norm = FamilyRules.Norm(adventurer, balance);
            if (norm <= 0)
            {
                state.FamilyInNeed = false;
                return true;
            }

            int walletBefore = state.Wallet;
            int sent = WalletService.SendHome(adventurer, FamilyRules.Payment(adventurer, norm, ctx.Data));
            bool full = sent >= norm;
            if (ctx.Log.IsOn(SimLogLevel.Debug))
            {
                ctx.Log.Begin(SimLogLevel.Debug).Append("family pay #").Append(adventurer.Id).Append(' ').Append(adventurer.Name)
                    .Append(" norm=").Append(norm).Append(" sent=").Append(sent)
                    .Append(" (wallet ").Append(walletBefore).Append(" -> ").Append(state.Wallet).Append(')');
                ctx.Log.Commit();
            }
            ctx.Events.Publish(SimEventType.FamilyMoneySent, EventImportance.Normal, adventurer.Id)
                .With("amount", sent)
                .With("norm", norm);

            if (full)
            {
                if (state.FamilyInNeed) ctx.Log.Write(SimLogLevel.Debug, "family #{0} {1}: need over", adventurer.Id, adventurer.Name);
                state.FamilyInNeed = false;
            }
            else if (!state.FamilyInNeed)
            {
                state.FamilyInNeed = true;
                ctx.Events.Publish(SimEventType.FamilyNeedStarted, EventImportance.Notable, adventurer.Id)
                    .With("amount", sent)
                    .With("norm", norm);
            }
            return full;
        }

        /// <summary>
        /// Неделя болезни: у каждого больного — бросок смерти (<see cref="FamilyRules.DeathChance"/> — ниже при полной плате),
        /// выживший поправляется, когда срок болезни прошёл (только лог).
        /// </summary>
        internal static void WeekOfIllness(SimContext ctx, Adventurer adventurer, bool paidInFull)
        {
            long now = ctx.World.Time.TotalHours;
            foreach (FamilyMember member in adventurer.Family.Members)
            {
                if (!member.IsIll) continue;
                if (ctx.RollChance(FamilyRules.DeathChance(paidInFull, ctx.Data.Balance.Family), "family-death", adventurer))
                {
                    Die(ctx, adventurer, member);
                    continue;
                }
                if (now < member.IllUntilHours) continue;
                member.Health = FamilyHealth.Healthy;
                if (member.PendingNews == FamilyNews.Ill) member.PendingNews = FamilyNews.None;
                ctx.Log.Write(SimLogLevel.Debug, "family #{0} {1}: {2} {3} recovered", adventurer.Id, adventurer.Name, member.Role, member.Name);
            }
        }

        /// <summary>Начало месяца: у каждого живого здорового родного — бросок болезни по роли.</summary>
        internal static void RollIllness(SimContext ctx, Adventurer adventurer)
        {
            FamilyBalance balance = ctx.Data.Balance.Family;
            foreach (FamilyMember member in adventurer.Family.Members)
            {
                if (!member.Alive || member.IsIll) continue;
                if (ctx.RollChance(FamilyRules.IllChance(member.Role, balance), "family-ill", adventurer)) FallIll(ctx, adventurer, member);
            }
        }

        /// <summary>Родной заболел на <c>illWeeks</c> недель (бросок); весть — сразу или по возвращении с задания.</summary>
        internal static void FallIll(SimContext ctx, Adventurer adventurer, FamilyMember member)
        {
            FamilyBalance balance = ctx.Data.Balance.Family;
            long now = ctx.World.Time.TotalHours;
            int weeks = ctx.Rng.RangeInclusive(balance.IllWeeks.Min, balance.IllWeeks.Max);
            member.Health = FamilyHealth.Ill;
            member.IllSinceHours = now;
            member.IllUntilHours = now + ctx.Calendar.DaysToHours(weeks * balance.PaymentIntervalDays);
            if (ctx.Log.IsOn(SimLogLevel.Debug))
            {
                ctx.Log.Begin(SimLogLevel.Debug).Append("family #").Append(adventurer.Id).Append(' ').Append(adventurer.Name)
                    .Append(": ").Append(member.Role).Append(' ').Append(member.Name).Append(" ill for ").Append(weeks).Append(" weeks");
                ctx.Log.Commit();
            }
            Tell(ctx, adventurer, member, FamilyNews.Ill);
        }

        /// <summary>Родной умер: из нормы он выпадает; весть — сразу или по возвращении с задания.</summary>
        internal static void Die(SimContext ctx, Adventurer adventurer, FamilyMember member)
        {
            member.Alive = false;
            member.Health = FamilyHealth.Healthy;
            member.DiedAtHours = ctx.World.Time.TotalHours;
            ctx.Log.Write(SimLogLevel.Debug, "family #{0} {1}: {2} {3} died", adventurer.Id, adventurer.Name, member.Role, member.Name);
            Tell(ctx, adventurer, member, FamilyNews.Died);
        }

        /// <summary>Вести, которые ждали человека с задания: болезнь (если ещё болеет) и смерть.</summary>
        internal static void DeliverNews(SimContext ctx, Adventurer adventurer)
        {
            List<FamilyMember> members = null;
            foreach (FamilyMember member in adventurer.Family.Members)
            {
                if (member.PendingNews == FamilyNews.None) continue;
                (members ??= new List<FamilyMember>()).Add(member);
            }
            if (members == null) return;

            foreach (FamilyMember member in members)
            {
                FamilyNews news = member.PendingNews;
                member.PendingNews = FamilyNews.None;
                if (news == FamilyNews.Died) Learn(ctx, adventurer, member, FamilyNews.Died);
                else if (member.IsIll) Learn(ctx, adventurer, member, FamilyNews.Ill);
            }
        }

        /// <summary>
        /// Сутки (00:00): семья в нужде — стресс <c>needStressPerDay</c> (Семейный × <c>familyNeedMultiplier</c>, × черты);
        /// траур кончился — снимается.
        /// </summary>
        internal static void ApplyDay(SimContext ctx, Adventurer adventurer)
        {
            FamilyBalance balance = ctx.Data.Balance.Family;
            AdventurerState state = adventurer.State;
            if (state.FamilyInNeed)
            {
                float stress = balance.NeedStressPerDay;
                if (FamilyRules.IsFamilial(adventurer, ctx.Data)) stress *= balance.FamilyNeedMultiplier;
                StateService.AddStress(ctx, adventurer, stress);
            }
            if (state.IsMourning && ctx.World.Time.TotalHours >= state.MourningEndsAtHours)
            {
                state.IsMourning = false;
                ctx.Log.Write(SimLogLevel.Debug, "family #{0} {1}: mourning over", adventurer.Id, adventurer.Name);
            }
        }

        private static void Tell(SimContext ctx, Adventurer adventurer, FamilyMember member, FamilyNews news)
        {
            if (adventurer.State.IsOnQuest())
            {
                if (member.PendingNews != FamilyNews.Died) member.PendingNews = news;
                return;
            }
            Learn(ctx, adventurer, member, news);
        }

        /// <summary>
        /// Человек узнал: о болезни — строка ленты; о смерти — событие с автопаузой, стресс (Семейный — сильнее, × черты) и траур
        /// на <c>mourningDays</c> суток.
        /// </summary>
        private static void Learn(SimContext ctx, Adventurer adventurer, FamilyMember member, FamilyNews news)
        {
            int index = IndexOf(adventurer.Family, member);
            if (news == FamilyNews.Ill)
            {
                ctx.Events.Publish(SimEventType.FamilyMemberIll, EventImportance.Notable, adventurer.Id)
                    .With("relative", index)
                    .With("role", member.Role);
                return;
            }

            AdventurerState state = adventurer.State;
            StateService.AddStress(ctx, adventurer, FamilyRules.DeathStress(adventurer, ctx.Data));
            long end = ctx.World.Time.TotalHours + ctx.Calendar.DaysToHours(ctx.Data.Balance.Family.MourningDays);
            state.IsMourning = true;
            if (end > state.MourningEndsAtHours) state.MourningEndsAtHours = end;
            ctx.Events.Publish(SimEventType.FamilyMemberDied, EventImportance.Important, adventurer.Id)
                .With("relative", index)
                .With("role", member.Role);
        }

        private static int IndexOf(Family family, FamilyMember member)
        {
            for (int i = 0; i < family.Members.Count; i++)
            {
                if (family.Members[i] == member) return i;
            }
            return -1;
        }
    }
}
