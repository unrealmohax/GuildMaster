using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Все числа баланса. Разделы повторяют docs/content/numbers.md (полный состав — ТЗ 02).
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Balance Settings", fileName = "BalanceSettings")]
    public sealed class BalanceSettings : ScriptableObject
    {
        [SerializeField] private TimeBalance time = new TimeBalance();

        public TimeBalance Time => time;
    }
}
