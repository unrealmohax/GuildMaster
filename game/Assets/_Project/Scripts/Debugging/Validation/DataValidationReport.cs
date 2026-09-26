using System.Collections.Generic;
using System.Linq;
using System.Text;
using Object = UnityEngine.Object;

namespace GuildMaster.Debugging
{
    public enum IssueSeverity
    {
        /// <summary>Данные битые: игра упадёт или поведёт себя не по дизайну.</summary>
        Error,
        /// <summary>Подозрительно, но играть можно (например, работа GM-14 по падежам и роду ещё впереди).</summary>
        Warning,
    }

    /// <summary>Одна находка валидатора: где (ассет и путь к полю) и что не так.</summary>
    public readonly struct DataIssue
    {
        public DataIssue(IssueSeverity severity, Object asset, string path, string message)
        {
            Severity = severity;
            Asset = asset;
            Path = path;
            Message = message;
        }

        public IssueSeverity Severity { get; }
        public Object Asset { get; }
        public string Path { get; }
        public string Message { get; }

        public override string ToString()
        {
            string assetName = Asset != null ? Asset.name : "?";
            return string.IsNullOrEmpty(Path) ? $"{assetName}: {Message}" : $"{assetName} → {Path}: {Message}";
        }
    }

    public sealed class DataValidationReport
    {
        private readonly List<DataIssue> issues = new List<DataIssue>();

        public IReadOnlyList<DataIssue> Issues => issues;
        public IEnumerable<DataIssue> Errors => issues.Where(i => i.Severity == IssueSeverity.Error);
        public IEnumerable<DataIssue> Warnings => issues.Where(i => i.Severity == IssueSeverity.Warning);
        public int ErrorCount => issues.Count(i => i.Severity == IssueSeverity.Error);
        public int WarningCount => issues.Count(i => i.Severity == IssueSeverity.Warning);
        public bool HasErrors => ErrorCount > 0;

        public void Error(Object asset, string path, string message) => issues.Add(new DataIssue(IssueSeverity.Error, asset, path, message));
        public void Warning(Object asset, string path, string message) => issues.Add(new DataIssue(IssueSeverity.Warning, asset, path, message));

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append($"Ошибок: {ErrorCount}, предупреждений: {WarningCount}");
            foreach (DataIssue issue in issues)
            {
                text.Append('\n').Append(issue.Severity == IssueSeverity.Error ? "[Ошибка] " : "[Предупреждение] ").Append(issue);
            }
            return text.ToString();
        }
    }
}
