using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Имена людей и части названий постоянных групп: «{прилагательное} {существительное}» — «Серые волки».
    /// Название группы в тексты идёт в кавычках и не склоняется: «группа «Серые волки»».
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

        public IReadOnlyList<NounForms> MaleNames => maleNames;
        public IReadOnlyList<NounForms> FemaleNames => femaleNames;
        public IReadOnlyList<string> GroupAdjectives => groupAdjectives;
        public IReadOnlyList<string> GroupNouns => groupNouns;
    }
}
