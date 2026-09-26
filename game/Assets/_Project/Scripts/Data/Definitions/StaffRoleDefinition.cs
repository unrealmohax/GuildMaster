using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Должность персонала. Формулы зарплаты и эффекта уровня — <see cref="StaffBalance"/>.</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Staff Role", fileName = "StaffRole")]
    public sealed class StaffRoleDefinition : Definition
    {
        [Tooltip("Базовая зарплата в месяц, монет")]
        [SerializeField, Min(0)] private int baseSalary;

        [Tooltip("Где работает: без готовой постройки вакансии нет")]
        [SerializeField] private BuildingDefinition requiredBuilding;

        [Tooltip("На что влияет уровень возможностей")]
        [SerializeField] private StaffLevelEffect levelEffect;

        [SerializeField, TextArea] private string levelEffectDescription;

        [Tooltip("Есть на старте игры")]
        [SerializeField] private bool hiredAtStart;

        public int BaseSalary => baseSalary;
        public BuildingDefinition RequiredBuilding => requiredBuilding;
        public StaffLevelEffect LevelEffect => levelEffect;
        public string LevelEffectDescription => levelEffectDescription;
        public bool HiredAtStart => hiredAtStart;
    }
}
