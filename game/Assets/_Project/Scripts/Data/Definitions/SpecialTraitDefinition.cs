using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Особая черта. Числа особых правил — <see cref="TraitsBalance"/>.</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Special Trait", fileName = "Trait")]
    public sealed class SpecialTraitDefinition : Definition
    {
        [Tooltip("Название для женщины, если отличается («Семейная»)")]
        [SerializeField] private string displayNameFemale;

        [SerializeField] private TraitCategory category;

        [Tooltip("Приобретённая: появляется в игре, заменяет старую приобретённую")]
        [SerializeField] private bool isAcquired;

        [SerializeField] private List<TraitEffect> effects = new List<TraitEffect>();

        [Tooltip("Особые правила, которые делает код")]
        [SerializeField] private List<TraitHook> codeHooks = new List<TraitHook>();

        [Tooltip("Связана с другим человеком (Соперник, Влюблённый)")]
        [SerializeField] private bool requiresPartner;

        [SerializeField] private List<SpecialTraitDefinition> incompatibleWith = new List<SpecialTraitDefinition>();

        [SerializeField] private RevealTrigger revealTrigger;

        [Tooltip("Ключ строки раскрытия в FeedTemplateSet")]
        [SerializeField] private string revealFeedKey;

        public string DisplayNameFemale => string.IsNullOrEmpty(displayNameFemale) ? DisplayName : displayNameFemale;
        public TraitCategory Category => category;
        public bool IsAcquired => isAcquired;
        public IReadOnlyList<TraitEffect> Effects => effects;
        public IReadOnlyList<TraitHook> CodeHooks => codeHooks;
        public bool RequiresPartner => requiresPartner;
        public IReadOnlyList<SpecialTraitDefinition> IncompatibleWith => incompatibleWith;
        public RevealTrigger RevealTrigger => revealTrigger;
        public string RevealFeedKey => revealFeedKey;

        public bool HasHook(TraitHook hook) => codeHooks.Contains(hook);
    }
}
