using System;
using System.Collections.Generic;
using System.Linq;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Какой ключ шаблона ленты даёт событие. Событие без ключа строки не даёт. Новое событие со строкой —
    /// строка в <see cref="Keys"/>; постоянный ключ — ещё и в <see cref="Fixed"/> (его проверяет валидатор данных).
    /// Ключи раскрытия черт берутся из данных полюса, черты или оси.
    /// </summary>
    public static class FeedKeys
    {
        public const string CandidateArrived = "guild.candidate.arrived";
        public const string CandidateLeft = "guild.candidate.left";
        public const string AdventurerJoined = "guild.adventurer.joined";
        public const string ArchetypeChanged = "guild.archetype.changed";
        public const string AdventurerLeft = "guild.adventurer.left";
        public const string WalletEmptied = "guild.money.walletEmpty";
        public const string Recovered = "guild.health.recovered";
        public const string WoundComplicated = "guild.health.complication";
        public const string BreakdownBinge = "guild.breakdown.binge";
        public const string BreakdownBrawl = "guild.breakdown.brawl";
        public const string BreakdownBrawlAlone = "guild.breakdown.brawlAlone";
        public const string BreakdownRefuse = "guild.breakdown.refuse";
        public const string BreakdownCollapse = "guild.breakdown.collapse";
        public const string Quarrel = "guild.relation.quarrel";
        public const string BankruptcyStarted = "guild.bankruptcy.started";
        public const string BankruptcyLifted = "guild.bankruptcy.lifted";
        public const string GuildClosed = "guild.closed";
        public const string MonthReport = "guild.month.summary";
        public const string NewOrders = "guild.board.newOrders";
        public const string RegistrarDeclined = "guild.board.declined";
        public const string AwaitingPlayer = "guild.board.awaitingPlayer";
        public const string OrderExpired = "guild.board.expired";
        public const string OrderNoAnswer = "guild.board.noAnswer";
        public const string OrderTaken = "guild.order.taken";
        public const string PromotionTaken = "guild.promotion.taken";
        public const string OrderRefused = "guild.order.refused";
        public const string PromotionOffered = "guild.promotion.offered";
        public const string RankPromoted = "guild.rank.promoted";
        public const string Fled = "guild.adventurer.fled";
        public const string DeserterReturned = "guild.deserter.returned";
        public const string Disappeared = "guild.adventurer.disappeared";
        public const string EventQuestFound = "guild.eventQuest.found";
        public const string PartyGatheredPair = "guild.party.formed.pair";
        public const string PartyGatheredThree = "guild.party.formed.three";
        public const string PartyGatheredFour = "guild.party.formed";
        public const string PartyGatheredFive = "guild.party.formed.five";
        public const string PartyGatheredSix = "guild.party.formed.six";
        public const string PermanentPartySetOut = "guild.party.permanentSetOut";
        public const string PartyNotGathered = "guild.party.noPartners";
        public const string PartyNotGatheredSolo = "guild.party.noPartners.solo";
        public const string InvitationDeclined = "guild.party.declined";
        public const string InvitationDeclinedSolo = "guild.party.declined.solo";
        public const string PermanentPartyFormed = "guild.party.permanentFormed";
        public const string PermanentPartyFormedPair = "guild.party.permanentFormed.pair";
        public const string PermanentPartyDisbanded = "guild.party.permanentDisbanded";
        public const string PartyMemberLeftQuarrel = "guild.party.memberLeft.quarrel";
        public const string PartyMemberLeftLoner = "guild.party.memberLeft.loner";
        public const string PartyMemberJoined = "guild.party.memberJoined";

        public const string QuestDeparted = "quest.departed";
        public const string QuestTravel = "quest.travel";
        public const string QuestCamp = "quest.camp";
        public const string QuestCeiling = "quest.ceiling";
        public const string QuestDeath = "quest.death.one";
        public const string QuestLost = "quest.death.all";
        public const string QuestOnlyFugitive = "quest.death.onlyFugitive";
        public const string QuestMedicSaved = "quest.death.medicSaved";
        public const string QuestReturnTrip = "quest.returnTrip";
        public const string QuestReturnTripHard = "quest.returnTrip.hard";
        public const string QuestRetreat = "quest.decision.retreat";
        public const string QuestFlee = "quest.tension.flee";
        public const string QuestLootHandedIn = "quest.loot.handedIn";
        public const string QuestLootSkimmed = "quest.loot.skimmed";

        // Строки ленты задания с видом из данных события (kind): «префикс.вид».
        private static readonly string[] QuestLossKinds = { "time", "stress", "bonus", "gear", "loot" };
        private static readonly string[] QuestWoundKinds = { "shield", "light", "heavy", "maimed" };
        private static readonly string[] QuestDecisionKinds = { "continue", "doubt", "argue" };
        private static readonly string[] QuestTensionKinds = { "panic", "rush", "hero", "hold", "breakdown" };
        private static readonly string[] QuestSynergyKinds = { "friends", "lovers", "rivals" };
        private static readonly string[] QuestResultKinds = { "brilliant", "success", "partial", "fail", "catastrophe" };
        private static readonly string[] PromotionKinds = { "passed", "failed" };

        /// <summary>Ключи, которые события дают всегда (не из данных) — их шаблоны обязаны быть в данных.</summary>
        public static IReadOnlyList<string> Fixed { get; } = new[]
        {
            CandidateArrived, CandidateLeft, AdventurerJoined, ArchetypeChanged, AdventurerLeft, WalletEmptied,
            Recovered, WoundComplicated,
            BreakdownBinge, BreakdownBrawl, BreakdownBrawlAlone, BreakdownRefuse, BreakdownCollapse,
            Quarrel,
            BankruptcyStarted, BankruptcyLifted, GuildClosed, MonthReport,
            NewOrders, RegistrarDeclined, AwaitingPlayer, OrderExpired, OrderNoAnswer,
            OrderTaken, PromotionTaken, OrderRefused, PromotionOffered, RankPromoted, Fled, DeserterReturned, Disappeared, EventQuestFound,
            PartyGatheredPair, PartyGatheredThree, PartyGatheredFour, PartyGatheredFive, PartyGatheredSix, PermanentPartySetOut,
            PartyNotGathered, PartyNotGatheredSolo, InvitationDeclined, InvitationDeclinedSolo,
            PermanentPartyFormed, PermanentPartyFormedPair, PermanentPartyDisbanded, PartyMemberLeftQuarrel, PartyMemberLeftLoner, PartyMemberJoined,
            QuestDeparted, QuestTravel, QuestCamp, QuestCeiling, QuestDeath, QuestLost, QuestOnlyFugitive, QuestMedicSaved,
            QuestReturnTrip, QuestReturnTripHard, QuestRetreat, QuestFlee, QuestLootHandedIn, QuestLootSkimmed,
        }.Concat(Prefixed("quest.loss.", QuestLossKinds)).Concat(Prefixed("quest.wound.", QuestWoundKinds))
            .Concat(Prefixed("quest.decision.", QuestDecisionKinds)).Concat(Prefixed("quest.tension.", QuestTensionKinds))
            .Concat(Prefixed("quest.synergy.", QuestSynergyKinds)).Concat(Prefixed("quest.returned.", QuestResultKinds))
            .Concat(Prefixed("quest.promotion.", PromotionKinds)).ToArray();

        /// <summary>
        /// События задания, строки которых важны для всей гильдии: гибель, отступление (катастрофа — по уровню возвращения;
        /// у бегства — своя строка гильдии с причиной). Их строки ленты задания дублируются в ленту гильдии.
        /// </summary>
        private static readonly HashSet<SimEventType> GuildWorthy = new HashSet<SimEventType>
        {
            SimEventType.AdventurerDied, SimEventType.QuestLost, SimEventType.OnlyFugitiveSurvived, SimEventType.MedicSaved,
            SimEventType.PartyRetreated,
        };

        /// <summary>Своя строка гильдии у события задания (кроме строки ленты задания): бегство — с причиной.</summary>
        private static readonly Dictionary<SimEventType, string> GuildLines = new Dictionary<SimEventType, string>
        {
            { SimEventType.AdventurerFled, Fled },
        };

        private static readonly Dictionary<SimEventType, Func<SimEvent, WorldState, DataRegistry, string>> Keys =
            new Dictionary<SimEventType, Func<SimEvent, WorldState, DataRegistry, string>>
            {
                { SimEventType.CandidateArrived, (e, w, d) => CandidateArrived },
                { SimEventType.CandidateLeft, (e, w, d) => CandidateLeft },
                { SimEventType.AdventurerJoined, (e, w, d) => AdventurerJoined },
                { SimEventType.ArchetypeChanged, (e, w, d) => ArchetypeChanged },
                { SimEventType.AxisRevealed, (e, w, d) => FromPayload(e) },
                { SimEventType.TraitRevealed, (e, w, d) => FromPayload(e) },
                { SimEventType.AxisBalanced, Balanced },
                { SimEventType.Breakdown, (e, w, d) => Breakdown(e) },
                { SimEventType.AdventurerLeft, (e, w, d) => AdventurerLeft },
                { SimEventType.WalletEmptied, (e, w, d) => WalletEmptied },
                { SimEventType.WoundHealed, Healed },
                { SimEventType.WoundComplicated, (e, w, d) => WoundComplicated },
                { SimEventType.Quarrel, (e, w, d) => Quarrel },
                { SimEventType.BankruptcyStarted, (e, w, d) => BankruptcyStarted },
                { SimEventType.BankruptcyLifted, (e, w, d) => BankruptcyLifted },
                { SimEventType.GuildClosed, (e, w, d) => GuildClosed },
                { SimEventType.MonthReportReady, (e, w, d) => MonthReport },
                { SimEventType.NewOrdersPosted, (e, w, d) => NewOrders },
                { SimEventType.RegistrarDeclinedOrders, (e, w, d) => RegistrarDeclined },
                { SimEventType.OrderAwaitingPlayer, (e, w, d) => AwaitingPlayer },
                { SimEventType.OrderExpired, (e, w, d) => OrderExpired },
                { SimEventType.OrderDeclinedByPlayer, (e, w, d) => e.TryGet("noAnswer", out bool noAnswer) && noAnswer ? OrderNoAnswer : null },
                { SimEventType.OrderTaken, (e, w, d) => e.TryGet("promotion", out bool promotion) && promotion ? PromotionTaken : OrderTaken },
                { SimEventType.OrderRefused, (e, w, d) => OrderRefused },
                { SimEventType.PromotionOffered, (e, w, d) => PromotionOffered },
                { SimEventType.RankPromoted, (e, w, d) => RankPromoted },
                { SimEventType.DeserterReturned, (e, w, d) => DeserterReturned },
                { SimEventType.AdventurerDisappeared, (e, w, d) => Disappeared },
                { SimEventType.EventQuestAwaitingPlayer, (e, w, d) => EventQuestFound },
                { SimEventType.QuestDeparted, (e, w, d) => QuestDeparted },
                { SimEventType.TravelProgress, (e, w, d) => QuestTravel },
                { SimEventType.QuestCamp, (e, w, d) => QuestCamp },
                { SimEventType.TravelEvent, TravelEventKey },
                { SimEventType.Discovery, DiscoveryKey },
                { SimEventType.RoundSuccess, (e, w, d) => RoundKey(e, d, success: true) },
                { SimEventType.RoundFail, (e, w, d) => RoundKey(e, d, success: false) },
                { SimEventType.QuestLoss, (e, w, d) => Kind(e, "quest.loss.", QuestLossKinds) },
                { SimEventType.QuestWound, (e, w, d) => Kind(e, "quest.wound.", QuestWoundKinds) },
                { SimEventType.PartyDecision, (e, w, d) => Kind(e, "quest.decision.", QuestDecisionKinds) },
                { SimEventType.PartyRetreated, (e, w, d) => QuestRetreat },
                { SimEventType.TensionMoment, (e, w, d) => Kind(e, "quest.tension.", QuestTensionKinds) },
                { SimEventType.AdventurerFled, (e, w, d) => QuestFlee },
                { SimEventType.CeilingTriggered, (e, w, d) => QuestCeiling },
                { SimEventType.Synergy, (e, w, d) => Kind(e, "quest.synergy.", QuestSynergyKinds) },
                { SimEventType.AdventurerDied, (e, w, d) => e.TryGet("all", out bool all) && all ? null : QuestDeath },
                { SimEventType.QuestLost, (e, w, d) => QuestLost },
                { SimEventType.OnlyFugitiveSurvived, (e, w, d) => QuestOnlyFugitive },
                { SimEventType.MedicSaved, (e, w, d) => QuestMedicSaved },
                { SimEventType.ReturnTrip, (e, w, d) => e.TryGet("kind", out string kind) && kind == "hard" ? QuestReturnTripHard : QuestReturnTrip },
                { SimEventType.QuestReturned, (e, w, d) => e.TryGet("count", out int count) && count > 0 ? Kind(e, "quest.returned.", QuestResultKinds) : null },
                { SimEventType.LootHandedIn, (e, w, d) => QuestLootHandedIn },
                { SimEventType.LootSkimmed, (e, w, d) => QuestLootSkimmed },
                { SimEventType.PromotionExam, (e, w, d) => e.TryGet("passed", out bool passed) && passed ? "quest.promotion.passed" : "quest.promotion.failed" },
                { SimEventType.PartyGathered, (e, w, d) => Gathered(e) },
                { SimEventType.PartyNotGathered, (e, w, d) => e.TryGet("solo", out bool solo) && solo ? PartyNotGatheredSolo : PartyNotGathered },
                { SimEventType.InvitationDeclined, (e, w, d) => e.TryGet("solo", out bool solo) && solo ? InvitationDeclinedSolo : InvitationDeclined },
                { SimEventType.PermanentPartyFormed, (e, w, d) => e.TryGet("count", out int more) && more > 0 ? PermanentPartyFormed : PermanentPartyFormedPair },
                { SimEventType.PermanentPartyDisbanded, (e, w, d) => PermanentPartyDisbanded },
                { SimEventType.PartyMemberLeft, (e, w, d) => MemberLeft(e) },
                { SimEventType.PartyMemberJoined, (e, w, d) => PartyMemberJoined },
            };

        /// <summary>Ключ строки события; <c>null</c> — событие строки не даёт.</summary>
        public static string Of(SimEvent simEvent, WorldState world, DataRegistry data) =>
            Keys.TryGetValue(simEvent.Type, out Func<SimEvent, WorldState, DataRegistry, string> key) ? key(simEvent, world, data) : null;

        /// <summary>У событий этого типа бывают строки.</summary>
        public static bool HasLine(SimEventType type) => Keys.ContainsKey(type);

        /// <summary>Своя строка гильдии события задания; <c>null</c> — нет.</summary>
        public static string GuildLineOf(SimEvent simEvent) => GuildLines.TryGetValue(simEvent.Type, out string key) ? key : null;

        /// <summary>Строку ленты задания этого события дублировать в ленту гильдии: гибель, отступление, катастрофа.</summary>
        public static bool IsGuildWorthy(SimEvent simEvent) =>
            GuildWorthy.Contains(simEvent.Type)
            || (simEvent.Type == SimEventType.QuestReturned && simEvent.TryGet("kind", out string kind) && kind == "catastrophe");

        /// <summary>Группа собралась: постоянная — «взяла заказ», под задание — строка по числу людей.</summary>
        private static string Gathered(SimEvent simEvent)
        {
            if (simEvent.TryGet("permanent", out bool permanent) && permanent) return PermanentPartySetOut;
            simEvent.TryGet("total", out int total);
            switch (total)
            {
                case 2: return PartyGatheredPair;
                case 3: return PartyGatheredThree;
                case 4: return PartyGatheredFour;
                case 5: return PartyGatheredFive;
                default: return total > 5 ? PartyGatheredSix : null;
            }
        }

        /// <summary>Ушёл из постоянной группы: ссора или одиночка — строка; погиб или ушёл из гильдии — строки нет (есть своя).</summary>
        private static string MemberLeft(SimEvent simEvent)
        {
            simEvent.TryGet("cause", out string cause);
            switch (cause)
            {
                case "quarrel": return PartyMemberLeftQuarrel;
                case "loner": return PartyMemberLeftLoner;
                default: return null;
            }
        }

        private static IEnumerable<string> Prefixed(string prefix, IEnumerable<string> kinds) => kinds.Select(k => prefix + k);

        private static string Kind(SimEvent simEvent, string prefix, string[] known) =>
            simEvent.TryGet("kind", out string kind) && Array.IndexOf(known, kind) >= 0 ? prefix + kind : null;

        private static string TravelEventKey(SimEvent simEvent, WorldState world, DataRegistry data)
        {
            if (!simEvent.TryGet("event", out string id) || !data.TryGet(id, out RandomEventDefinition definition)) return null;
            simEvent.TryGet("kind", out string kind);
            switch (kind)
            {
                case "start": return definition.StartFeedKey;
                case "success": return definition.SuccessFeedKey;
                case "fail": return definition.FailFeedKey;
                default: return null;
            }
        }

        private static string DiscoveryKey(SimEvent simEvent, WorldState world, DataRegistry data)
        {
            if (!simEvent.TryGet("discovery", out string id) || !data.TryGet(id, out DiscoveryDefinition definition)) return null;
            simEvent.TryGet("kind", out string kind);
            switch (kind)
            {
                case "found": return definition.FoundFeedKey;
                case "explore": return definition.ExploreFeedKey;
                case "skip": return definition.SkipFeedKey;
                case "empty": return definition.EmptyFeedKey;
                case "loot": return definition.LootFeedKey;
                case "fail": return definition.FailFeedKey;
                case "reported": return definition.ReportedFeedKey;
                default: return null;
            }
        }

        private static string RoundKey(SimEvent simEvent, DataRegistry data, bool success)
        {
            if (!simEvent.TryGet("questType", out string id) || !data.TryGet(id, out QuestTypeDefinition type)) return null;
            return success ? type.RoundSuccessFeedKey : type.RoundFailFeedKey;
        }

        private static string FromPayload(SimEvent simEvent) => simEvent.TryGet("feedKey", out string key) ? key : null;

        private static string Balanced(SimEvent simEvent, WorldState world, DataRegistry data) =>
            simEvent.TryGet("axis", out AxisId axis) ? data.Axis(axis).BalancedFeedKey : null;

        private static string Breakdown(SimEvent simEvent)
        {
            simEvent.TryGet("kind", out BreakdownKind kind);
            switch (kind)
            {
                case BreakdownKind.Binge: return BreakdownBinge;
                case BreakdownKind.Brawl: return simEvent.Participants.Count > 1 ? BreakdownBrawl : BreakdownBrawlAlone;
                case BreakdownKind.RefuseQuests: return BreakdownRefuse;
                case BreakdownKind.Collapse: return BreakdownCollapse;
                default: return null;
            }
        }

        /// <summary>«Поправился» — только когда зажила последняя рана.</summary>
        private static string Healed(SimEvent simEvent, WorldState world, DataRegistry data)
        {
            if (simEvent.Participants.Count == 0) return null;
            Adventurer adventurer = EventTextSource.FindPerson(world, simEvent.Participants[0]);
            return adventurer != null && adventurer.State.Conditions.Count == 0 ? Recovered : null;
        }
    }
}
