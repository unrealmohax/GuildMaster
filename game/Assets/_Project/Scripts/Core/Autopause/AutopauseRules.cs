using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Какие события ставят автопаузу и под каким переключателем (ТЗ 03, таблица «Автопауза»).
    /// Система, которая заводит такое событие в своём ТЗ, добавляет сюда одну строку
    /// <c>{ SimEventType.Тип, AutopauseKind.Вид },</c>. Одному виду может соответствовать несколько событий.
    /// </summary>
    public static class AutopauseRules
    {
        public static IReadOnlyDictionary<SimEventType, AutopauseKind> Default { get; } =
            new Dictionary<SimEventType, AutopauseKind>
            {
                // Пока пусто: событий из таблицы ещё нет. Ждут — ТЗ 04 (раскрытие черты), 05 (уход из гильдии),
                // 09 (гибель, бегство, катастрофа, отступление), 10 (увольнение, банкротство), 13 (обращение).
            };
    }
}
