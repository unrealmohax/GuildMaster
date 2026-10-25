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
            if (!ctx.RollChance(CandidateChance(ctx.Data, ctx.World), "candidate")) return;

            AddCandidate(ctx);
        }

        /// <summary>
        /// Шанс кандидата в сутки: <c>(influxBaseChance + репутация × influxChancePerReputation)</c> × множитель населения
        /// (<see cref="PopulationMultiplier"/>) × молва (<see cref="Rumor"/>) × множитель распоряжений
        /// (<see cref="DecreeRules.CandidateChanceMultiplier"/>).
        /// </summary>
        public static float CandidateChance(DataRegistry data, WorldState world) =>
            CandidateChance(data, world.Guild.Reputation, DecreeRules.CandidateChanceMultiplier(world, data),
                PopulationMultiplier(data, world.Settlement.Population), Rumor(data, world));

        /// <summary>Шанс кандидата в сутки при репутации <paramref name="reputation"/> и множителях распоряжений, населения и молвы.</summary>
        public static float CandidateChance(DataRegistry data, float reputation, float decreeMultiplier = 1f, float populationMultiplier = 1f,
            float rumor = 1f)
        {
            GuildBalance guild = data.Balance.Guild;
            return (guild.InfluxBaseChance + reputation * guild.InfluxChancePerReputation) * populationMultiplier * rumor * decreeMultiplier;
        }

        /// <summary>
        /// Множитель населения: население / стартовое население, не больше <c>influxPopulationCap</c>. Пустеет поселение — некому
        /// идти в гильдию.
        /// </summary>
        public static float PopulationMultiplier(DataRegistry data, int population)
        {
            int start = data.Balance.Settlement.StartPopulation;
            if (start <= 0) return 1f;
            return Math.Min(data.Balance.Guild.InfluxPopulationCap, (float)population / start);
        }

        /// <summary>
        /// Молва — что люди гильдии рассказывают о ней в поселении: <c>rumorBase</c> + среднее довольство людей гильдии /
        /// <c>rumorDivisor</c>, в пределах 0..<c>rumorMax</c>. Недовольные люди могут погасить приток совсем. Людей нет — × 1.
        /// </summary>
        public static float Rumor(DataRegistry data, WorldState world)
        {
            IReadOnlyList<Adventurer> active = world.Adventurers.Active;
            if (active.Count == 0) return 1f;

            float sum = 0f;
            foreach (Adventurer adventurer in active) sum += adventurer.State.Contentment;
            GuildBalance guild = data.Balance.Guild;
            return Math.Max(0f, Math.Min(guild.RumorMax, guild.RumorBase + sum / active.Count / guild.RumorDivisor));
        }

        /// <summary>Подъёмные кандидату по его уровню (у Новичка уровень всегда слабый).</summary>
        public static int SigningBonus(DataRegistry data, AdventurerLevel level)
        {
            GuildBalance guild = data.Balance.Guild;
            switch (level)
            {
                case AdventurerLevel.Strong: return guild.SigningBonusStrong;
                case AdventurerLevel.Medium: return guild.SigningBonusMedium;
                default: return guild.SigningBonusWeak;
            }
        }

        /// <summary>Подъёмные этому кандидату.</summary>
        public static int SigningBonus(DataRegistry data, Candidate candidate) => SigningBonus(data, candidate.Level);

        /// <summary>Хватает ли в казне денег на подъёмные кандидату.</summary>
        public static bool CanAffordSigningBonus(DataRegistry data, WorldState world, Candidate candidate) =>
            TreasuryService.CanAfford(world, SigningBonus(data, candidate));

        private static void AddCandidate(SimContext ctx)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active) names.Add(adventurer.Name);
            foreach (Candidate waiting in ctx.World.Adventurers.Candidates) names.Add(waiting.Adventurer.Name);

            GeneratedAdventurer generated = AdventurerGenerator.Generate(ctx.Rng, ctx.Data, ctx.World, ctx.World.Ids.Next(), atStart: false, names,
                workAxisShift: DecreeRules.CandidateWorkShift(ctx.World, ctx.Data));
            Adventurer candidate = generated.Adventurer;

            long now = ctx.World.Time.TotalHours;
            long expires = now + ctx.Calendar.DaysToHours(ctx.Data.Balance.Adventurers.CandidateWaitDays);
            ctx.World.Adventurers.AddCandidate(new Candidate(candidate, generated.Level, now, expires));
            AdventurerLog.WriteGenerated(ctx, "candidate", candidate, generated.Type, generated.Level);

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
    /// Принять кандидата: гильдия платит подъёмные по его уровню (из казны в кошелёк, статья
    /// <see cref="LedgerCategories.SigningBonuses"/>) — денег меньше, команда отклоняется: событие
    /// <see cref="SimEventType.CandidateAcceptRefused"/> с причиной <c>money</c>, кандидат ждёт дальше. Иначе вступает в гильдию сейчас. Черта с партнёром (Соперник, Влюблённый) появляется и у партнёра,
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
            int bonus = RecruitSystem.SigningBonus(ctx.Data, candidate);
            if (!TreasuryService.CanAfford(ctx.World, bonus))
            {
                ctx.Events.Publish(SimEventType.CandidateAcceptRefused, EventImportance.Normal, adventurer.Id)
                    .With("reason", RefusedForMoney)
                    .With("amount", bonus)
                    .With("money", ctx.World.Treasury.Money);
                return;
            }

            if (bonus > 0)
            {
                TreasuryService.Debit(ctx, LedgerCategories.SigningBonuses, bonus, candidate.Level.ToString(), adventurer.Id);
                WalletService.Give(adventurer, bonus);
            }
            roster.RemoveCandidate(candidate);
            adventurer.JoinedAtHours = ctx.World.Time.TotalHours;
            adventurer.Housing = Housing.City; // в Общежитие переселяет BuildingSystem, когда есть место
            LinkPartners(ctx, adventurer);
            roster.AddActive(adventurer);

            ctx.Events.Publish(SimEventType.AdventurerJoined, EventImportance.Normal, adventurer.Id)
                .With("archetype", adventurer.ArchetypeId)
                .With("amount", bonus);
        }

        /// <summary>Причина отказа в приёме: в казне меньше подъёмных.</summary>
        public const string RefusedForMoney = "money";

        private static void LinkPartners(SimContext ctx, Adventurer adventurer)
        {
            var traits = new List<TraitInstance>(adventurer.Traits);
            foreach (TraitInstance trait in traits)
            {
                if (trait.PartnerId == 0) continue;

                SpecialTraitDefinition definition = ctx.Data.Get<SpecialTraitDefinition>(trait.TraitId);
                if (ctx.World.Adventurers.TryGetActive(trait.PartnerId, out Adventurer partner) && TraitRules.CanAdd(partner, definition, ctx.Data))
                {
                    TraitService.AddAtGeneration(partner, definition, ctx.World.Time.TotalHours, adventurer.Id);
                    FamilyGenerator.FitToTraits(ctx, partner);
                }
                else
                {
                    adventurer.RemoveTrait(trait);
                }
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
