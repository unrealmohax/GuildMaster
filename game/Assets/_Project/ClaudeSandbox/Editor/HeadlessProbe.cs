using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Прогон без интерфейса из меню GuildMaster → Sandbox → Headless — то же, что кнопка окна Run Headless…,
    /// но без окна: для проверки через MCP (`execute_menu_item`). Файлы — в <c>Logs/</c> проекта, пути — в консоль.
    /// </summary>
    public static class HeadlessProbe
    {
        private const string Menu = "GuildMaster/Sandbox/Headless/";

        [MenuItem(Menu + "Year Seed 1 Debug")]
        private static void YearDebug() => Run(1u, 360, SimLogLevel.Debug, 1);

        [MenuItem(Menu + "Year Seed 1 Trace")]
        private static void YearTrace() => Run(1u, 360, SimLogLevel.Trace, 1);

        [MenuItem(Menu + "Year Seeds 1-10 Info")]
        private static void TenYears() => Run(1u, 360, SimLogLevel.Info, 10);

        /// <summary>Год «Простого» бота, который в первый такт включает все четыре распоряжения (Сухой закон — на 7 дней).</summary>
        [MenuItem(Menu + "Year Seeds 1-10 Info With Decrees")]
        private static void TenYearsWithDecrees() => Run(1u, 360, SimLogLevel.Info, 10, WithDecrees);

        private static PlayerBot WithDecrees()
        {
            var rules = new System.Collections.Generic.List<IBotRule>(PlayerBots.Simple().Rules) { new AllDecreesRule() };
            return new PlayerBot("Простой + распоряжения", rules.ToArray());
        }

        private sealed class AllDecreesRule : IBotRule
        {
            public void Act(BotTurn turn)
            {
                if (turn.Tick != 0) return;
                turn.Send(new ToggleDecreeCommand("FreeLodgingForNewcomers", true));
                turn.Send(new ToggleDecreeCommand("GroupOnlyFromRank", true, new[] { GuildRank.C }));
                turn.Send(new ToggleDecreeCommand("InjuryCompensation", true));
                turn.Send(new ToggleDecreeCommand("Prohibition", true, null, DecreeDuration.Week));
            }
        }

        private static void Run(uint seed, int days, SimLogLevel level, int runs, System.Func<PlayerBot> bot = null)
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null)
            {
                Debug.Log("[HeadlessProbe] no GameConfig");
                return;
            }

            var options = new HeadlessRun.Options { Seed = seed, Days = days, LogLevel = level, Runs = runs, Bot = bot ?? PlayerBots.Simple };
            HeadlessRun.Batch batch = HeadlessRun.RunToFiles(config, options);
            double seconds = 0;
            foreach (HeadlessRun.Result result in batch.Results) seconds += result.ElapsedSeconds;
            Debug.Log($"[HeadlessProbe] {runs} run(s), {days} days, {level}: {seconds:0.000} s\n" + string.Join("\n", batch.Files));
        }
    }
}
