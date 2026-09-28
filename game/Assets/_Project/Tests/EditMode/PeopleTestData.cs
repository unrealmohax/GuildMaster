using System;
using GuildMaster.Core;
using GuildMaster.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Данные для тестов с людьми: определения из реального GameConfig (архетипы, черты, оси, имена) и BalanceSettings
    /// со значениями по умолчанию (числа из документов), чтобы правка ассета не ломала тесты. Ассеты на диске не меняются.
    /// </summary>
    internal sealed class PeopleData : IDisposable
    {
        private readonly GameData data = new GameData();

        public PeopleData()
        {
            Balance = ScriptableObject.CreateInstance<BalanceSettings>();
            GameConfig config = data.Copy(data.Config);
            GameData.Edit(config, "balance", p => p.objectReferenceValue = Balance);
            Registry = DataRegistry.FromConfig(config);
        }

        public BalanceSettings Balance { get; }
        public DataRegistry Registry { get; }

        /// <summary>Число баланса по пути сериализованного поля (<c>adventurers.noviceThreshold</c>).</summary>
        public void Set(string path, float value) => GameData.Edit(Balance, path, p => p.floatValue = value);

        public void Set(string path, int value) => GameData.Edit(Balance, path, p => p.intValue = value);

        /// <summary>
        /// Люди не берут заказов: выйти можно только в час начала утра, а решение действует со следующего часа. Для тестов
        /// механик, которым задания мешают (заказы копятся и снимаются по сроку, репутация не меняется).
        /// </summary>
        public void NoQuests() => Set("time.latestDepartureHour", Balance.Time.MorningHour);

        /// <summary>
        /// Казна на старте так велика, что содержание построек и зарплаты не уводят её в минус за долгий прогон: для тестов частот
        /// за годы без доходов (без заданий гильдия иначе разоряется и закрывается, и симуляция встаёт).
        /// </summary>
        public void NoBankruptcy() => Set("guild.startMoney", 10_000_000);

        public void Dispose()
        {
            data.Dispose();
            Object.DestroyImmediate(Balance);
        }
    }
}
