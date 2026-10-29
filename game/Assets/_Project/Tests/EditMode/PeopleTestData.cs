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
        /// механик, которым задания мешают (заказы копятся и снимаются по сроку, репутация не меняется — <see cref="StableReputation"/>).
        /// Без заданий гильдия не защищает поселение — оно не давит (<see cref="NoSettlementPressure"/>), иначе через несколько
        /// месяцев его покинули бы и симуляция встала.
        /// </summary>
        public void NoQuests()
        {
            Set("time.latestDepartureHour", Balance.Time.MorningHour);
            NoSettlementPressure();
            StableReputation();
        }

        /// <summary>
        /// Репутация не меняется сама: нет остывания в дни без заданий и потерь за уход людей. Для тестов, которым нужна
        /// постоянная репутация (поток заказов, шанс кандидата), — без заданий она иначе остывала бы каждые сутки.
        /// </summary>
        public void StableReputation()
        {
            Set("guild.reputationDecayPerDay", 0f);
            Set("guild.leaveReputation", 0f);
            Set("guild.fleeReputation", 0f);
        }

        /// <summary>
        /// Поселение не меняется само: нет суточного спада Безопасности, сдвигов за заказы, роста населения и дохода таверны
        /// от жителей, кризисы ничего не меняют (<see cref="NoCrises"/>). Для тестов других механик, которым поселение мешает
        /// (казна, долгие прогоны).
        /// </summary>
        public void NoSettlementPressure()
        {
            foreach (string path in new[]
            {
                "settlement.dailyDecay", "settlement.threatDonePerRank", "settlement.otherDonePerRank", "settlement.failedPerRank",
                "settlement.partyLost", "settlement.threatMissedPerRank", "settlement.calmGrowth", "settlement.localsTavernIncome",
            })
                Set(path, 0f);
            Set("settlement.populationPerBuilding", 0);
            NoCrises();
        }

        /// <summary>
        /// Кризисы по календарю приходят, но ничего не меняют: налог 0, мора нет, зима не дороже и не реже, заказ набега не снимается
        /// по сроку, а его итог Безопасность и население не трогает. Для долгих прогонов других механик.
        /// </summary>
        public void NoCrises()
        {
            foreach (string path in new[]
            {
                "crises.raidWonSecurity", "crises.raidLostSecurity", "crises.raidLostPopulationShare", "crises.taxShareOfIncome",
                "crises.taxUnpaidReputation", "crises.taxUnpaidSecurity", "crises.plagueChance", "crises.winterCityHousing",
            })
                Set(path, 0f);
            Set("crises.taxBase", 0);
            Set("crises.raidDays", 100000);
            foreach (string path in new[] { "crises.winterFoodPriceMultiplier", "crises.winterOrdersMultiplier", "crises.winterDecayMultiplier" })
                Set(path, 1f);
        }

        /// <summary>
        /// Казна на старте так велика, что содержание построек и зарплаты не уводят её в минус за долгий прогон: для тестов частот
        /// за годы без доходов (без заданий гильдия иначе разоряется и закрывается, и симуляция встаёт).
        /// </summary>
        public void NoBankruptcy() => Set("guild.startMoney", 10_000_000);

        /// <summary>
        /// Молва всегда × 1: шанс кандидата не зависит от довольства людей. Для тестов, где системы состояния, решений или еды
        /// не должны сдвигать броски притока.
        /// </summary>
        public void NeutralRumor()
        {
            Set("guild.rumorBase", 1f);
            Set("guild.rumorDivisor", float.MaxValue);
        }

        public void Dispose()
        {
            data.Dispose();
            Object.DestroyImmediate(Balance);
        }
    }
}
