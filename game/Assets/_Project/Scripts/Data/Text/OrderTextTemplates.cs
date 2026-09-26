using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Тексты заказов (ТЗ 08): заказчики, места, враги и грузы по типам, шаблоны описаний, намёки по осям.
    /// Подстановки — как в ленте (<see cref="TextPlaceholders"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Order Text Templates", fileName = "OrderTextTemplates")]
    public sealed class OrderTextTemplates : ScriptableObject
    {
        [Tooltip("Заказчики для {заказчик}")]
        [SerializeField] private List<NounForms> clients = new List<NounForms>();

        [SerializeField] private List<QuestTypeTexts> questTypes = new List<QuestTypeTexts>();

        [Tooltip("Намёки на самые большие оси профиля")]
        [SerializeField] private List<AxisHint> hints = new List<AxisHint>();

        [Tooltip("Намёк для дальнего заказа")]
        [SerializeField] private string farHint;

        [Tooltip("Намёк на потолок размера группы")]
        [SerializeField] private string partySizeCeilingHint;

        public IReadOnlyList<NounForms> Clients => clients;
        public IReadOnlyList<QuestTypeTexts> QuestTypes => questTypes;
        public IReadOnlyList<AxisHint> Hints => hints;
        public string FarHint => farHint;
        public string PartySizeCeilingHint => partySizeCeilingHint;
    }

    [Serializable]
    public sealed class QuestTypeTexts
    {
        [SerializeField] private QuestTypeDefinition questType;

        [Tooltip("Места для {место}")]
        [SerializeField] private List<NounForms> places = new List<NounForms>();

        [Tooltip("Противники для {враг}")]
        [SerializeField] private List<NounForms> enemies = new List<NounForms>();

        [Tooltip("Грузы для {груз} (сопровождение, доставка)")]
        [SerializeField] private List<NounForms> cargo = new List<NounForms>();

        [Tooltip("Шаблоны описания заказа")]
        [SerializeField] private List<string> descriptions = new List<string>();

        public QuestTypeDefinition QuestType => questType;
        public IReadOnlyList<NounForms> Places => places;
        public IReadOnlyList<NounForms> Enemies => enemies;
        public IReadOnlyList<NounForms> Cargo => cargo;
        public IReadOnlyList<string> Descriptions => descriptions;
    }

    [Serializable]
    public sealed class AxisHint
    {
        [SerializeField] private StatId stat;
        [SerializeField] private List<string> lines = new List<string>();

        public StatId Stat => stat;
        public IReadOnlyList<string> Lines => lines;
    }
}
