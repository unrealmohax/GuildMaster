using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Adventurers</c>: генерация, архетип, оси, особые черты, старт, отношения, раскрытие
    /// (numbers.md → Генерация, ТЗ 04). Шанс типа при генерации — <see cref="ArchetypeDefinition.GenerationWeight"/>.
    /// </summary>
    [Serializable]
    public sealed class AdventurersBalance
    {
        [Header("Генерация")]
        [Tooltip("Шанс мужского пола")]
        [SerializeField, Range(0f, 1f)] private float maleChance = 0.5f;

        [Tooltip("Возраст (только для вида)")]
        [SerializeField] private IntRange age = new IntRange(17, 35);

        [Tooltip("Уровни для всех типов, кроме Новичка: по строке на Weak, Medium, Strong")]
        [SerializeField] private List<GenerationLevel> levels = new List<GenerationLevel>
        {
            new GenerationLevel(AdventurerLevel.Weak, 0.5f, new IntRange(10, 30), new IntRange(10, 25), new IntRange(40, 55), new IntRange(30, 40), new IntRange(25, 40)),
            new GenerationLevel(AdventurerLevel.Medium, 0.35f, new IntRange(20, 40), new IntRange(15, 35), new IntRange(55, 70), new IntRange(40, 50), new IntRange(35, 50)),
            new GenerationLevel(AdventurerLevel.Strong, 0.15f, new IntRange(30, 50), new IntRange(25, 45), new IntRange(70, 85), new IntRange(50, 60), new IntRange(45, 60)),
        };

        [Tooltip("Стартовые люди — не выше этого уровня (prototype.md: «параметры низкие и средние»)")]
        [SerializeField] private AdventurerLevel startMaxLevel = AdventurerLevel.Medium;

        [Tooltip("Новичок (всегда слабый): характеристики")]
        [SerializeField] private IntRange noviceCharacteristics = new IntRange(10, 25);

        [Tooltip("Новичок: навыки")]
        [SerializeField] private IntRange noviceSkills = new IntRange(10, 20);

        [Tooltip("Новичок: сколько случайных параметров чуть выше")]
        [SerializeField] private IntRange novicePeakCount = new IntRange(1, 2);

        [Tooltip("Новичок: значение этих параметров")]
        [SerializeField] private IntRange novicePeak = new IntRange(20, 30);

        [Tooltip("Слаженность (кроме случая, когда она вспомогательный параметр роли)")]
        [SerializeField] private IntRange cohesion = new IntRange(20, 60);

        [Tooltip("Естественный минимум параметров")]
        [SerializeField, Min(0)] private int naturalMinimum = 10;

        [Tooltip("Верх шкалы параметров при генерации; выше — только ростом")]
        [SerializeField, Min(1)] private int statCap = 99;

        [SerializeField] private IntRange wallet = new IntRange(20, 100);

        [Header("Архетип и ранг характеристик")]
        [SerializeField, Range(0f, 1f)] private float roleMainWeight = 0.7f;
        [SerializeField, Range(0f, 1f)] private float roleSecondaryWeight = 0.2f;
        [SerializeField, Range(0f, 1f)] private float roleRestWeight = 0.1f;

        [Tooltip("Лучшая оценка роли ниже — Новичок")]
        [SerializeField, Min(0f)] private float noviceThreshold = 25f;

        [Tooltip("Лучшая оценка выше среднего всех 14 меньше чем на это — Мастер на все руки")]
        [SerializeField, Min(0f)] private float jackOfAllTradesThreshold = 10f;

        [Header("Оси характера")]
        [SerializeField] private float axisMean = 0f;
        [SerializeField, Min(0f)] private float axisStdDev = 45f;

        [Tooltip("Крайний полюс: |значение| от")]
        [SerializeField, Range(0f, 100f)] private float extremePoleThreshold = 70f;

        [Tooltip("Нейтральная ось: |значение| ниже; раскрывать нечего")]
        [SerializeField, Range(0f, 100f)] private float neutralThreshold = 30f;

        [Tooltip("Нейтральная ось раскрывается как «уравновешен» через, дней в гильдии")]
        [SerializeField, Min(0)] private int neutralRevealDays = 60;

        [Header("Особые черты")]
        [Tooltip("Особых черт у человека не больше")]
        [SerializeField, Min(0)] private int maxSpecialTraits = 4;

        [Tooltip("Черт «от рождения» у стартовых")]
        [SerializeField] private IntRange birthTraitsAtStart = new IntRange(0, 1);

        [Tooltip("Черт «от рождения» у пришедших позже")]
        [SerializeField] private IntRange birthTraitsLater = new IntRange(0, 2);

        [Header("Старт")]
        [SerializeField, Min(1)] private int startAdventurers = 6;

        [Tooltip("Сколько стартовых — ранга F")]
        [SerializeField] private IntRange startRankF = new IntRange(1, 2);

        [Tooltip("У ранга F поднятые параметры +")]
        [SerializeField, Min(0)] private int rankFBoost = 10;

        [Tooltip("Особых черт на всех стартовых")]
        [SerializeField] private IntRange startTraitsTotal = new IntRange(2, 3);

        [Tooltip("Готовых пар отношений")]
        [SerializeField] private IntRange startPairs = new IntRange(1, 2);

        [Tooltip("«Старые друзья»: отношения")]
        [SerializeField, Range(-100f, 100f)] private float oldFriendsRelation = 50f;

        [Tooltip("Не больше стольких стартовых на одном крайнем полюсе")]
        [SerializeField, Min(1)] private int maxStartPerExtremePole = 2;

        [Tooltip("Кандидат в авантюристы ждёт ответа, дней")]
        [SerializeField, Min(1)] private int candidateWaitDays = 3;

        [Header("Отношения")]
        [SerializeField, Range(-100f, 100f)] private float friendsThreshold = 40f;
        [SerializeField, Range(-100f, 100f)] private float dislikeThreshold = -40f;

        [Tooltip("Давние напарники: совместных заданий от")]
        [SerializeField, Min(1)] private int longPartnersQuests = 10;

        [SerializeField] private float tavernEveningRelation = 0.5f;
        [SerializeField] private float jointSuccessRelation = 3f;
        [SerializeField] private float jointHardQuestRelation = 2f;
        [SerializeField] private float quarrelRelation = -10f;

        [Tooltip("Ссора вечером: шанс")]
        [SerializeField, Range(0f, 1f)] private float quarrelChance = 0.05f;

        [Tooltip("Ссора возможна при отношениях ниже (или у Соперников)")]
        [SerializeField, Range(-100f, 100f)] private float quarrelRelationBelow = -20f;

        [Tooltip("Бегство: отношения брошенных к беглецу")]
        [SerializeField] private float fleeAbandonedRelation = -20f;

        [Header("Раскрытие осей")]
        [Tooltip("Бескорыстный: доля награды ниже этой доли среднего для ранга")]
        [SerializeField, Range(0f, 1f)] private float selflessShareOfAverage = 0.5f;

        [Tooltip("Ленивый: дней подряд без заданий и тренировок")]
        [SerializeField, Min(1)] private int lazyIdleDays = 7;

        [Tooltip("Преданный: остался при лояльности ниже")]
        [SerializeField, Range(0f, 100f)] private float loyalStayLoyaltyBelow = 40f;

        [Tooltip("Беспринципный: шанс, что утаивание заметят")]
        [SerializeField, Range(0f, 1f)] private float skimCaughtChance = 0.3f;

        [Tooltip("Честный: дней без утаивания при крайнем полюсе")]
        [SerializeField, Min(1)] private int honestNoSkimDays = 60;

        public float MaleChance => maleChance;
        public IntRange Age => age;
        public IReadOnlyList<GenerationLevel> Levels => levels;
        public AdventurerLevel StartMaxLevel => startMaxLevel;
        public IntRange NoviceCharacteristics => noviceCharacteristics;
        public IntRange NoviceSkills => noviceSkills;
        public IntRange NovicePeakCount => novicePeakCount;
        public IntRange NovicePeak => novicePeak;
        public IntRange Cohesion => cohesion;
        public int NaturalMinimum => naturalMinimum;
        public int StatCap => statCap;
        public IntRange Wallet => wallet;
        public float RoleMainWeight => roleMainWeight;
        public float RoleSecondaryWeight => roleSecondaryWeight;
        public float RoleRestWeight => roleRestWeight;
        public float NoviceThreshold => noviceThreshold;
        public float JackOfAllTradesThreshold => jackOfAllTradesThreshold;
        public float AxisMean => axisMean;
        public float AxisStdDev => axisStdDev;
        public float ExtremePoleThreshold => extremePoleThreshold;
        public float NeutralThreshold => neutralThreshold;
        public int NeutralRevealDays => neutralRevealDays;
        public int MaxSpecialTraits => maxSpecialTraits;
        public IntRange BirthTraitsAtStart => birthTraitsAtStart;
        public IntRange BirthTraitsLater => birthTraitsLater;
        public int StartAdventurers => startAdventurers;
        public IntRange StartRankF => startRankF;
        public int RankFBoost => rankFBoost;
        public IntRange StartTraitsTotal => startTraitsTotal;
        public IntRange StartPairs => startPairs;
        public float OldFriendsRelation => oldFriendsRelation;
        public int MaxStartPerExtremePole => maxStartPerExtremePole;
        public int CandidateWaitDays => candidateWaitDays;
        public float FriendsThreshold => friendsThreshold;
        public float DislikeThreshold => dislikeThreshold;
        public int LongPartnersQuests => longPartnersQuests;
        public float TavernEveningRelation => tavernEveningRelation;
        public float JointSuccessRelation => jointSuccessRelation;
        public float JointHardQuestRelation => jointHardQuestRelation;
        public float QuarrelRelation => quarrelRelation;
        public float QuarrelChance => quarrelChance;
        public float QuarrelRelationBelow => quarrelRelationBelow;
        public float FleeAbandonedRelation => fleeAbandonedRelation;
        public float SelflessShareOfAverage => selflessShareOfAverage;
        public int LazyIdleDays => lazyIdleDays;
        public float LoyalStayLoyaltyBelow => loyalStayLoyaltyBelow;
        public float SkimCaughtChance => skimCaughtChance;
        public int HonestNoSkimDays => honestNoSkimDays;
    }

    public enum AdventurerLevel
    {
        Weak = 0,
        Medium = 1,
        Strong = 2,
    }

    /// <summary>Уровень при генерации: шанс и диапазоны параметров для ролей и Мастера на все руки.</summary>
    [Serializable]
    public sealed class GenerationLevel
    {
        [SerializeField] private AdventurerLevel level;
        [SerializeField, Min(0f)] private float weight;

        [Tooltip("Прочие характеристики")]
        [SerializeField] private IntRange characteristics;

        [Tooltip("Прочие навыки")]
        [SerializeField] private IntRange skills;

        [Tooltip("Основные параметры роли")]
        [SerializeField] private IntRange mainStats;

        [Tooltip("Вспомогательные параметры роли")]
        [SerializeField] private IntRange secondaryStats;

        [Tooltip("Мастер на все руки: все 14 параметров")]
        [SerializeField] private IntRange jackOfAllTrades;

        public GenerationLevel()
        {
        }

        public GenerationLevel(AdventurerLevel level, float weight, IntRange characteristics, IntRange skills,
            IntRange mainStats, IntRange secondaryStats, IntRange jackOfAllTrades)
        {
            this.level = level;
            this.weight = weight;
            this.characteristics = characteristics;
            this.skills = skills;
            this.mainStats = mainStats;
            this.secondaryStats = secondaryStats;
            this.jackOfAllTrades = jackOfAllTrades;
        }

        public AdventurerLevel Level => level;
        public float Weight => weight;
        public IntRange Characteristics => characteristics;
        public IntRange Skills => skills;
        public IntRange MainStats => mainStats;
        public IntRange SecondaryStats => secondaryStats;
        public IntRange JackOfAllTrades => jackOfAllTrades;
    }
}
