using System.Collections.Generic;

namespace GuildMaster.Data
{
    /// <summary>
    /// Словарь подстановок в текстах ленты, описаний заказов и дилемм: <c>{имя}</c>, <c>{место:р}</c>,
    /// <c>[м|ж]</c>, <c>[его|её]@имя</c>. Новая метка добавляется сюда; валидатор ловит незнакомые.
    /// </summary>
    public static class TextPlaceholders
    {
        /// <summary>Люди: склоняются и задают род для скобок <c>[м|ж]</c>.</summary>
        private static readonly HashSet<string> people = new HashSet<string>
        {
            "имя", "напарник", "лекарь", "щит", "решающий",
        };

        /// <summary>Прочие метки ленты и описаний заказов.</summary>
        private static readonly HashSet<string> other = new HashSet<string>
        {
            "группа", "место", "враг", "заказчик", "груз",
            "число", "всего", "сумма", "доход", "расход",
            "распоряжение", "постройка", "архетип", "причина", "название",
        };

        /// <summary>Названия с родом из данных: скобка рода к ним — только с явной привязкой <c>@метка</c>.</summary>
        private static readonly HashSet<string> gendered = new HashSet<string>
        {
            "группа", "место", "враг", "заказчик", "груз", "распоряжение", "постройка",
        };

        /// <summary>Только в текстах дилемм (сейчас таких нет: «причина» есть и в ленте — причины ухода).</summary>
        private static readonly HashSet<string> dilemmaOnly = new HashSet<string>();

        public static IReadOnlyCollection<string> People => people;
        public static IReadOnlyCollection<string> Other => other;
        public static IReadOnlyCollection<string> DilemmaOnly => dilemmaOnly;
        public static IReadOnlyCollection<string> Gendered => gendered;

        /// <summary>Буква падежа после двоеточия: <c>{имя:р}</c>. Без буквы — именительный.</summary>
        public static readonly IReadOnlyDictionary<string, GrammaticalCase> CaseSuffixes = new Dictionary<string, GrammaticalCase>
        {
            ["р"] = GrammaticalCase.Genitive,
            ["д"] = GrammaticalCase.Dative,
            ["в"] = GrammaticalCase.Accusative,
            ["т"] = GrammaticalCase.Instrumental,
            ["п"] = GrammaticalCase.Prepositional,
        };

        public static bool IsPerson(string name) => people.Contains(name);

        /// <summary>К метке можно привязать скобку рода: <c>[…]@имя</c>, <c>[…]@постройка</c>.</summary>
        public static bool HasGender(string name) => people.Contains(name) || gendered.Contains(name);

        public static bool IsKnown(string name, bool allowDilemmaOnly) =>
            people.Contains(name) || other.Contains(name) || (allowDilemmaOnly && dilemmaOnly.Contains(name));
    }
}
