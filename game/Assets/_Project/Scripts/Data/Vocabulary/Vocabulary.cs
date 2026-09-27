namespace GuildMaster.Data
{
    // Словарь кодовых имён игры. Значения перечислений записаны в ассетах числами:
    // новые значения — только в конец, существующие не менять и не переставлять.

    /// <summary>14 параметров человека: 8 характеристик и 6 навыков. Индекс массива параметров.</summary>
    public enum StatId
    {
        Strength = 0,
        Endurance = 1,
        Agility = 2,
        Reaction = 3,
        Perception = 4,
        Composure = 5,
        Charisma = 6,
        /// <summary>Множитель в группе, не ось диаграммы задания.</summary>
        Cohesion = 7,
        Marksmanship = 8,
        Stealth = 9,
        Knowledge = 10,
        Survival = 11,
        Medicine = 12,
        Crafting = 13,
    }

    /// <summary>Шесть осей характера, −100..+100.</summary>
    public enum AxisId
    {
        Risk = 0,
        Money = 1,
        People = 2,
        Work = 3,
        Loyalty = 4,
        Principles = 5,
    }

    /// <summary>Полюс оси: отрицательный (−100) или положительный (+100).</summary>
    public enum AxisPole
    {
        Negative = 0,
        Positive = 1,
    }

    /// <summary>Шесть мотивов модели решений.</summary>
    public enum Motive
    {
        Money = 0,
        Glory = 1,
        Safety = 2,
        Companions = 3,
        Rest = 4,
        Comfort = 5,
    }

    /// <summary>Ранги гильдии прототипа. Номер ранга для формул — значение + 1 (G = 1 … C = 5).</summary>
    public enum GuildRank
    {
        G = 0,
        F = 1,
        E = 2,
        D = 3,
        C = 4,
    }

    /// <summary>Падеж для подстановок в тексты.</summary>
    public enum GrammaticalCase
    {
        Nominative = 0,
        Genitive = 1,
        Dative = 2,
        Accusative = 3,
        Instrumental = 4,
        Prepositional = 5,
    }

    /// <summary>Род названия для скобок <c>[м|ж|ср|мн]@метка</c>. Не задан — ошибка валидатора.</summary>
    public enum GrammaticalGender
    {
        Unspecified = 0,
        Masculine = 1,
        Feminine = 2,
        Neuter = 3,
        Plural = 4,
    }

    public static class Vocabulary
    {
        public const int StatCount = 14;
        public const int AxisCount = 6;
        public const int RankCount = 5;
        public const int MotiveCount = 6;

        /// <summary>Характеристика (а не навык): для роста «в 3 раза медленнее» и для Калеки.</summary>
        public static bool IsCharacteristic(StatId stat) => stat <= StatId.Cohesion;

        /// <summary>Ось диаграммы задания: все параметры, кроме Слаженности (она множитель группы).</summary>
        public static bool IsDiagramAxis(StatId stat) => stat != StatId.Cohesion;

        /// <summary>Номер ранга для формул: G = 1 … C = 5.</summary>
        public static int Number(GuildRank rank) => (int)rank + 1;
    }
}
