using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Доступ симуляции к определениям из ScriptableObject. Только чтение.
    /// Поиск определений по id добавляется в ТЗ 02 вместе с самими определениями.
    /// </summary>
    public sealed class DataRegistry
    {
        public DataRegistry(BalanceSettings balance)
        {
            Balance = balance != null ? balance : throw new ArgumentNullException(nameof(balance));
        }

        public static DataRegistry FromConfig(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.Balance == null) throw new ArgumentException($"{config.name}: Balance is not set", nameof(config));
            return new DataRegistry(config.Balance);
        }

        public BalanceSettings Balance { get; }
    }
}
