using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Постройка гильдии.</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Building", fileName = "Building")]
    public sealed class BuildingDefinition : Definition
    {
        [Tooltip("Падежные формы названия для {постройка}")]
        [SerializeField] private NounForms nameForms = new NounForms();

        [Tooltip("Цена стройки, монет")]
        [SerializeField, Min(0)] private int cost;

        [Tooltip("Срок стройки, дней")]
        [SerializeField, Min(0)] private int buildDays;

        [Tooltip("Содержание в месяц, монет")]
        [SerializeField, Min(0)] private int upkeepPerMonth;

        [Tooltip("Мест, коек. 0 — без ограничения")]
        [SerializeField, Min(0)] private int capacity;

        [SerializeField] private bool builtAtStart;

        [Tooltip("Какая должность здесь работает (пусто — никакая)")]
        [SerializeField, OptionalReference] private StaffRoleDefinition staffRole;

        [Tooltip("Улучшение уровня: цена ×")]
        [SerializeField, Min(1f)] private float levelCostMultiplier = 1f;

        [Tooltip("Улучшение уровня: срок ×")]
        [SerializeField, Min(1f)] private float levelTimeMultiplier = 1f;

        public NounForms NameForms => nameForms;
        public int Cost => cost;
        public int BuildDays => buildDays;
        public int UpkeepPerMonth => upkeepPerMonth;
        public int Capacity => capacity;
        public bool BuiltAtStart => builtAtStart;
        public StaffRoleDefinition StaffRole => staffRole;
        public float LevelCostMultiplier => levelCostMultiplier;
        public float LevelTimeMultiplier => levelTimeMultiplier;
    }
}
