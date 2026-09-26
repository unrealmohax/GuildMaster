using System.Globalization;
using System.Linq;
using System.Text;
using GuildMaster.Bootstrap;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница (GM-04, состояние — GM-05). Люди гильдии в консоль и ответ кандидату из меню — для проверки через MCP
    /// (<c>execute_menu_item</c>, <c>read_console</c> с <c>types: ["all"]</c>). Меню: GuildMaster → Sandbox → Adventurers.
    /// </summary>
    public static class AdventurerProbe
    {
        private const string Menu = "GuildMaster/Sandbox/Adventurers/";

        [MenuItem(Menu + "Log Roster")]
        private static void LogRoster()
        {
            if (TryGetRunner(out GameRunner runner)) Debug.Log(Describe(runner.Simulation));
        }

        [MenuItem(Menu + "Accept First Candidate")]
        private static void AcceptFirst()
        {
            if (!TryGetRunner(out GameRunner runner)) return;
            Candidate candidate = runner.Simulation.World.Adventurers.Candidates.FirstOrDefault();
            if (candidate == null)
            {
                Debug.Log("[AdventurerProbe] no candidates");
                return;
            }
            runner.Simulation.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
            Debug.Log($"[AdventurerProbe] accept #{candidate.Adventurer.Id} {candidate.Adventurer.Name} sent");
        }

        [MenuItem(Menu + "Reject First Candidate")]
        private static void RejectFirst()
        {
            if (!TryGetRunner(out GameRunner runner)) return;
            Candidate candidate = runner.Simulation.World.Adventurers.Candidates.FirstOrDefault();
            if (candidate == null)
            {
                Debug.Log("[AdventurerProbe] no candidates");
                return;
            }
            runner.Simulation.Send(new RejectCandidateCommand(candidate.Adventurer.Id));
            Debug.Log($"[AdventurerProbe] reject #{candidate.Adventurer.Id} {candidate.Adventurer.Name} sent");
        }

        /// <summary>Стартовая шестёрка для зёрен 1–3 без Play Mode: симуляция создаётся в памяти и выбрасывается.</summary>
        [MenuItem(Menu + "Preview Start Lineups")]
        private static void PreviewStart()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null)
            {
                Debug.Log("[AdventurerProbe] no GameConfig");
                return;
            }
            DataRegistry data = DataRegistry.FromConfig(config);
            for (uint seed = 1; seed <= 3; seed++)
            {
                Debug.Log($"seed {seed}\n" + Describe(Simulation.CreateDefault(data, seed)));
            }
        }

        private static string Describe(Simulation simulation)
        {
            AdventurerRoster roster = simulation.World.Adventurers;
            var text = new StringBuilder();
            text.Append($"[AdventurerProbe] {simulation.World.Time}: active {roster.Active.Count}, candidates {roster.Candidates.Count}, ")
                .Append($"archive {roster.Archive.Count}, relations {simulation.World.Relations.All.Count}\n");
            foreach (Adventurer adventurer in roster.Active) Append(text, "", adventurer, simulation.Data);
            foreach (Candidate candidate in roster.Candidates) Append(text, $"candidate until {simulation.Calendar.At(candidate.ExpiresAtHours)}: ", candidate.Adventurer, simulation.Data);
            foreach (Relation relation in simulation.World.Relations.All)
            {
                text.Append($"  relation #{relation.A}–#{relation.B}: {relation.Value.ToString("0.#", CultureInfo.InvariantCulture)}, joint {relation.JointQuests}\n");
            }
            return text.ToString();
        }

        private static void Append(StringBuilder text, string prefix, Adventurer a, DataRegistry data)
        {
            string archetype = data.TryGet(a.ArchetypeId, out ArchetypeDefinition definition)
                ? (a.Gender == Gender.Female ? definition.DisplayNameFemale : definition.DisplayName)
                : a.ArchetypeId;
            text.Append($"  {prefix}#{a.Id} {a.Name} ({a.Gender}, {a.Age}), {a.GuildRank}, {archetype} {a.PowerScore:0.#}, wallet {a.State.Wallet}\n");
            text.Append("    stats ").Append(string.Join(" ", a.Stats.Select((v, i) => $"{(StatId)i}={v:0.#}"))).Append('\n');
            text.Append("    axes ").Append(string.Join(" ", a.Axes.Select((v, i) => $"{(AxisId)i}={v:0}{(a.RevealedAxes[i] ? "!" : "")}"))).Append('\n');
            if (a.Traits.Count > 0)
            {
                text.Append("    traits ").Append(string.Join(", ", a.Traits.Select(t =>
                    t.TraitId + (t.PartnerId != 0 ? $"@#{t.PartnerId}" : "") + (t.AffectedStat.HasValue ? $"[{t.AffectedStat}]" : "") + (t.Revealed ? "!" : " (hidden)")))).Append('\n');
            }
            text.Append("    ").Append(DescribeState(a, data)).Append('\n');
        }

        /// <summary>Состояние (GM-05) одной строкой: занятие, шкалы, лояльность словами, деньги, флаги, раны.</summary>
        internal static string DescribeState(Adventurer a, DataRegistry data)
        {
            AdventurerState s = a.State;
            var text = new StringBuilder();
            text.Append($"state {s.Activity}: fatigue {s.Fatigue:0.#}, stress {s.Stress:0.#}, contentment {s.Contentment:0.#}, ")
                .Append($"loyalty {s.Loyalty:0.#} «{StateRules.LoyaltyWord(s.Loyalty, a.Gender, data.Balance.State)}», wallet {s.Wallet}, debt {s.DebtToGuild}");
            if (s.IsWalletEmpty) text.Append(", wallet empty");
            if (s.Breakdown != BreakdownKind.None) text.Append($", breakdown {s.Breakdown} until {s.BreakdownEndsAtHours}h");
            if (s.InInfirmary) text.Append(", in infirmary");
            if (s.Conditions.Count > 0)
            {
                text.Append(", wounds ").Append(string.Join(" ", s.Conditions.Select(c =>
                    $"{c.Kind} {c.RemainingDays:0.#}/{c.Days}d{(c.IsComplicated ? " complicated" : "")}")));
            }
            return text.ToString();
        }

        internal static bool TryGetRunner(out GameRunner runner)
        {
            runner = EditorApplication.isPlaying ? Object.FindFirstObjectByType<GameRunner>() : null;
            if (runner != null && runner.Simulation != null) return true;

            Debug.Log("[AdventurerProbe] needs Play Mode with a running GameRunner");
            return false;
        }
    }
}
