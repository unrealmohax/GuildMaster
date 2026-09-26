using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Корневой ассет данных: ссылается на все остальные определения. <c>DataRegistry</c> строится из него при старте.
    /// Остальные ссылки добавляются в ТЗ 02.
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField] private BalanceSettings balance;

        public BalanceSettings Balance => balance;
    }
}
