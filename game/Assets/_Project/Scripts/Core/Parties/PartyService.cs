using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Жизнь групп вне решений: группа идёт на задание и распадается после него, постоянная группа складывается, принимает
    /// гостя в члены, теряет людей и распадается. Числа — <see cref="DecisionsBalance"/> (раздел «Постоянные группы»).
    /// <list type="bullet">
    /// <item>Складывается: после выполненного задания из его вернувшихся участников без постоянной группы — наибольший набор
    /// (от двух), где у каждой пары не меньше <c>permanentPartySuccesses</c> совместных успехов и отношения не ниже
    /// <c>permanentPartyMinRelation</c>. Название — прилагательное и существительное из списка имён, без повторов, пока есть
    /// свободные сочетания; бросок — свой поток <see cref="NameStream"/>.</item>
    /// <item>Гость, который выполнил с постоянной группой <c>newMemberSuccesses</c> заданий и не состоит в своей, — член.</item>
    /// <item>Теряет людей: отношения двух членов не выше <c>permanentPartyBreakRelation</c> — уходит тот, кто хуже относится
    /// к остальным (среднее отношений); одиночка (ось Люди на крайнем отрицательном полюсе) — после
    /// <c>lonerLeavesAfterQuests</c> совместных заданий; погибший и ушедший из гильдии. Остался один — группа распалась.</item>
    /// </list>
    /// </summary>
    public static class PartyService
    {
        /// <summary>Поток случайных чисел для названий постоянных групп.</summary>
        public const string NameStream = "Parties";

        /// <summary>«группа «Серые волки»» в шести падежах, женский род — для <c>{группа}</c>.</summary>
        public static TextValue NameValue(string title)
        {
            string quoted = "«" + title + "»";
            return TextValue.Noun(new NounForms("группа " + quoted, "группы " + quoted, "группе " + quoted, "группу " + quoted,
                "группой " + quoted, "группе " + quoted, GrammaticalGender.Feminine));
        }

        /// <summary>Название группы для строк и лога: постоянная — по названию, под задание — по инициатору.</summary>
        public static TextValue NameOf(SimContext ctx, Party party)
        {
            if (party.IsPermanent) return NameValue(party.Name);
            ctx.World.Adventurers.TryGetKnown(party.InitiatorId, out Adventurer initiator);
            return QuestParty.PartyName(initiator, ctx.Data);
        }

        // ---------- Под задание ----------

        /// <summary>Новая группа под заказ: инициатор — первый член.</summary>
        internal static Party StartGathering(SimContext ctx, Adventurer initiator, Order order)
        {
            Party party = ctx.World.Parties.Create(false, initiator.Id);
            party.OrderId = order.Id;
            party.AddMember(initiator.Id);
            initiator.PartyId = party.Id;
            return party;
        }

        /// <summary>
        /// Группа не пошла или эти люди с ней не идут: снять с них группу и взятый заказ. Группа под задание, от которой никого не
        /// осталось, удаляется; у постоянной забывается заказ.
        /// </summary>
        internal static void Release(SimContext ctx, Party party, IReadOnlyList<Adventurer> people)
        {
            foreach (Adventurer person in people)
            {
                if (party == null || person.PartyId == party.Id) person.PartyId = 0;
                person.State.PlannedOrderId = 0;
            }
            if (party == null) return;
            if (party.IsPermanent) party.OrderId = 0;
            else if (!HasAnyone(ctx, party)) ctx.World.Parties.Remove(party);
        }

        /// <summary>
        /// Задание начинается: отметить группу в задании. Идёт один — задание без группы (группа под задание удаляется).
        /// </summary>
        internal static void Attach(SimContext ctx, QuestRun run, Party party, IReadOnlyList<Adventurer> going)
        {
            if (party == null) return;
            if (going.Count < 2)
            {
                foreach (Adventurer person in going) person.PartyId = 0;
                if (party.IsPermanent) party.OrderId = 0;
                else ctx.World.Parties.Remove(party);
                return;
            }
            run.PartyId = party.Id;
            run.IsPermanentParty = party.IsPermanent;
            run.PartyTitle = party.IsPermanent ? party.Name : string.Empty;
            foreach (Adventurer person in going) person.PartyId = party.Id;
        }

        /// <summary>
        /// Задание кончилось: группа под задание распадается; постоянная считает задание и успех, гости копят успехи, одиночки
        /// уходят; выполнено — может сложиться новая постоянная группа.
        /// </summary>
        public static void AfterQuest(SimContext ctx, QuestRun run, IReadOnlyList<Adventurer> returned)
        {
            if (run.PartyId != 0 && ctx.World.Parties.TryGetParty(run.PartyId, out Party party))
            {
                if (party.IsPermanent) CountPermanentQuest(ctx, party, run, returned);
                else ctx.World.Parties.Remove(party);
            }
            foreach (int id in run.Departed)
            {
                if (ctx.World.Adventurers.TryGetKnown(id, out Adventurer member) && member.PartyId == run.PartyId) member.PartyId = 0;
            }
            if (run.Completed && !run.IsPromotion) TryForm(ctx, returned);
        }

        private static bool HasAnyone(SimContext ctx, Party party)
        {
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                if (adventurer.PartyId == party.Id) return true;
            }
            return false;
        }

        // ---------- Постоянные группы ----------

        private static void CountPermanentQuest(SimContext ctx, Party party, QuestRun run, IReadOnlyList<Adventurer> returned)
        {
            DecisionsBalance decisions = ctx.Data.Balance.Decisions;
            party.OrderId = 0;
            party.JointQuests++;
            if (run.Completed) party.JointSuccesses++;

            foreach (Adventurer person in returned)
            {
                if (party.HasMember(person.Id))
                {
                    party.AddQuestTogether(person.Id);
                    continue;
                }
                if (!run.Completed || person.PermanentPartyId != 0) continue;
                int successes = party.AddGuestSuccess(person.Id);
                if (successes < decisions.NewMemberSuccesses) continue;
                Join(ctx, party, person);
            }

            AdventurersBalance people = ctx.Data.Balance.Adventurers;
            foreach (Adventurer person in new List<Adventurer>(returned))
            {
                if (!party.HasMember(person.Id) || !ctx.World.Parties.TryGetParty(party.Id, out _)) continue;
                if (!AxisMath.IsOnExtremePole(person.GetAxis(AxisId.People), AxisPole.Negative, people)) continue;
                if (party.GetQuestsTogether(person.Id) < decisions.LonerLeavesAfterQuests) continue;
                Leave(ctx, party, person, "loner", null);
            }
            if (ctx.World.Parties.TryGetParty(party.Id, out _)) CheckQuarrels(ctx, party);
        }

        private static void Join(SimContext ctx, Party party, Adventurer person)
        {
            party.AddMember(person.Id);
            person.PermanentPartyId = party.Id;
            ctx.Log.Write(SimLogLevel.Info, "party #{0} {1}: #{2} {3} joined", party.Id, party.Name, person.Id, person.Name);
            ctx.Events.Publish(SimEventType.PartyMemberJoined, EventImportance.Normal, person.Id)
                .With("party", NameValue(party.Name)).With("partyId", party.Id);
        }

        /// <summary>
        /// Сложить постоянную группу из вернувшихся с выполненного задания: наибольший набор людей без постоянной группы, где
        /// каждая пара выполнила вместе не меньше <c>permanentPartySuccesses</c> заданий и относится друг к другу не хуже
        /// <c>permanentPartyMinRelation</c>. При равном размере — первый в переборе по битовой маске участников.
        /// </summary>
        internal static Party TryForm(SimContext ctx, IReadOnlyList<Adventurer> returned)
        {
            var free = new List<Adventurer>();
            foreach (Adventurer person in returned)
            {
                if (person.PermanentPartyId == 0 && ctx.World.Adventurers.IsActive(person.Id)) free.Add(person);
            }
            int max = Math.Min(free.Count, ctx.Data.Balance.Rounds.MaxPartySize);
            if (max < 2) return null;

            List<Adventurer> best = null;
            int subsets = 1 << free.Count;
            for (int mask = 1; mask < subsets; mask++)
            {
                int size = CountBits(mask);
                if (size < 2 || size > max || (best != null && size <= best.Count)) continue;
                var set = new List<Adventurer>(size);
                for (int i = 0; i < free.Count; i++)
                {
                    if ((mask & (1 << i)) != 0) set.Add(free[i]);
                }
                if (IsTight(ctx, set)) best = set;
            }
            return best == null ? null : Form(ctx, best);
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        private static bool IsTight(SimContext ctx, List<Adventurer> set)
        {
            DecisionsBalance decisions = ctx.Data.Balance.Decisions;
            RelationBook relations = ctx.World.Relations;
            for (int i = 0; i < set.Count; i++)
            {
                for (int j = i + 1; j < set.Count; j++)
                {
                    if (relations.GetJointSuccesses(set[i].Id, set[j].Id) < decisions.PermanentPartySuccesses) return false;
                    if (relations.GetValue(set[i].Id, set[j].Id) < decisions.PermanentPartyMinRelation) return false;
                }
            }
            return true;
        }

        internal static Party Form(SimContext ctx, List<Adventurer> members)
        {
            PartyBook book = ctx.World.Parties;
            Party party = book.Create(true, members[0].Id);
            party.Name = PickName(ctx);
            party.FormedAtHours = ctx.World.Time.TotalHours;
            book.UseName(party.Name);
            book.PermanentFormed++;
            foreach (Adventurer member in members)
            {
                party.AddMember(member.Id);
                member.PermanentPartyId = party.Id;
            }

            if (ctx.Log.IsOn(SimLogLevel.Info))
            {
                StringBuilder line = ctx.Log.Begin(SimLogLevel.Info);
                line.Append("party #").Append(party.Id.ToString(CultureInfo.InvariantCulture)).Append(" formed \"").Append(party.Name).Append("\":");
                foreach (Adventurer member in members) AdventurerLog.AppendName(line.Append(' '), member);
                ctx.Log.Commit();
            }
            ctx.Events.Publish(SimEventType.PermanentPartyFormed, EventImportance.Notable, QuestSystem.Ids(members))
                .With("party", NameValue(party.Name)).With("title", party.Name).With("partyId", party.Id).With("count", members.Count - 2);
            return party;
        }

        /// <summary>Свободное сочетание «прилагательное существительное» (все заняты — любое); бросок — поток названий.</summary>
        private static string PickName(SimContext ctx)
        {
            NameList names = ctx.Data.Names;
            IReadOnlyList<string> adjectives = names.GroupAdjectives;
            IReadOnlyList<string> nouns = names.GroupNouns;
            var free = new List<string>();
            foreach (string adjective in adjectives)
            {
                foreach (string noun in nouns)
                {
                    string name = adjective + " " + noun;
                    if (!ctx.World.Parties.IsNameUsed(name)) free.Add(name);
                }
            }
            Rng rng = ctx.Streams.Stream(NameStream);
            if (free.Count > 0) return free[rng.RangeInclusive(0, free.Count - 1)];
            return adjectives[rng.RangeInclusive(0, adjectives.Count - 1)] + " " + nouns[rng.RangeInclusive(0, nouns.Count - 1)];
        }

        /// <summary>Ссоры во всех постоянных группах (раз в сутки): отношения двух членов упали до порога — один уходит.</summary>
        public static void CheckQuarrels(SimContext ctx)
        {
            foreach (Party party in new List<Party>(ctx.World.Parties.Active))
            {
                if (party.IsPermanent) CheckQuarrels(ctx, party);
            }
        }

        private static void CheckQuarrels(SimContext ctx, Party party)
        {
            float limit = ctx.Data.Balance.Decisions.PermanentPartyBreakRelation;
            RelationBook relations = ctx.World.Relations;
            while (ctx.World.Parties.TryGetParty(party.Id, out _))
            {
                List<Adventurer> members = Members(ctx, party);
                Adventurer a = null;
                Adventurer b = null;
                for (int i = 0; i < members.Count && a == null; i++)
                {
                    for (int j = i + 1; j < members.Count; j++)
                    {
                        if (relations.GetValue(members[i].Id, members[j].Id) > limit) continue;
                        a = members[i];
                        b = members[j];
                        break;
                    }
                }
                if (a == null) return;

                // Уходит тот, кто хуже относится к остальным; при равенстве — кто позже вступил.
                Adventurer leaver = GroupLoyalty(relations, a, members) < GroupLoyalty(relations, b, members) ? a : b;
                Leave(ctx, party, leaver, "quarrel", leaver == a ? b : a);
            }
        }

        /// <summary>Лояльность группе: среднее отношений к остальным членам.</summary>
        public static float GroupLoyalty(RelationBook relations, Adventurer person, IReadOnlyList<Adventurer> members)
        {
            float sum = 0f;
            int count = 0;
            foreach (Adventurer other in members)
            {
                if (other.Id == person.Id) continue;
                sum += relations.GetValue(person.Id, other.Id);
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }

        /// <summary>Человек ушёл из гильдии или погиб: выходит и из постоянной группы.</summary>
        internal static void OnRetired(SimContext ctx, Adventurer person)
        {
            if (person.PermanentPartyId != 0 && ctx.World.Parties.TryGetParty(person.PermanentPartyId, out Party party))
                Leave(ctx, party, person, "gone", null);
            person.PermanentPartyId = 0;
        }

        private static void Leave(SimContext ctx, Party party, Adventurer person, string cause, Adventurer other)
        {
            party.RemoveMember(person.Id);
            person.PermanentPartyId = 0;
            ctx.Log.Write(SimLogLevel.Info, "party #{0} {1}: #{2} left ({3})", party.Id, party.Name, person.Id, cause);
            int[] ids = other != null ? new[] { person.Id, other.Id } : new[] { person.Id };
            ctx.Events.Publish(SimEventType.PartyMemberLeft, EventImportance.Normal, ids)
                .With("party", NameValue(party.Name)).With("partyId", party.Id).With("cause", cause);
            if (party.MemberIds.Count < 2) Disband(ctx, party, cause);
        }

        private static void Disband(SimContext ctx, Party party, string cause)
        {
            List<Adventurer> left = Members(ctx, party);
            foreach (Adventurer member in left) member.PermanentPartyId = 0;
            PartyBook book = ctx.World.Parties;
            book.Remove(party);
            book.PermanentDisbanded++;
            ctx.Log.Write(SimLogLevel.Info, "party #{0} {1} disbanded after {2} quests, {3} done", party.Id, party.Name,
                party.JointQuests, party.JointSuccesses);
            ctx.Events.Publish(SimEventType.PermanentPartyDisbanded, EventImportance.Notable, QuestSystem.Ids(left))
                .With("party", NameValue(party.Name)).With("partyId", party.Id).With("cause", cause);
        }

        /// <summary>Члены постоянной группы, которые в гильдии, по порядку вступления.</summary>
        public static List<Adventurer> Members(SimContext ctx, Party party)
        {
            var members = new List<Adventurer>(party.MemberIds.Count);
            foreach (int id in party.MemberIds)
            {
                if (ctx.World.Adventurers.TryGetActive(id, out Adventurer member)) members.Add(member);
            }
            return members;
        }
    }
}
