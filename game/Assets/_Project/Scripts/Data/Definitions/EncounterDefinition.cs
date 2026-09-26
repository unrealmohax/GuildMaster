using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Общая часть события в пути и находки: один раунд со своим профилем (ТЗ 09).
    /// Профиль: оси <see cref="Axes"/> со значениями строки <see cref="ValueSource"/> ранга задания,
    /// сдвинутого на <see cref="RankOffset"/> (в пределах G–C).
    /// </summary>
    public abstract class EncounterDefinition : Definition
    {
        [Tooltip("Оси профиля")]
        [SerializeField] private List<StatId> axes = new List<StatId>();

        [Tooltip("Значения: главных или второстепенных осей ранга")]
        [SerializeField] private RankValueSource valueSource;

        [Tooltip("Сдвиг ранга относительно ранга задания («истинный ранг» пещеры)")]
        [SerializeField] private IntRange rankOffset;

        [Tooltip("Исходы при успехе: ровно один по шансам (пусто — ничего)")]
        [SerializeField] private List<OutcomeChance> successOutcomes = new List<OutcomeChance>();

        [Tooltip("Исходы при провале: ровно один по шансам")]
        [SerializeField] private List<OutcomeChance> failureOutcomes = new List<OutcomeChance>();

        public IReadOnlyList<StatId> Axes => axes;
        public RankValueSource ValueSource => valueSource;
        public IntRange RankOffset => rankOffset;
        public IReadOnlyList<OutcomeChance> SuccessOutcomes => successOutcomes;
        public IReadOnlyList<OutcomeChance> FailureOutcomes => failureOutcomes;
    }

    /// <summary>Один исход из списка взаимоисключающих; шансы списка в сумме — 1.</summary>
    [Serializable]
    public sealed class OutcomeChance
    {
        [SerializeField] private OutcomeKind kind;
        [SerializeField, Range(0f, 1f)] private float chance;

        [Tooltip("Скольким людям (раны, гибель)")]
        [SerializeField, Min(1)] private int count = 1;

        [Tooltip("Раненый поворачивает назад один («не дошёл»)")]
        [SerializeField] private bool turnsBack;

        [Tooltip("Loot: добыча — доля награды задания")]
        [SerializeField] private FloatRange lootShareOfReward;

        public OutcomeKind Kind => kind;
        public float Chance => chance;
        public int Count => count;
        public bool TurnsBack => turnsBack;
        public FloatRange LootShareOfReward => lootShareOfReward;
    }
}
