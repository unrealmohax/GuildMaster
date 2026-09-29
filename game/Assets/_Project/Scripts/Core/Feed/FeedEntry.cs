using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Строка ленты: готовый текст, участники и места в тексте со ссылками на объекты — для перехода по клику.</summary>
    public sealed class FeedEntry
    {
        internal FeedEntry(long timeHours, FeedKind feed, EventImportance importance, string text, int[] links, string templateKey,
            int questRunId = 0, TextSpan[] spans = null)
        {
            Spans = Array.AsReadOnly(spans ?? Array.Empty<TextSpan>());
            QuestRunId = questRunId;
            TimeHours = timeHours;
            Feed = feed;
            Importance = importance;
            Text = text;
            Links = Array.AsReadOnly(links);
            TemplateKey = templateKey;
        }

        /// <summary>Когда случилось событие строки.</summary>
        public long TimeHours { get; }

        public FeedKind Feed { get; }

        /// <summary>[О] обычное, [З] заметное, [В] важное.</summary>
        public EventImportance Importance { get; }

        public string Text { get; }

        /// <summary>Id людей и других участников события строки.</summary>
        public IReadOnlyList<int> Links { get; }

        /// <summary>Где в <see cref="Text"/> стоят имена и названия со ссылками, по порядку.</summary>
        public IReadOnlyList<TextSpan> Spans { get; }

        /// <summary>Ключ шаблона — для отладки.</summary>
        public string TemplateKey { get; }

        /// <summary>Задание, к которому относится строка (<see cref="QuestRun.Id"/>); 0 — строка не о задании.</summary>
        public int QuestRunId { get; }
    }
}
