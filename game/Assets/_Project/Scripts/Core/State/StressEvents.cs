using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Стресс от событий (ТЗ 05 → «Прибавки от событий»). Вызывают задания (ТЗ 09): провал раунда, рана товарища, гибель.
    /// Своя рана — <see cref="HealthService.Wound"/>. Прибавки умножаются на эффекты черт (трус — быстрее).
    /// </summary>
    public static class StressEvents
    {
        /// <summary>
        /// Провал раунда: всем в группе — стресс ступени лестницы провалов (<see cref="RoundsBalance.FailureLadder"/>,
        /// +5 / +5 / +8). <paramref name="failedRounds"/> — номер провала, с 1.
        /// </summary>
        public static void RoundFailed(SimContext ctx, IReadOnlyList<Adventurer> party, int failedRounds)
        {
            IReadOnlyList<FailureStep> ladder = ctx.Data.Balance.Rounds.FailureLadder;
            if (failedRounds < 1 || ladder.Count == 0) return;

            float stress = ladder[System.Math.Min(failedRounds, ladder.Count) - 1].Stress;
            PartyContext context = ContextOf(party);
            foreach (Adventurer member in party) StateService.AddStress(ctx, member, stress, context);
        }

        /// <summary>Рана товарища: остальным в группе +<c>comradeWoundStress</c> (5).</summary>
        public static void ComradeWounded(SimContext ctx, IReadOnlyList<Adventurer> party, Adventurer wounded)
        {
            PartyContext context = ContextOf(party);
            foreach (Adventurer member in party)
            {
                if (member.Id != wounded.Id) StateService.AddStress(ctx, member, ctx.Data.Balance.State.ComradeWoundStress, context);
            }
        }

        /// <summary>
        /// Гибель человека гильдии: всем остальным в гильдии стресс +25, другу (отношения от <c>friendsThreshold</c>) +40,
        /// партнёру Влюблённого +60; Железные нервы — × 0,5. Другу (отношения от <c>grievingFriendRelation</c>) и партнёру
        /// Влюблённого — черта «Потерявший товарища» (если её можно дать). Вызывать до или после ухода погибшего в архив — всё равно.
        /// </summary>
        public static void AdventurerDied(SimContext ctx, Adventurer deceased)
        {
            DataRegistry data = ctx.Data;
            StateBalance state = data.Balance.State;
            TraitsBalance traits = data.Balance.Traits;
            SpecialTraitDefinition grieving = TraitRules.FindByHook(data, TraitHook.GrievingOutcome);

            foreach (Adventurer survivor in new List<Adventurer>(ctx.World.Adventurers.Active))
            {
                if (survivor.Id == deceased.Id) continue;

                float relation = ctx.World.Relations.GetValue(survivor.Id, deceased.Id);
                TraitInstance lover = TraitRules.FindWithHook(survivor, TraitHook.LoverPartner, data);
                bool isLover = lover != null && lover.PartnerId == deceased.Id;
                bool isFriend = RelationService.AreFriends(ctx.World.Relations, survivor.Id, deceased.Id, data.Balance.Adventurers);

                float stress = isLover ? state.LoverDeathStress : isFriend ? state.FriendDeathStress : state.ComradeDeathStress;
                if (TraitRules.FindWithHook(survivor, TraitHook.IronNervesComradeDeath, data) != null)
                    stress *= traits.IronNervesComradeDeathMultiplier;
                StateService.AddStress(ctx, survivor, stress);

                if (grieving != null && (isLover || relation >= traits.GrievingFriendRelation))
                    TraitService.TryAcquire(ctx, survivor, grieving.Id, deceased.Id);
            }
        }

        private static PartyContext ContextOf(IReadOnlyList<Adventurer> party) =>
            party.Count > 1 ? PartyContext.InGroup : PartyContext.Solo;
    }
}
