#if UNITY_EDITOR
using System.Globalization;
using GuildMaster.Data;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// GuildMaster → Run Headless…: прогон симуляции без сцены и интерфейса.
    /// </summary>
    public sealed class HeadlessRunWindow : EditorWindow
    {
        private GameConfig config;
        private long seed;
        private int days = 360;
        private string report = string.Empty;

        [MenuItem("GuildMaster/Run Headless…")]
        private static void Open() => GetWindow<HeadlessRunWindow>("Run Headless");

        private void OnEnable()
        {
            if (config == null) config = EditorAssets.FindGameConfig();
            if (seed == 0) seed = HeadlessRun.NewRandomSeed();
        }

        private void OnGUI()
        {
            config = (GameConfig)EditorGUILayout.ObjectField("Game Config", config, typeof(GameConfig), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                seed = EditorGUILayout.LongField("Зерно", seed);
                if (GUILayout.Button("Случайное", GUILayout.Width(90))) seed = HeadlessRun.NewRandomSeed();
            }
            seed = System.Math.Min(System.Math.Max(seed, 0L), uint.MaxValue);
            days = Mathf.Max(0, EditorGUILayout.IntField("Дней", days));

            using (new EditorGUI.DisabledScope(config == null))
            {
                if (GUILayout.Button("Прогнать")) RunOnce();
            }

            if (config == null) EditorGUILayout.HelpBox("Нет ассета GameConfig.", MessageType.Warning);
            if (report.Length > 0) EditorGUILayout.HelpBox(report, MessageType.Info);
        }

        private void RunOnce()
        {
            HeadlessRun.Result result = HeadlessRun.Run(config, (uint)seed, days);
            report = string.Format(CultureInfo.InvariantCulture,
                "Зерно {0}: {1} тактов, {2} событий, конец — {3}, за {4:0.000} с",
                result.Seed, result.Ticks, result.Events, result.FinalTime, result.ElapsedSeconds);
            Debug.Log("[GuildMaster] Headless run. " + report);
        }
    }
}
#endif
