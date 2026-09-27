using System;
using System.Collections.Generic;
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

        /// <summary>Ключи, которые события дают всегда (не из данных) — их шаблоны обязаны быть в данных.</summary>
        public static IReadOnlyList<string> Fixed { get; } = new[]
        {
            CandidateArrived, CandidateLeft, AdventurerJoined, ArchetypeChanged, AdventurerLeft, WalletEmptied,
            Recovered, WoundComplicated,
            BreakdownBinge, BreakdownBrawl, BreakdownBrawlAlone, BreakdownRefuse, BreakdownCollapse,
            Quarrel,
            BankruptcyStarted, BankruptcyLifted, GuildClosed, MonthReport,
            NewOrders, RegistrarDeclined, AwaitingPlayer, OrderExpired, OrderNoAnswer,
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
            };

        /// <summary>Ключ строки события; <c>null</c> — событие строки не даёт.</summary>
        public static string Of(SimEvent simEvent, WorldState world, DataRegistry data) =>
            Keys.TryGetValue(simEvent.Type, out Func<SimEvent, WorldState, DataRegistry, string> key) ? key(simEvent, world, data) : null;

        /// <summary>У событий этого типа бывают строки.</summary>
        public static bool HasLine(SimEventType type) => Keys.ContainsKey(type);

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
