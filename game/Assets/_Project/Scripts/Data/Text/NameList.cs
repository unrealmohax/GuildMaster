using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Имена людей и части названий постоянных групп: «{прилагательное} {существительное}» — «Серые волки».
    /// Название группы в тексты идёт в кавычках и не склоняется: «группа «Серые волки»». Слова родства — для строк о семье.
    /// Названия поселения — одно выбирается в начале игры.
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Name List", fileName = "NameList")]
    public sealed class NameList : ScriptableObject
    {
        [SerializeField] private List<NounForms> maleNames = new List<NounForms>();
        [SerializeField] private List<NounForms> femaleNames = new List<NounForms>();

        [Tooltip("Прилагательные во множественном числе: «Серые»")]
        [SerializeField] private List<string> groupAdjectives = new List<string>();

        [Tooltip("Существительные во множественном числе: «волки»")]
        [SerializeField] private List<string> groupNouns = new List<string>();

        [Tooltip("Родные по порядку: отец, мать, муж, жена, сын, дочь — все шесть падежей и род")]
        [SerializeField] private List<NounForms> relatives = new List<NounForms>();

        [Tooltip("Названия поселения рядом с гильдией — все шесть падежей и род")]
        [SerializeField] private List<NounForms> settlementNames = new List<NounForms>();

        public IReadOnlyList<NounForms> MaleNames => maleNames;
        public IReadOnlyList<NounForms> FemaleNames => femaleNames;
        public IReadOnlyList<string> GroupAdjectives => groupAdjectives;
        public IReadOnlyList<string> GroupNouns => groupNouns;

        /// <summary>Слова родства по порядку: отец, мать, муж, жена, сын, дочь (<see cref="RelativesCount"/>).</summary>
        public IReadOnlyList<NounForms> Relatives => relatives;

        public IReadOnlyList<NounForms> SettlementNames => settlementNames;

        /// <summary>Сколько слов родства должно быть: три роли × два пола.</summary>
        public const int RelativesCount = 6;
    }
}
