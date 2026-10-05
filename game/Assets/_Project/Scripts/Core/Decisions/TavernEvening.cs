using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Итоги вечера в таверне (в начале ночи). Кто провёл в таверне хотя бы час вечера (запой не считается), — по парам в порядке
    /// списка людей: отношения + <c>tavernEveningRelation</c>; затем, если отношения ниже <c>quarrelRelationBelow</c> или пара —
    /// Соперники, шанс ссоры <c>quarrelChance</c>: отношения + <c>quarrelRelation</c>, событие <see cref="SimEventType.Quarrel"/> [З].
    /// Ссора Соперников раскрывает черту у обоих («Ссора»). Числа — <see cref="AdventurersBalance"/>. Если гильдия устроила праздник
    /// (ответ на обращение Трактирщика), его эффекты получают те, кто был в таверне этим вечером.
    /// </summary>
    public static class TavernEvening
    {
        public static void Settle(SimContext ctx)
        {
            var there = new List<Adventurer>();
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                if (adventurer.State.InTavernThisEvening) there.Add(adventurer);
                adventurer.State.InTavernThisEvening = false;
            }
            if (there.Count > 0) ctx.Log.Write(SimLogLevel.Debug, "tavern evening: {0} people, {1} pairs", there.Count, there.Count * (there.Count - 1) / 2);

            // Черты Соперника — один раз на вечер, а не на каждую пару.
            var rivalTraits = new Dictionary<int, List<TraitInstance>>();
            foreach (Adventurer adventurer in there)
            {
                foreach (TraitInstance trait in adventurer.Traits)
                {
                    if (trait.PartnerId == 0 || !ctx.Data.Get<SpecialTraitDefinition>(trait.TraitId).HasHook(TraitHook.RivalPartner)) continue;
                    if (!rivalTraits.TryGetValue(adventurer.Id, out List<TraitInstance> list)) rivalTraits[adventurer.Id] = list = new List<TraitInstance>();
                    list.Add(trait);
                }
            }

            AdventurersBalance balance = ctx.Data.Balance.Adventurers;
            for (int i = 0; i < there.Count; i++)
            {
                for (int j = i + 1; j < there.Count; j++)
                {
                    Adventurer a = there[i];
                    Adventurer b = there[j];
                    float relation = RelationService.Change(ctx, a.Id, b.Id, balance.TavernEveningRelation);

                    TraitInstance rivalA = rivalTraits.Count > 0 ? RivalOf(rivalTraits, a, b) : null;
                    TraitInstance rivalB = rivalTraits.Count > 0 ? RivalOf(rivalTraits, b, a) : null;
                    bool rivals = rivalA != null || rivalB != null;
                    if (!rivals && relation >= balance.QuarrelRelationBelow) continue;
                    if (!ctx.RollChance(balance.QuarrelChance, "quarrel", a, "relation", relation)) continue;

                    relation = RelationService.Change(ctx, a.Id, b.Id, balance.QuarrelRelation);
                    ctx.Events.Publish(SimEventType.Quarrel, EventImportance.Notable, a.Id, b.Id)
                        .With("relation", relation)
                        .With("rivals", rivals);
                    if (rivalA != null) RevealService.TryRevealTrait(ctx, a, rivalA.TraitId, RevealTrigger.RivalFirstClash);
                    if (rivalB != null) RevealService.TryRevealTrait(ctx, b, rivalB.TraitId, RevealTrigger.RivalFirstClash);
                }
            }

            DilemmaService.ApplyFeast(ctx, there);
        }

        /// <summary>Черта Соперника у <paramref name="adventurer"/>, где партнёр — <paramref name="other"/>; нет — null.</summary>
        private static TraitInstance RivalOf(Dictionary<int, List<TraitInstance>> rivalTraits, Adventurer adventurer, Adventurer other)
        {
            if (!rivalTraits.TryGetValue(adventurer.Id, out List<TraitInstance> traits)) return null;
            foreach (TraitInstance trait in traits)
            {
                if (trait.PartnerId == other.Id) return trait;
            }
            return null;
        }
    }
}
