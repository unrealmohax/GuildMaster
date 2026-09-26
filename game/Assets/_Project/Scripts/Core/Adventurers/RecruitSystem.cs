using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 13 такта: кандидаты в авантюристы. Каждый такт — уход кандидатов без ответа
    /// (через <c>candidateWaitDays</c>); раз в сутки, утром (начало утра — время прихода), шанс нового кандидата,
    /// если людей вместе с ожидающими меньше <c>maxAdventurers</c>. Ответ игрока — <see cref="AcceptCandidateCommand"/>,
    /// <see cref="RejectCandidateCommand"/>.
    /// </summary>
    public sealed class RecruitSystem : ISimSystem
    {
        public string Name => nameof(RecruitSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;

            ExpireCandidates(ctx);

            if (ctx.World.Time.Hour != ctx.Data.Balance.Time.MorningHour) return;
            if (ctx.World.Adventurers.HeadCount >= ctx.Data.Balance.Guild.MaxAdventurers) return;
            if (!ctx.Rng.Chance(CandidateChance(ctx.Data))) return;

            AddCandidate(ctx);
        }

        /// <summary>
        /// Шанс кандидата в сутки: <c>influxBaseChance + репутация × influxChancePerReputation</c> × распоряжение.
        /// Репутация — стартовая, множитель распоряжений — 1.
        /// </summary>
        public static float CandidateChance(DataRegistry data)
        {
            GuildBalance guild = data.Balance.Guild;
            float reputation = guild.StartReputation;   // репутации в мире пока нет — стартовая
            const float decreeMultiplier = 1f;          // распоряжений пока нет
            return (guild.InfluxBaseChance + reputation * guild.InfluxChancePerReputation) * decreeMultiplier;
        }

        private static void AddCandidate(SimContext ctx)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active) names.Add(adventurer.Name);
            foreach (Candidate waiting in ctx.World.Adventurers.Candidates) names.Add(waiting.Adventurer.Name);

            GeneratedAdventurer generated = AdventurerGenerator.Generate(ctx.Rng, ctx.Data, ctx.World, ctx.World.Ids.Next(), atStart: false, names);
            Adventurer candidate = generated.Adventurer;

            long now = ctx.World.Time.TotalHours;
            long expires = now + ctx.Calendar.DaysToHours(ctx.Data.Balance.Adventurers.CandidateWaitDays);
            ctx.World.Adventurers.AddCandidate(new Candidate(candidate, now, expires));

            ctx.Events.Publish(SimEventType.CandidateArrived, EventImportance.Normal, candidate.Id)
                .With("archetype", candidate.ArchetypeId)
                .With("expiresAt", expires);
        }

        private static void ExpireCandidates(SimContext ctx)
        {
            IReadOnlyList<Candidate> candidates = ctx.World.Adventurers.Candidates;
            long now = ctx.World.Time.TotalHours;
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                Candidate candidate = candidates[i];
                if (now < candidate.ExpiresAtHours) continue;

                ctx.World.Adventurers.RemoveCandidate(candidate);
                ctx.Events.Publish(SimEventType.CandidateLeft, EventImportance.Normal, candidate.Adventurer.Id);
            }
        }
    }

    /// <summary>
    /// Принять кандидата: вступает в гильдию сейчас. Черта с партнёром (Соперник, Влюблённый) появляется и у партнёра,
    /// если тот ещё в гильдии и может её получить; иначе черта у новичка снимается. Нет такого кандидата — ничего.
    /// </summary>
    public sealed class AcceptCandidateCommand : ICommand
    {
        public AcceptCandidateCommand(int candidateId)
        {
            CandidateId = candidateId;
        }

        public int CandidateId { get; }

        public void Apply(SimContext ctx)
        {
            AdventurerRoster roster = ctx.World.Adventurers;
            if (!roster.TryGetCandidate(CandidateId, out Candidate candidate)) return;

            Adventurer adventurer = candidate.Adventurer;
            roster.RemoveCandidate(candidate);
            adventurer.JoinedAtHours = ctx.World.Time.TotalHours;
            adventurer.Housing = Housing.City; // Общежития пока нет
            LinkPartners(ctx, adventurer);
            roster.AddActive(adventurer);

            ctx.Events.Publish(SimEventType.AdventurerJoined, EventImportance.Normal, adventurer.Id)
                .With("archetype", adventurer.ArchetypeId);
        }

        private static void LinkPartners(SimContext ctx, Adventurer adventurer)
        {
            var traits = new List<TraitInstance>(adventurer.Traits);
            foreach (TraitInstance trait in traits)
            {
                if (trait.PartnerId == 0) continue;

                SpecialTraitDefinition definition = ctx.Data.Get<SpecialTraitDefinition>(trait.TraitId);
                if (ctx.World.Adventurers.TryGetActive(trait.PartnerId, out Adventurer partner) && TraitRules.CanAdd(partner, definition, ctx.Data))
                    TraitService.AddAtGeneration(partner, definition, ctx.World.Time.TotalHours, adventurer.Id);
                else
                    adventurer.RemoveTrait(trait);
            }
        }
    }

    /// <summary>Отказать кандидату. Нет такого кандидата — ничего.</summary>
    public sealed class RejectCandidateCommand : ICommand
    {
        public RejectCandidateCommand(int candidateId)
        {
            CandidateId = candidateId;
        }

        public int CandidateId { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.World.Adventurers.TryGetCandidate(CandidateId, out Candidate candidate)) return;

            ctx.World.Adventurers.RemoveCandidate(candidate);
            ctx.Events.Publish(SimEventType.CandidateRejected, EventImportance.Normal, CandidateId);
        }
    }
}
