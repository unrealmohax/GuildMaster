using GuildMaster.Bootstrap;
using GuildMaster.Core;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Пауза, скорости и состояние времени в Play Mode из меню — для проверки через MCP
    /// (<c>execute_menu_item</c>), где горячие клавиши не нажать. Меню: GuildMaster → Sandbox → Time.
    /// </summary>
    public static class TimeProbe
    {
        private const string Menu = "GuildMaster/Sandbox/Time/";

        [MenuItem(Menu + "Log State")]
        private static void LogState()
        {
            if (!TryGetRunner(out GameRunner runner)) return;

            Simulation simulation = runner.Simulation;
            GameClock clock = runner.Clock;
            GameTime time = simulation.World.Time;
            Debug.Log($"[TimeProbe] {time} {simulation.Rhythm.PhaseAt(time)}, ticks {simulation.TicksDone}, " +
                      $"paused {clock.Paused}, x{clock.Multiplier}, debug speed {clock.DebugSpeed} (allowed {clock.DebugSpeedAllowed}), " +
                      $"frame {Time.frameCount}, realtime {Time.realtimeSinceStartup:0.00}");
        }

        [MenuItem(Menu + "Toggle Pause")]
        private static void TogglePause()
        {
            if (TryGetRunner(out GameRunner runner)) runner.Clock.TogglePause();
        }

        [MenuItem(Menu + "Speed 1")] private static void Speed1() => SetSpeed(0);
        [MenuItem(Menu + "Speed 2")] private static void Speed2() => SetSpeed(1);
        [MenuItem(Menu + "Speed 3")] private static void Speed3() => SetSpeed(2);

        [MenuItem(Menu + "Debug Speed")]
        private static void DebugSpeed()
        {
            if (TryGetRunner(out GameRunner runner) && !runner.Clock.SetDebugSpeed())
                Debug.Log("[TimeProbe] debug speed is not allowed in this build");
        }

        /// <summary>
        /// Редактор без фокуса не крутит кадры Play Mode (runInBackground выключен в настройках проекта).
        /// Пункт переключает runInBackground только у запущенной игры; настройку проекта (PlayerSettings) не трогает
        /// и пишет обе в лог, чтобы это было видно. Выключить — тем же пунктом до выхода из Play Mode.
        /// </summary>
        [MenuItem(Menu + "Toggle Run In Background")]
        private static void ToggleRunInBackground()
        {
            if (!EditorApplication.isPlaying) return;

            Application.runInBackground = !Application.runInBackground;
            Debug.Log($"[TimeProbe] runInBackground: game {Application.runInBackground}, project {PlayerSettings.runInBackground}");
        }

        private static void SetSpeed(int index)
        {
            if (TryGetRunner(out GameRunner runner)) runner.Clock.SetSpeed(index);
        }

        private static bool TryGetRunner(out GameRunner runner)
        {
            runner = EditorApplication.isPlaying ? Object.FindAnyObjectByType<GameRunner>() : null;
            if (runner != null && runner.Simulation != null) return true;

            Debug.Log("[TimeProbe] needs Play Mode with a running GameRunner");
            return false;
        }
    }
}
