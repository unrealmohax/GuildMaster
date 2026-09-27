using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Growth</c>: рост параметров.</summary>
    [Serializable]
    public sealed class GrowthBalance
    {
        [Tooltip("Навык за день тренировки: это × (1 − значение/100)")]
        [SerializeField, Min(0f)] private float trainSkillPerDay = 0.3f;

        [Tooltip("Характеристика растёт медленнее навыка во столько раз")]
        [SerializeField, Min(1f)] private float characteristicSlowdown = 3f;

        [Tooltip("Выше 99: рост ×")]
        [SerializeField, Range(0f, 1f)] private float above99Multiplier = 0.1f;

        [Tooltip("Часов на дворе = 1 день тренировки")]
        [SerializeField, Min(1)] private int trainingHoursPerDay = 8;

        [Tooltip("Опыт по осям задания: успех")]
        [SerializeField, Min(0f)] private float questExperienceSuccess = 0.5f;

        [Tooltip("Опыт по осям задания: провал")]
        [SerializeField, Min(0f)] private float questExperienceFailure = 0.3f;

        [Tooltip("Хладнокровие за задание с провалом раунда")]
        [SerializeField, Min(0f)] private float composurePerHardQuest = 0.3f;

        [Tooltip("Слаженность за групповое задание")]
        [SerializeField, Min(0f)] private float cohesionPerGroupQuest = 0.5f;

        [Tooltip("Слаженность за задание постоянной группой")]
        [SerializeField, Min(0f)] private float cohesionPerPermanentPartyQuest = 1f;

        public float TrainSkillPerDay => trainSkillPerDay;
        public float CharacteristicSlowdown => characteristicSlowdown;
        public float Above99Multiplier => above99Multiplier;
        public int TrainingHoursPerDay => trainingHoursPerDay;
        public float QuestExperienceSuccess => questExperienceSuccess;
        public float QuestExperienceFailure => questExperienceFailure;
        public float ComposurePerHardQuest => composurePerHardQuest;
        public float CohesionPerGroupQuest => cohesionPerGroupQuest;
        public float CohesionPerPermanentPartyQuest => cohesionPerPermanentPartyQuest;
    }
}
