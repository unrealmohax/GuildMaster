using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Распоряжение (laws.md, ТЗ 12). Цены и множители эффекта — <see cref="DecreesBalance"/>.</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Decree", fileName = "Decree")]
    public sealed class DecreeDefinition : Definition
    {
        [Tooltip("Номер закона в laws.md")]
        [SerializeField, Min(1)] private int lawNumber = 1;

        [Tooltip("Падежные формы названия для {распоряжение}")]
        [SerializeField] private NounForms nameForms = new NounForms();

        [SerializeField, TextArea] private string description;
        [SerializeField, TextArea] private string plusText;
        [SerializeField, TextArea] private string minusText;

        [SerializeField] private DecreeScopeKind scopeKind;

        [Tooltip("Область по умолчанию (для Ranks)")]
        [SerializeField] private List<GuildRank> defaultRanks = new List<GuildRank>();

        [SerializeField] private List<DecreeDuration> allowedDurations = new List<DecreeDuration>();

        [Tooltip("Льгота: отмена игроком вызывает недовольство")]
        [SerializeField] private bool isBenefit;

        public int LawNumber => lawNumber;
        public NounForms NameForms => nameForms;
        public string Description => description;
        public string PlusText => plusText;
        public string MinusText => minusText;
        public DecreeScopeKind ScopeKind => scopeKind;
        public IReadOnlyList<GuildRank> DefaultRanks => defaultRanks;
        public IReadOnlyList<DecreeDuration> AllowedDurations => allowedDurations;
        public bool IsBenefit => isBenefit;
    }
}
