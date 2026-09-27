using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Общая часть определений: неизменный строковый id латиницей (по нему ищет <c>DataRegistry</c>)
    /// и название для игрока. Определение описывает, что бывает, а не что сейчас; во время игры не меняется.
    /// </summary>
    public abstract class Definition : ScriptableObject
    {
        [Tooltip("Неизменный id латиницей, уникален среди определений своего вида")]
        [SerializeField] private string id;

        [Tooltip("Название для игрока")]
        [SerializeField] private string displayName;

        public string Id => id;
        public string DisplayName => displayName;

        public override string ToString() => $"{GetType().Name}({id})";
    }
}
