using System;
using System.Collections.Generic;
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
    /// Песочница. Состояние и здоровье из меню GuildMaster → Sandbox → State: раны и стресс первому в гильдии
    /// в Play Mode (командой, как их вызывало бы задание) и прогон 90 дней без Play Mode для зёрен 1–3
    /// (симуляция в памяти, кандидаты принимаются, сцена не трогается). Пишет через <c>Debug.Log</c>.
    /// </summary>
    public static class StateProbe
    {
        private const string Menu = "GuildMaster/Sandbox/State/";
        private const int PreviewDays = 90;

        [MenuItem(Menu + "Light Wound First")]
        private static void LightWound() => SendToFirst("light wound", (ctx, a) => HealthService.Wound(ctx, a, ConditionKind.LightWound));

        [MenuItem(Menu + "Heavy Wound First")]
        private static void HeavyWound() => SendToFirst("heavy wound", (ctx, a) => HealthService.Wound(ctx, a, ConditionKind.HeavyWound));

        [MenuItem(Menu + "Stress +50 First")]
        private static void Stress() => SendToFirst("stress +50", (ctx, a) => StateService.AddStress(ctx, a, 50f));

        [MenuItem(Menu + "Preview 90 Days")]
        private static void Preview()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null)
            {
                Debug.Log("[StateProbe] no GameConfig");
                return;
            }
            DataRegistry data = DataRegistry.FromConfig(config);
            for (uint seed = 1; seed <= 3; seed++) Debug.Log(Run(data, seed));
        }

        private static string Run(DataRegistry data, uint seed)
        {
            Simulation simulation = Simulation.CreateDefault(data, seed);
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            simulation.TickCompleted += events =>
            {
                foreach (SimEvent e in events)
                {
                    if (e.Source != nameof(ActivitySystem) && e.Source != nameof(StateSystem) && e.Source != nameof(HealthSystem)) continue;
                    string key = e.Type == SimEventType.Breakdown && e.TryGet("kind", out BreakdownKind kind) ? $"Breakdown.{kind}" : e.Type.ToString();
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            };

            for (int day = 0; day < PreviewDays; day++)
            {
                for (int hour = 0; hour < simulation.Calendar.HoursPerDay; hour++) simulation.Tick();
                foreach (Candidate candidate in simulation.World.Adventurers.Candidates.ToList())
                    simulation.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
            }

            var text = new StringBuilder();
            text.Append($"[StateProbe] seed {seed}, {PreviewDays} days → {simulation.World.Time}: active {simulation.World.Adventurers.Active.Count}, ")
                .Append($"archive {simulation.World.Adventurers.Archive.Count}\n  events ")
                .Append(counts.Count == 0 ? "none" : string.Join(", ", counts.Select(p => $"{p.Key} {p.Value}"))).Append('\n');
            foreach (Adventurer adventurer in simulation.World.Adventurers.Active)
            {
                text.Append($"  #{adventurer.Id} {adventurer.Name}: ").Append(AdventurerProbe.DescribeState(adventurer, data)).Append('\n');
            }
            return text.ToString();
        }

        private static void SendToFirst(string what, Action<SimContext, Adventurer> action)
        {
            if (!AdventurerProbe.TryGetRunner(out GameRunner runner)) return;
            Adventurer first = runner.Simulation.World.Adventurers.Active.FirstOrDefault();
            if (first == null)
            {
                Debug.Log("[StateProbe] nobody in the guild");
                return;
            }
            runner.Simulation.Send(new ProbeCommand(ctx =>
            {
                if (ctx.World.Adventurers.TryGetActive(first.Id, out Adventurer adventurer)) action(ctx, adventurer);
            }));
            Debug.Log($"[StateProbe] {what} → #{first.Id} {first.Name} sent");
        }

        /// <summary>Команда песочницы: вызвать службу Core так, как её вызовет система.</summary>
        private sealed class ProbeCommand : ICommand
        {
            private readonly Action<SimContext> action;

            public ProbeCommand(Action<SimContext> action)
            {
                this.action = action;
            }

            public void Apply(SimContext ctx) => action(ctx);
        }
    }
}
