using System.Collections.Generic;
using System.Text;
using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>Запись цели ссылки в разметке TMP (<c>&lt;link="adventurer:12"&gt;</c>) и обратно.</summary>
    public static class LinkCodec
    {
        public static string Encode(TextLink link) => link.Kind + ":" + link.Id;

        public static bool TryDecode(string text, out TextLink link)
        {
            link = TextLink.None;
            if (string.IsNullOrEmpty(text)) return false;
            int colon = text.IndexOf(':');
            if (colon <= 0) return false;
            if (!System.Enum.TryParse(text.Substring(0, colon), out TextLinkKind kind)) return false;
            if (!int.TryParse(text.Substring(colon + 1), out int id)) return false;
            link = new TextLink(kind, id);
            return !link.IsNone;
        }

        /// <summary>Текст как ссылка заданного цвета.</summary>
        public static string Wrap(string text, TextLink link, string colorHex) =>
            link.IsNone ? Escape(text) : $"<link=\"{Encode(link)}\"><color={colorHex}>{Escape(text)}</color></link>";

        /// <summary>Убрать из текста угловые скобки, чтобы TMP не принял его за разметку.</summary>
        public static string Escape(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("<", "‹").Replace(">", "›");
    }

    /// <summary>
    /// Строка ленты для экрана: метка времени, текст с кликабельными именами и названиями (по местам ссылок строки),
    /// оформление по важности — [О] серый, [З] цветной, [В] крупно и со значком.
    /// </summary>
    public sealed class FeedFormatter
    {
        public const string ImportantMark = "♦ ";

        private readonly string dimHex;
        private readonly string textHex;
        private readonly string accentHex;
        private readonly float importantSizePercent;

        public FeedFormatter(string dimHex, string textHex, string accentHex, float importantSizePercent = 115f)
        {
            this.dimHex = dimHex;
            this.textHex = textHex;
            this.accentHex = accentHex;
            this.importantSizePercent = importantSizePercent;
        }

        /// <summary>Текст строки с разметкой ссылок, без оформления важности.</summary>
        public static string WithLinks(string text, IReadOnlyList<TextSpan> spans, string linkHex)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (spans == null || spans.Count == 0) return LinkCodec.Escape(text);

            var builder = new StringBuilder(text.Length + spans.Count * 40);
            int at = 0;
            foreach (TextSpan span in spans)
            {
                if (span.Start < at || span.Start + span.Length > text.Length) continue;
                builder.Append(LinkCodec.Escape(text.Substring(at, span.Start - at)));
                builder.Append(LinkCodec.Wrap(text.Substring(span.Start, span.Length), span.Link, linkHex));
                at = span.Start + span.Length;
            }
            builder.Append(LinkCodec.Escape(text.Substring(at)));
            return builder.ToString();
        }

        public string Format(FeedEntry entry, Calendar calendar)
        {
            string stamp = $"<color={dimHex}>{UiFormat.Stamp(calendar.At(entry.TimeHours))}</color>  ";
            // Имена выделяются на фоне своей строки: в обычной и заметной — светлым текстом, в важной — акцентом.
            string body = WithLinks(entry.Text, entry.Spans, entry.Importance == EventImportance.Important ? accentHex : textHex);
            switch (entry.Importance)
            {
                case EventImportance.Important:
                    return $"{stamp}<size={importantSizePercent:0}%><color={accentHex}>{ImportantMark}</color><b><color={textHex}>{body}</color></b></size>";
                case EventImportance.Notable:
                    return $"{stamp}<color={accentHex}>{body}</color>";
                default:
                    return $"{stamp}<color={dimHex}>{body}</color>";
            }
        }
    }
}
