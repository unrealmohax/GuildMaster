#if UNITY_EDITOR
using GuildMaster.Data;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// GuildMaster → Validate Data: проверка данных из GameConfig. Каждая находка — отдельная строка в консоли
    /// (клик выделяет ассет), итог — последней строкой.
    /// </summary>
    public static class DataValidatorMenu
    {
        [MenuItem("GuildMaster/Validate Data")]
        public static void ValidateData()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            DataValidationReport report = DataValidator.Validate(config);

            foreach (DataIssue issue in report.Issues)
            {
                string line = "[GuildMaster] Validate Data: " + issue;
                if (issue.Severity == IssueSeverity.Error) Debug.LogError(line, issue.Asset);
                else Debug.LogWarning(line, issue.Asset);
            }

            string summary = $"[GuildMaster] Validate Data: {(config != null ? config.name : "нет GameConfig")} — ошибок {report.ErrorCount}, предупреждений {report.WarningCount}";
            if (report.HasErrors) Debug.LogError(summary, config);
            else Debug.Log(summary, config);
        }
    }
}
#endif
