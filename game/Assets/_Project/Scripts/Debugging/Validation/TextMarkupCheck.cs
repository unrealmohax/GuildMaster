using System.Text.RegularExpressions;
using GuildMaster.Data;
using Object = UnityEngine.Object;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Разметка текстов: подстановки <c>{имя}</c>, <c>{место:р}</c>, скобки рода <c>[м|ж]</c>, <c>[его|её]@имя</c>,
    /// у названий — <c>[м|ж|ср]@постройка</c>, <c>[м|ж|ср|мн]@враг</c>.
    /// Ошибка — незнакомая подстановка, неверный падеж, битые скобки, привязка не к той метке.
    /// Предупреждение — скобка рода без человека в том же предложении (нужна явная привязка <c>@имя</c>).
    /// </summary>
    internal static class TextMarkupCheck
    {
        private static readonly Regex Placeholder = new Regex(@"\{([^{}]*)\}");
        private static readonly Regex GenderBracket = new Regex(@"\[([^\[\]]*)\](?:@(\w+))?");
        private static readonly char[] SentenceEnds = { '.', '!', '?', '…' };

        public static void Run(string text, bool allowDilemmaOnly, Object asset, string path, DataValidationReport report)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (Count(text, '{') != Count(text, '}')) report.Error(asset, path, $"непарные фигурные скобки: «{text}»");
            if (Count(text, '[') != Count(text, ']')) report.Error(asset, path, $"непарные квадратные скобки: «{text}»");

            foreach (Match match in Placeholder.Matches(text))
            {
                string body = match.Groups[1].Value;
                int colon = body.IndexOf(':');
                string name = colon < 0 ? body : body.Substring(0, colon);
                if (!TextPlaceholders.IsKnown(name, allowDilemmaOnly))
                {
                    report.Error(asset, path, $"неизвестная подстановка {match.Value}: «{text}»");
                    continue;
                }
                if (colon >= 0 && !TextPlaceholders.CaseSuffixes.ContainsKey(body.Substring(colon + 1)))
                    report.Error(asset, path, $"неизвестный падеж в {match.Value} (можно р, д, в, т, п): «{text}»");
            }

            foreach (Match match in GenderBracket.Matches(text))
            {
                int forms = match.Groups[1].Value.Split('|').Length;
                string owner = match.Groups[2].Success ? match.Groups[2].Value : null;

                if (owner != null && !TextPlaceholders.HasGender(owner))
                {
                    report.Error(asset, path, $"{match.Value}: после @ нужна метка с родом — человек или название: «{text}»");
                    continue;
                }
                if (owner != null && !text.Contains("{" + owner))
                    report.Error(asset, path, $"{match.Value}: метки {{{owner}}} в строке нет: «{text}»");

                bool person = owner == null || TextPlaceholders.IsPerson(owner);
                if (person && forms != 2)
                {
                    report.Error(asset, path, $"скобка рода {match.Value} у человека — ровно две формы [м|ж]: «{text}»");
                    continue;
                }
                if (!person && (forms < 3 || forms > 4))
                {
                    report.Error(asset, path, $"скобка рода {match.Value} у названия — три или четыре формы [м|ж|ср] / [м|ж|ср|мн]: «{text}»");
                    continue;
                }
                if (owner != null) continue;

                if (!HasPersonInSentence(text, match.Index))
                    report.Warning(asset, path, $"у скобки рода {match.Value} нет человека в том же предложении — нужен @имя: «{text}»");
            }
        }

        private static bool HasPersonInSentence(string text, int index)
        {
            int start = text.LastIndexOfAny(SentenceEnds, index) + 1;
            int end = text.IndexOfAny(SentenceEnds, index);
            if (end < 0) end = text.Length;
            string sentence = text.Substring(start, end - start);

            foreach (Match match in Placeholder.Matches(sentence))
            {
                string body = match.Groups[1].Value;
                int colon = body.IndexOf(':');
                if (TextPlaceholders.IsPerson(colon < 0 ? body : body.Substring(0, colon))) return true;
            }
            return false;
        }

        private static int Count(string text, char c)
        {
            int count = 0;
            foreach (char ch in text)
            {
                if (ch == c) count++;
            }
            return count;
        }
    }
}
