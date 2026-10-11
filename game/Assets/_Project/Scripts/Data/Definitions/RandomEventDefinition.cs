using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Случайное событие в пути: засада, звери. Шансы события — <see cref="TravelBalance"/>.</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Random Event", fileName = "RandomEvent")]
    public sealed class RandomEventDefinition : EncounterDefinition
    {
        [Tooltip("Противники для {враг}")]
        [SerializeField] private List<NounForms> enemies = new List<NounForms>();

        [SerializeField] private string startFeedKey;
        [SerializeField] private string successFeedKey;
        [SerializeField] private string failFeedKey;

        [Tooltip("Провал события может стоить группе части пайков")]
        [SerializeField] private bool losesRations;

        public IReadOnlyList<NounForms> Enemies => enemies;
        public string StartFeedKey => startFeedKey;
        public string SuccessFeedKey => successFeedKey;
        public string FailFeedKey => failFeedKey;
        public bool LosesRations => losesRations;
    }
}
