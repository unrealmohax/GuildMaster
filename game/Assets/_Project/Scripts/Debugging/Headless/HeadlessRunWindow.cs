#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// GuildMaster → Run Headless…: прогон симуляции без сцены и интерфейса. Поля — зерно, дней, уровень лога, бот игрока
    /// (готовые или сценарий из файла), число прогонов. Итог — лог, сводка и сценарий в <c>Logs/</c> и таблица сводки в окне;
    /// при нескольких прогонах — средние и разброс.
    /// </summary>
    public sealed class HeadlessRunWindow : EditorWindow
    {
        private const int MaxRuns = 100;
        private const float LabelWidth = 64f;
        private const float CellWidth = 78f;

        [SerializeField] private GameConfig config;
        [SerializeField] private long seed;
        [SerializeField] private int days = 360;
        [SerializeField] private SimLogLevel logLevel = SimLogLevel.Info;
        [SerializeField] private int botIndex = 1;
        [SerializeField] private string scenarioPath = string.Empty;
        [SerializeField] private int runs = 1;
        [SerializeField] private bool logToConsole;

        private HeadlessRun.Batch batch;
        private string report = string.Empty;
        private string error = string.Empty;
        private string warning = string.Empty;
        private Vector2 scroll;

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
            seed = Math.Min(Math.Max(seed, 0L), uint.MaxValue);
            days = Mathf.Max(0, EditorGUILayout.IntField("Дней", days));
            logLevel = (SimLogLevel)EditorGUILayout.EnumPopup("Уровень лога", logLevel);
            logToConsole = EditorGUILayout.Toggle("Лог в консоль", logToConsole);

            string[] botNames = BotNames();
            botIndex = EditorGUILayout.Popup("Бот игрока", Mathf.Clamp(botIndex, 0, botNames.Length - 1), botNames);
            if (IsScenarioBot) ScenarioField();

            runs = EditorGUILayout.IntSlider("Число прогонов", runs, 1, MaxRuns);
            if (runs > 1) EditorGUILayout.LabelField(" ", $"зёрна {seed}…{(uint)(seed + runs - 1)}");

            using (new EditorGUI.DisabledScope(config == null))
            {
                if (GUILayout.Button("Прогнать")) RunNow();
            }

            if (config == null) EditorGUILayout.HelpBox("Нет ассета GameConfig.", MessageType.Warning);
            if (error.Length > 0) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (warning.Length > 0) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            if (report.Length > 0)
            {
                EditorGUILayout.HelpBox(report, MessageType.Info);
                if (GUILayout.Button("Открыть папку Logs")) EditorUtility.RevealInFinder(LogFiles.DefaultFolder);
            }

            if (batch == null || batch.Results.Count == 0) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (batch.Aggregate != null) DrawAggregate(batch.Aggregate);
            else DrawSummary(batch.Results[0].Summary);
            EditorGUILayout.EndScrollView();
        }

        private bool IsScenarioBot => botIndex >= PlayerBots.Presets.Count;

        private static string[] BotNames()
        {
            var names = new List<string>();
            foreach (BotPreset preset in PlayerBots.Presets) names.Add(preset.Name);
            names.Add(PlayerBots.ScenarioName);
            return names.ToArray();
        }

        private void ScenarioField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                scenarioPath = EditorGUILayout.TextField("Файл сценария", scenarioPath);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    string picked = EditorUtility.OpenFilePanel("Сценарий", LogFiles.DefaultFolder, "txt");
                    if (!string.IsNullOrEmpty(picked)) scenarioPath = picked;
                }
            }
        }

        private void RunNow()
        {
            error = string.Empty;
            warning = string.Empty;
            report = string.Empty;
            batch = null;

            Func<PlayerBot> bot;
            if (IsScenarioBot)
            {
                if (!TryLoadScenario(out ScenarioScript script)) return;
                bot = () => PlayerBots.Scenario(script);
            }
            else
            {
                bot = PlayerBots.Presets[botIndex].Create;
            }

            var options = new HeadlessRun.Options
            {
                Seed = (uint)seed,
                Days = days,
                LogLevel = logLevel,
                Runs = runs,
                Bot = bot,
                LogToConsole = logToConsole,
            };

            try
            {
                batch = HeadlessRun.RunToFiles(config, options, (i, total) =>
                    total == 1 || !EditorUtility.DisplayCancelableProgressBar("Run Headless", $"Прогон {i + 1} из {total}", (float)i / total));
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogException(e);
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            report = Report(batch);
            Debug.Log("[GuildMaster] Headless run. " + report);
        }

        private bool TryLoadScenario(out ScenarioScript script)
        {
            script = null;
            if (!File.Exists(scenarioPath))
            {
                error = "Нет файла сценария: " + scenarioPath;
                return false;
            }
            try
            {
                script = ScenarioScript.Parse(File.ReadAllText(scenarioPath));
            }
            catch (FormatException e)
            {
                error = e.Message;
                return false;
            }
            if (script.Seed.HasValue && script.Seed.Value != (uint)seed)
            {
                warning = $"Сценарий записан с зерном {script.Seed.Value}, а задано {seed}: ид кандидатов не совпадут. Зерно взято из сценария.";
                seed = script.Seed.Value;
            }
            return true;
        }

        private static string Report(HeadlessRun.Batch batch)
        {
            var text = new StringBuilder();
            double seconds = 0;
            long lines = 0;
            foreach (HeadlessRun.Result result in batch.Results)
            {
                seconds += result.ElapsedSeconds;
                lines += result.LogLines;
            }

            HeadlessRun.Result first = batch.Results[0];
            if (batch.Results.Count == 1)
            {
                text.AppendFormat(CultureInfo.InvariantCulture, "Зерно {0}: {1} тактов, {2} событий, {3} строк лога, конец — {4}, за {5:0.000} с",
                    first.Seed, first.Ticks, first.Events, lines, first.FinalTime, seconds);
                if (first.GuildClosed) text.Append($"\nГильдия закрыта {first.FinalTime}: игра проиграна.");
                if (first.Scenario.Unrecorded > 0) text.Append($"\nКоманд вне сценария: {first.Scenario.Unrecorded} — сценарий игру не повторит.");
            }
            else
            {
                text.AppendFormat(CultureInfo.InvariantCulture, "{0} прогонов по {1} тактов, {2} строк лога, за {3:0.000} с (в среднем {4:0.000} с)",
                    batch.Results.Count, first.Ticks, lines, seconds, seconds / batch.Results.Count);
                int closed = 0;
                foreach (HeadlessRun.Result result in batch.Results) closed += result.GuildClosed ? 1 : 0;
                if (closed > 0) text.Append($"\nГильдия закрылась в {closed} прогонах.");
            }
            if (batch.Cancelled) text.Append("\nПрервано.");

            text.Append("\nФайлы:");
            int shown = Math.Min(batch.Files.Count, 6);
            for (int i = 0; i < shown; i++) text.Append("\n  ").Append(Path.GetFileName(batch.Files[i]));
            if (batch.Files.Count > shown) text.Append($"\n  … и ещё {batch.Files.Count - shown}");
            return text.ToString();
        }

        private static void DrawSummary(RunSummary summary)
        {
            EditorGUILayout.LabelField("По месяцам", EditorStyles.boldLabel);
            var header = new List<string> { "Месяц", "Дней" };
            foreach (MonthColumn column in summary.MonthColumns) header.Add(column.Name);
            Row(header, bold: true);
            foreach (MonthRow month in summary.Months)
            {
                var row = new List<string> { month.Label, month.Days.ToString(CultureInfo.InvariantCulture) };
                for (int c = 0; c < summary.MonthColumns.Count; c++) row.Add(SummaryCsv.Format(month.Values[c], summary.MonthColumns[c].Format));
                Row(row);
            }
            var totals = new List<string> { "Итого", summary.Days.ToString(CultureInfo.InvariantCulture) };
            for (int c = 0; c < summary.MonthColumns.Count; c++) totals.Add(SummaryCsv.Format(summary.Totals[c], summary.MonthColumns[c].Format));
            Row(totals, bold: true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("По людям", EditorStyles.boldLabel);
            var peopleHeader = new List<string>();
            foreach (PersonColumn column in summary.PersonColumns) peopleHeader.Add(column.Name);
            Row(peopleHeader, bold: true);
            foreach (string[] person in summary.People) Row(person);
        }

        private static void DrawAggregate(SummaryAggregate aggregate)
        {
            EditorGUILayout.LabelField($"{aggregate.Runs.Count} прогонов: среднее ± разброс", EditorStyles.boldLabel);
            var header = new List<string> { "Месяц" };
            foreach (MonthColumn column in aggregate.Columns) header.Add(column.Name);
            Row(header, bold: true);
            for (int m = 0; m < aggregate.Months.Count; m++) Row(StatRow(aggregate.MonthLabels[m], aggregate.Months[m]));
            Row(StatRow("Итого", aggregate.Totals), bold: true);
        }

        private static List<string> StatRow(string label, IReadOnlyList<SummaryStat> stats)
        {
            var row = new List<string> { label };
            foreach (SummaryStat stat in stats) row.Add(SummaryCsv.Format(stat.Mean, "0.#") + " ± " + SummaryCsv.Format(stat.Deviation, "0.#"));
            return row;
        }

        private static void Row(IReadOnlyList<string> cells, bool bold = false)
        {
            GUIStyle style = bold ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel;
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    GUILayout.Label(new GUIContent(cells[i], cells[i]), style, GUILayout.Width(i == 0 ? LabelWidth : CellWidth));
                }
            }
        }
    }
}
#endif
