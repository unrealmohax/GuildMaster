#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GuildMaster.Bootstrap;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GuildMaster.Sandbox
{
    /// <summary>
    /// Проверка интерфейса в Play Mode через меню (для MCP, где не нажать мышью): снимки Game view всех экранов и окон
    /// в 1920×1080 и 1280×720, замер кадра. Снимки — в <c>Logs/UiShots/</c> (после просмотра удалить).
    /// Размер Game view меняется отражением; <c>Restore Game View Size</c> возвращает прежний и убирает добавленные размеры.
    /// Без фокуса окна кадры не идут — на время проверки включить Run In Background (GuildMaster → Sandbox → Time).
    /// </summary>
    public static class UiProbe
    {
        private const string ShotsFolder = "Logs/UiShots";
        private const string SizeLabelPrefix = "GM probe ";
        private const int FramesPerStep = 6;

        private static readonly Queue<(string Name, Action Prepare)> Steps = new Queue<(string, Action)>();
        private static int waitFrames;
        private static string pendingShot;
        private static int savedSizeIndex = -1;

        [MenuItem("GuildMaster/Sandbox/UI/Capture All 1920x1080")]
        public static void CaptureFullHd() => CaptureAll(1920, 1080);

        [MenuItem("GuildMaster/Sandbox/UI/Capture All 1280x720")]
        public static void CaptureHd() => CaptureAll(1280, 720);

        [MenuItem("GuildMaster/Sandbox/UI/Capture Current 1920x1080")]
        public static void CaptureCurrentFullHd() => CaptureCurrent(1920, 1080);

        [MenuItem("GuildMaster/Sandbox/UI/Capture Current 1280x720")]
        public static void CaptureCurrentHd() => CaptureCurrent(1280, 720);

        private static void CaptureCurrent(int width, int height)
        {
            if (!EditorApplication.isPlaying) return;
            SetGameViewSize(width, height);
            Directory.CreateDirectory(ShotsFolder);
            Steps.Clear();
            Steps.Enqueue(($"{width}x{height}_current_{DateTime.Now:HHmmss}", () => { }));
            waitFrames = FramesPerStep;
            pendingShot = null;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("GuildMaster/Sandbox/UI/Restore Game View Size")]
        public static void RestoreGameViewSize()
        {
            object group = CurrentGroup();
            if (savedSizeIndex >= 0) SelectSize(savedSizeIndex);
            MethodInfo getTotal = group.GetType().GetMethod("GetTotalCount");
            MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize");
            MethodInfo remove = group.GetType().GetMethod("RemoveCustomSize");
            for (int i = (int)getTotal.Invoke(group, null) - 1; i >= 0; i--)
            {
                object size = getSize.Invoke(group, new object[] { i });
                string label = (string)size.GetType().GetProperty("baseText").GetValue(size);
                if (label != null && label.StartsWith(SizeLabelPrefix, StringComparison.Ordinal)) remove.Invoke(group, new object[] { i });
            }
            savedSizeIndex = -1;
            Debug.Log("[UiProbe] Game view size restored, probe sizes removed");
        }

        [MenuItem("GuildMaster/Sandbox/UI/Measure Frames 10s")]
        public static void MeasureFrames()
        {
            GameRunner runner = Object.FindAnyObjectByType<GameRunner>();
            if (!EditorApplication.isPlaying || runner == null)
            {
                Debug.LogWarning("[UiProbe] Play Mode with GameRunner required");
                return;
            }
            var meter = runner.gameObject.AddComponent<FrameMeter>();
            meter.Label = runner.Clock.Paused ? "paused" : $"x{runner.Clock.Multiplier}";
        }

        [MenuItem("GuildMaster/Sandbox/UI/Advance 45 Days")]
        private static void Advance45Days()
        {
            UiRoot ui = Object.FindAnyObjectByType<UiRoot>();
            if (!EditorApplication.isPlaying || ui == null || ui.Context?.Session == null)
            {
                Debug.LogWarning("[UiProbe] Play Mode with bound UiRoot required");
                return;
            }
            ui.Context.Session.Advance(24 * 45);
            ui.RefreshNow();
        }

        /// <summary>
        /// Включить «Только группой» (D, C, бессрочно), «Компенсацию за ранение» и Сухой закон на 7 дней — для снимков экрана
        /// распоряжений и отчёта.
        /// </summary>
        [MenuItem("GuildMaster/Sandbox/UI/Enable Sample Decrees")]
        public static void EnableSampleDecrees()
        {
            UiRoot ui = Object.FindAnyObjectByType<UiRoot>();
            if (!EditorApplication.isPlaying || ui == null || ui.Client == null)
            {
                Debug.LogWarning("[UiProbe] Play Mode with bound UiRoot required");
                return;
            }
            ui.Client.Send(new ToggleDecreeCommand("GroupOnlyFromRank", true, new[] { GuildRank.D, GuildRank.C }));
            ui.Client.Send(new ToggleDecreeCommand("InjuryCompensation", true));
            ui.Client.Send(new ToggleDecreeCommand("Prohibition", true, null, DecreeDuration.Week));
            Debug.Log("[UiProbe] sample decrees sent");
        }

        [MenuItem("GuildMaster/Sandbox/UI/Log Screen State")]
        public static void LogState()
        {
            UiRoot ui = Object.FindAnyObjectByType<UiRoot>();
            if (ui == null || ui.Navigator == null)
            {
                Debug.LogWarning("[UiProbe] UiRoot is not bound");
                return;
            }
            string windows = string.Join(", ", ui.Navigator.Windows.Select(w => w.GetType().Name));
            Debug.Log($"[UiProbe] screen={ui.Navigator.Current?.Id} windows=[{windows}] time={ui.Client.World.Time} " +
                      $"paused={ui.Clock?.Paused} triggers={ui.Client.World.Autopause.Triggers.Count} feed={ui.Client.World.Feed.Guild.Count}");
        }

        private static void CaptureAll(int width, int height)
        {
            UiRoot ui = Object.FindAnyObjectByType<UiRoot>();
            if (!EditorApplication.isPlaying || ui == null || ui.Navigator == null)
            {
                Debug.LogWarning("[UiProbe] Play Mode with bound UiRoot required");
                return;
            }

            SetGameViewSize(width, height);
            Directory.CreateDirectory(ShotsFolder);
            string prefix = $"{width}x{height}";
            UiNavigator nav = ui.Navigator;
            WorldState world = ui.Client.World;

            // Окна решений, которые ждут очереди, открылись бы поверх снимков — открыть и закрыть их заранее.
            CloseAll(nav);
            while (ui.ShowPendingPopup()) CloseAll(nav);

            Steps.Clear();
            Steps.Enqueue(($"{prefix}_1_guild_people", () => { CloseAll(nav); nav.Go(Destination.ToGuild(GuildTab.People)); }));
            Steps.Enqueue(($"{prefix}_2_guild_buildings", () => nav.Go(Destination.ToGuild(GuildTab.Buildings))));
            Steps.Enqueue(($"{prefix}_3_guild_staff", () => nav.Go(Destination.ToGuild(GuildTab.Staff))));
            Steps.Enqueue(($"{prefix}_4_guild_parties", () => nav.Go(Destination.ToGuild(GuildTab.Parties))));
            Steps.Enqueue(($"{prefix}_5_board", () =>
            {
                int order = world.Orders.Open.Count > 0 ? world.Orders.Open[0].Id : 0;
                nav.Go(Destination.ToScreen(ScreenId.Board, order));
            }));
            Steps.Enqueue(($"{prefix}_6_quests", () =>
            {
                int quest = world.Quests.Active.Count > 0 ? world.Quests.Active[0].Id
                    : world.Quests.Finished.Count > 0 ? world.Quests.Finished[0].Id : 0;
                nav.Go(Destination.ToScreen(ScreenId.Quests, quest));
            }));
            Steps.Enqueue(($"{prefix}_7_card", () =>
            {
                nav.ShowScreen(ScreenId.Guild);
                nav.Go(Destination.Card(world.Adventurers.Active[0].Id));
            }));
            Steps.Enqueue(($"{prefix}_8_time", () => { CloseAll(nav); ui.ToggleTime(); }));
            Steps.Enqueue(($"{prefix}_9_notifications", () => { CloseAll(nav); ui.ToggleNotifications(); }));
            Steps.Enqueue(($"{prefix}_10_debug", () => { CloseAll(nav); ui.ToggleDebug(); }));
            Steps.Enqueue(($"{prefix}_11_treasury", () => { CloseAll(nav); nav.Go(Destination.ToScreen(ScreenId.Treasury)); }));
            Steps.Enqueue(($"{prefix}_12_candidate", () =>
            {
                CloseAll(nav);
                if (world.Adventurers.Candidates.Count > 0) ui.OpenPopup(new Popup(PopupKind.AdventurerCandidate, world.Adventurers.Candidates[0].Adventurer.Id));
            }));
            Steps.Enqueue(($"{prefix}_13_staff_candidate", () =>
            {
                CloseAll(nav);
                if (world.Staff.Candidates.Count > 0) ui.OpenPopup(new Popup(PopupKind.StaffCandidate, world.Staff.Candidates[0].Id));
            }));
            Steps.Enqueue(($"{prefix}_14_report", () =>
            {
                CloseAll(nav);
                if (world.Reports.Reports.Count > 0) ui.OpenPopup(new Popup(PopupKind.Report, world.Reports.Reports.Count - 1));
            }));
            Steps.Enqueue(($"{prefix}_15_defeat_preview", () => { CloseAll(nav); ui.OpenPopup(new Popup(PopupKind.Defeat)); }));
            Steps.Enqueue(($"{prefix}_16_decrees", () => { CloseAll(nav); nav.ShowScreen(ScreenId.Decrees); }));
            Steps.Enqueue(($"{prefix}_end", () => { CloseAll(nav); nav.ShowScreen(ScreenId.Guild); }));

            waitFrames = FramesPerStep;
            pendingShot = null;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log($"[UiProbe] Capturing {Steps.Count - 1} shots at {prefix}");
        }

        private static void CloseAll(UiNavigator nav)
        {
            while (nav.CloseTop()) { }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                return;
            }
            if (--waitFrames > 0) return;

            UiRoot ui = Object.FindAnyObjectByType<UiRoot>();
            if (pendingShot != null)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(ShotsFolder, pendingShot + ".png"));
                pendingShot = null;
                waitFrames = FramesPerStep;
                return;
            }
            if (Steps.Count == 0 || ui == null)
            {
                EditorApplication.update -= Tick;
                Debug.Log("[UiProbe] Capture done: " + Path.GetFullPath(ShotsFolder));
                return;
            }

            (string name, Action prepare) = Steps.Dequeue();
            prepare();
            ui.RefreshNow();
            if (!name.EndsWith("_end", StringComparison.Ordinal)) pendingShot = name;
            waitFrames = FramesPerStep;
        }

        // ---------- Game view ----------

        private static object CurrentGroup()
        {
            Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object instance = singleton.GetProperty("instance").GetValue(null);
            return sizesType.GetProperty("currentGroup").GetValue(instance);
        }

        private static void SetGameViewSize(int width, int height)
        {
            object group = CurrentGroup();
            if (savedSizeIndex < 0) savedSizeIndex = SelectedIndex();

            MethodInfo getTotal = group.GetType().GetMethod("GetTotalCount");
            MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize");
            int index = -1;
            for (int i = 0; i < (int)getTotal.Invoke(group, null); i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                Type type = size.GetType();
                if ((int)type.GetProperty("width").GetValue(size) == width && (int)type.GetProperty("height").GetValue(size) == height
                    && (string)type.GetProperty("baseText").GetValue(size) == SizeLabelPrefix + width)
                {
                    index = i;
                }
            }
            if (index < 0)
            {
                Type sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
                Type kindType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                object fixedKind = Enum.Parse(kindType, "FixedResolution");
                object newSize = Activator.CreateInstance(sizeType, fixedKind, width, height, SizeLabelPrefix + width);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                index = (int)getTotal.Invoke(group, null) - 1;
            }
            SelectSize(index);
        }

        private static EditorWindow GameView() =>
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"), false, null, false);

        private static int SelectedIndex()
        {
            EditorWindow view = GameView();
            PropertyInfo selected = view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return selected != null ? (int)selected.GetValue(view) : -1;
        }

        private static void SelectSize(int index)
        {
            EditorWindow view = GameView();
            MethodInfo select = view.GetType().GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            select.Invoke(view, new object[] { index, null });
            view.Repaint();
        }
    }
}
#endif
