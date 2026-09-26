using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Стартовая шестёрка (ТЗ 04 → «Старт», решения 2026-09-26). Выполняется один раз при создании симуляции,
    /// со своим потоком случайных чисел <see cref="StreamName"/>: одно зерно — один и тот же состав. Событий не публикует:
    /// стартовое состояние — не новость. Реестр без определений (тесты времени) — людей нет.
    /// <list type="number">
    /// <item>6 человек по общей схеме, уровень не выше <c>startMaxLevel</c>, без черт.</item>
    /// <item>1–2 человека — ранг F (Новичок не выбирается; не хватает не-Новичков — состав заново).</item>
    /// <item>Разнообразие: пока на каком-то крайнем полюсе больше двух человек — оси последнего из них заново.</item>
    /// <item>Черт 2–3 на всех, включая черты готовых пар; 1–2 пары: старые друзья, соперники или влюблённые.
    /// Пара с чертой, которая не влезает в итог, становится старыми друзьями. Остаток — по черте «от рождения» другим.</item>
    /// </list>
    /// </summary>
    public static class StartScenario
    {
        public const string StreamName = nameof(StartScenario);

        private const int MaxAttempts = 1000;

        private enum PairKind
        {
            OldFriends,
            Rivals,
            Lovers,
        }

        public static void Apply(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;

            DataRegistry data = ctx.Data;
            AdventurersBalance balance = data.Balance.Adventurers;
            Rng rng = ctx.Rng;
            WorldState world = ctx.World;
            long now = world.Time.TotalHours;

            List<GeneratedAdventurer> lineup = GenerateLineup(ctx, out List<int> rankF);
            foreach (int index in rankF) AdventurerGenerator.ApplyRankF(data, lineup[index]);

            var people = new List<Adventurer>(lineup.Count);
            foreach (GeneratedAdventurer generated in lineup) people.Add(generated.Adventurer);

            EnsureDiversity(rng, balance, people);

            // Люди вступают до черт и отношений: черты с партнёром и отношения ссылаются на Id.
            foreach (Adventurer adventurer in people) world.Adventurers.AddActive(adventurer);

            int traitsTotal = rng.RangeInclusive(balance.StartTraitsTotal.Min, balance.StartTraitsTotal.Max);
            int traitsGiven = MakePairs(ctx, people, traitsTotal, now, out HashSet<Adventurer> inTraitPair);

            var free = new List<Adventurer>();
            foreach (Adventurer adventurer in people)
            {
                if (!inTraitPair.Contains(adventurer) && adventurer.Traits.Count < balance.BirthTraitsAtStart.Max) free.Add(adventurer);
            }
            while (traitsGiven < traitsTotal && free.Count > 0)
            {
                Adventurer adventurer = free[rng.Range(0, free.Count)];
                free.Remove(adventurer);
                if (AdventurerGenerator.AddBirthTrait(rng, data, world, adventurer, now, allowPartner: false)) traitsGiven++;
            }
        }

        private static List<GeneratedAdventurer> GenerateLineup(SimContext ctx, out List<int> rankF)
        {
            AdventurersBalance balance = ctx.Data.Balance.Adventurers;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var lineup = new List<GeneratedAdventurer>(balance.StartAdventurers);
                var names = new HashSet<string>(StringComparer.Ordinal);
                int firstId = ctx.World.Ids.LastIssued + 1;
                for (int i = 0; i < balance.StartAdventurers; i++)
                {
                    GeneratedAdventurer generated = AdventurerGenerator.Generate(ctx.Rng, ctx.Data, ctx.World, firstId + i, atStart: true, names);
                    names.Add(generated.Adventurer.Name);
                    lineup.Add(generated);
                }

                var eligible = new List<int>();
                for (int i = 0; i < lineup.Count; i++)
                {
                    if (lineup[i].Type.Kind != ArchetypeKind.Novice) eligible.Add(i);
                }

                int rankFCount = ctx.Rng.RangeInclusive(balance.StartRankF.Min, balance.StartRankF.Max);
                if (eligible.Count < rankFCount) continue;

                rankF = new List<int>(rankFCount);
                for (int i = 0; i < rankFCount; i++)
                {
                    int index = eligible[ctx.Rng.Range(0, eligible.Count)];
                    eligible.Remove(index);
                    rankF.Add(index);
                }

                // Id выдаются только принятому составу — по порядку, как их взял генератор.
                foreach (GeneratedAdventurer generated in lineup)
                {
                    int id = ctx.World.Ids.Next();
                    if (id != generated.Adventurer.Id) throw new InvalidOperationException($"Start id mismatch: {id} != {generated.Adventurer.Id}");
                }
                return lineup;
            }
            throw new InvalidOperationException($"No start lineup with enough non-novices for rank F in {MaxAttempts} attempts");
        }

        /// <summary>Пока на одном крайнем полюсе больше <c>maxStartPerExtremePole</c> человек — оси последнего из них заново.</summary>
        private static void EnsureDiversity(Rng rng, AdventurersBalance balance, List<Adventurer> people)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                Adventurer crowded = FindCrowdedPole(balance, people);
                if (crowded == null) return;
                AdventurerGenerator.RerollAxes(rng, balance, crowded);
            }
            throw new InvalidOperationException($"Start axes are not diverse after {MaxAttempts} rerolls");
        }

        private static Adventurer FindCrowdedPole(AdventurersBalance balance, List<Adventurer> people)
        {
            for (int axis = 0; axis < Vocabulary.AxisCount; axis++)
            {
                foreach (AxisPole pole in new[] { AxisPole.Negative, AxisPole.Positive })
                {
                    int count = 0;
                    Adventurer last = null;
                    foreach (Adventurer adventurer in people)
                    {
                        if (!AxisMath.IsOnExtremePole(adventurer.GetAxis((AxisId)axis), pole, balance)) continue;
                        count++;
                        last = adventurer;
                    }
                    if (count > balance.MaxStartPerExtremePole) return last;
                }
            }
            return null;
        }

        /// <summary>Готовые пары. Возвращает, сколько черт раздали парам.</summary>
        private static int MakePairs(SimContext ctx, List<Adventurer> people, int traitsTotal, long now, out HashSet<Adventurer> inTraitPair)
        {
            AdventurersBalance balance = ctx.Data.Balance.Adventurers;
            Rng rng = ctx.Rng;
            SpecialTraitDefinition rival = TraitRules.FindByHook(ctx.Data, TraitHook.RivalPartner);
            SpecialTraitDefinition lover = TraitRules.FindByHook(ctx.Data, TraitHook.LoverPartner);

            inTraitPair = new HashSet<Adventurer>();
            var unpaired = new List<Adventurer>(people);
            int pairs = rng.RangeInclusive(balance.StartPairs.Min, balance.StartPairs.Max);
            int traitsGiven = 0;

            for (int p = 0; p < pairs && unpaired.Count >= 2; p++)
            {
                Adventurer first = unpaired[rng.Range(0, unpaired.Count)];
                unpaired.Remove(first);
                Adventurer second = unpaired[rng.Range(0, unpaired.Count)];
                unpaired.Remove(second);

                var kind = (PairKind)rng.Range(0, 3);
                SpecialTraitDefinition trait = kind == PairKind.Rivals ? rival : kind == PairKind.Lovers ? lover : null;
                if (trait == null || traitsGiven + 2 > traitsTotal)
                {
                    RelationService.Set(ctx, first.Id, second.Id, balance.OldFriendsRelation);
                    continue;
                }

                TraitService.AddAtGeneration(first, trait, now, second.Id);
                TraitService.AddAtGeneration(second, trait, now, first.Id);
                inTraitPair.Add(first);
                inTraitPair.Add(second);
                traitsGiven += 2;
            }
            return traitsGiven;
        }
    }
}
