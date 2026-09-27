using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Люди задания и события о нём. Кто сейчас идёт (живые, не сбежавшие), кто решает за группу, кто принимает удар,
    /// название группы для ленты, публикация события задания со всеми данными для строк.
    /// </summary>
    public static class QuestParty
    {
        /// <summary>Кто идёт сейчас, по порядку выхода.</summary>
        public static List<Adventurer> Present(WorldState world, QuestRun run)
        {
            var present = new List<Adventurer>(run.Members.Count);
            foreach (int id in run.Members)
            {
                if (world.Adventurers.TryGetActive(id, out Adventurer adventurer)) present.Add(adventurer);
            }
            return present;
        }

        /// <summary>Решающий: высший ранг гильдии, при равенстве — выше Хладнокровие, дальше — кто раньше вышел.</summary>
        public static Adventurer Leader(IReadOnlyList<Adventurer> present, DataRegistry data)
        {
            Adventurer best = null;
            float bestComposure = 0f;
            foreach (Adventurer member in present)
            {
                float composure = AdventurerStats.Effective(member, StatId.Composure, data);
                if (best == null || member.GuildRank > best.GuildRank || (member.GuildRank == best.GuildRank && composure > bestComposure))
                {
                    best = member;
                    bestComposure = composure;
                }
            }
            return best;
        }

        /// <summary>
        /// Кто принимает удар: сначала бросившийся вперёд, иначе самый выносливый из не тяжело раненых (все тяжело ранены —
        /// самый выносливый). При равенстве — кто раньше вышел.
        /// </summary>
        public static Adventurer Shield(IReadOnlyList<Adventurer> present, QuestRun run, DataRegistry data)
        {
            foreach (Adventurer member in present)
            {
                if (Contains(run.Rushing, member.Id)) return member;
            }
            Adventurer best = MostEnduring(present, data, skipHeavy: true);
            return best ?? MostEnduring(present, data, skipHeavy: false);
        }

        /// <summary>Самый тяжело раненый: тяжёлая рана, затем лёгкая, затем без ран; при равенстве — с меньшей Выносливостью.</summary>
        public static Adventurer MostWounded(IReadOnlyList<Adventurer> present, DataRegistry data)
        {
            Adventurer worst = null;
            int worstLevel = -1;
            float worstEndurance = 0f;
            foreach (Adventurer member in present)
            {
                int level = member.State.HasHeavyWound() ? 2 : member.State.HasLightWound() ? 1 : 0;
                float endurance = AdventurerStats.Effective(member, StatId.Endurance, data);
                if (worst == null || level > worstLevel || (level == worstLevel && endurance < worstEndurance))
                {
                    worst = member;
                    worstLevel = level;
                    worstEndurance = endurance;
                }
            }
            return worst;
        }

        /// <summary>Лучший по параметру; при равенстве — кто раньше вышел.</summary>
        public static Adventurer Best(IReadOnlyList<Adventurer> present, StatId stat, DataRegistry data, int exceptId = 0)
        {
            Adventurer best = null;
            float bestValue = 0f;
            foreach (Adventurer member in present)
            {
                if (member.Id == exceptId) continue;
                float value = AdventurerStats.Effective(member, stat, data);
                if (best == null || value > bestValue)
                {
                    best = member;
                    bestValue = value;
                }
            }
            return best;
        }

        /// <summary>
        /// Название группы для <c>{группа}</c>: «группа Яна» — по решающему, в шести падежах, женский род. Имя без падежей
        /// в списке имён — не склоняется.
        /// </summary>
        public static TextValue PartyName(Adventurer leader, DataRegistry data)
        {
            if (leader == null) return TextValue.Word("группа", GrammaticalGender.Feminine);
            NounForms name = data.NameForms(leader.Name);
            string genitive = name != null && !string.IsNullOrEmpty(name.Get(GrammaticalCase.Genitive)) ? name.Get(GrammaticalCase.Genitive) : leader.Name;
            return TextValue.Noun(new NounForms("группа " + genitive, "группы " + genitive, "группе " + genitive, "группу " + genitive,
                "группой " + genitive, "группе " + genitive, GrammaticalGender.Feminine));
        }

        /// <summary>Название группы задания: постоянная — «группа «Серые волки»», иначе — по решающему.</summary>
        public static TextValue PartyName(QuestRun run, Adventurer leader, DataRegistry data) =>
            run.IsPermanentParty && !string.IsNullOrEmpty(run.PartyTitle) ? PartyService.NameValue(run.PartyTitle) : PartyName(leader, data);

        /// <summary>
        /// Событие задания: участники — люди события; в данных — задание, заказ, тип, ранг, расстояние, «один ли», названия,
        /// группа и решающий.
        /// </summary>
        public static SimEvent Publish(SimContext ctx, SimEventType type, QuestRun run, EventImportance importance, params int[] participants) =>
            Publish(ctx, type, run, importance, run.Enemy, participants);

        /// <summary>То же, но противник — свой (засада, звери), а не противник заказа.</summary>
        public static SimEvent Publish(SimContext ctx, SimEventType type, QuestRun run, EventImportance importance, NounForms enemy,
            params int[] participants)
        {
            List<Adventurer> present = Present(ctx.World, run);
            Adventurer leader = Leader(present, ctx.Data);
            if (leader == null && participants.Length > 0) ctx.World.Adventurers.TryGetKnown(participants[0], out leader);

            SimEvent simEvent = ctx.Events.Publish(type, importance, participants)
                .With("quest", run.Id)
                .With("order", run.OrderId)
                .With("questType", run.TypeId)
                .With("rank", run.Rank)
                .With("distance", run.Distance)
                .With("solo", run.Departed.Count == 1)
                .With("party", PartyName(run, leader, ctx.Data));
            if (run.Place != null) simEvent.With("place", run.Place);
            if (enemy != null) simEvent.With("enemy", enemy);
            if (run.Client != null) simEvent.With("client", run.Client);
            if (run.Cargo != null) simEvent.With("cargo", run.Cargo);
            if (leader != null) simEvent.With("leader", leader.Id);
            return simEvent;
        }

        /// <summary>Контекст для эффектов черт «в группе» / «один»: по тому, сколько людей идёт сейчас.</summary>
        public static PartyContext ContextOf(int presentCount) => presentCount > 1 ? PartyContext.InGroup : PartyContext.Solo;

        internal static bool Contains(IReadOnlyList<int> ids, int id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return true;
            }
            return false;
        }

        private static Adventurer MostEnduring(IReadOnlyList<Adventurer> present, DataRegistry data, bool skipHeavy)
        {
            Adventurer best = null;
            float bestValue = 0f;
            foreach (Adventurer member in present)
            {
                if (skipHeavy && member.State.HasHeavyWound()) continue;
                float value = AdventurerStats.Effective(member, StatId.Endurance, data);
                if (best == null || value > bestValue)
                {
                    best = member;
                    bestValue = value;
                }
            }
            return best;
        }
    }
}
