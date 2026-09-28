using System;
using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Раны и лечение; Лазарет — подмена (<see cref="FakeInfirmary"/>), без построек и персонала.</summary>
    public sealed class HealthTests
    {
        private StateWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        private static Condition Wound(StateWorld w, Adventurer adventurer, ConditionKind kind) =>
            w.Do(ctx => HealthService.Wound(ctx, adventurer, kind));

        /// <summary>Сколько суточных тактов (00:00) до заживления раны.</summary>
        private static int MidnightsToHeal(StateWorld w, Adventurer adventurer, Condition condition)
        {
            int midnights = 0;
            while (adventurer.State.Conditions.Contains(condition))
            {
                w.TickToHour(0);
                midnights++;
                if (midnights > 100) throw new InvalidOperationException("wound does not heal");
            }
            return midnights;
        }

        [Test]
        public void WoundDays_InRanges()
        {
            world = new StateWorld();
            HealthBalance health = world.Balance.Health;
            var light = new HashSet<int>();
            var heavy = new HashSet<int>();
            for (int i = 0; i < 200; i++)
            {
                light.Add(Wound(world, world.Add(), ConditionKind.LightWound).Days);
                heavy.Add(Wound(world, world.Add(), ConditionKind.HeavyWound).Days);
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(health.LightWoundDays.Min, 3), light);
            CollectionAssert.AreEquivalent(Enumerable.Range(health.HeavyWoundDays.Min, 8), heavy);
        }

        [Test]
        public void WithoutInfirmary_WoundsHealOneAndAHalfTimesLonger()
        {
            world = new StateWorld();
            world.Data.Set("health.complicationChance", 0f);
            for (int i = 0; i < 30; i++)
            {
                Adventurer adventurer = world.Add();
                ConditionKind kind = i % 2 == 0 ? ConditionKind.LightWound : ConditionKind.HeavyWound;
                Condition condition = Wound(world, adventurer, kind);
                int expected = (int)Math.Ceiling(condition.Days * 1.5 - 1e-6);
                Assert.AreEqual(expected, MidnightsToHeal(world, adventurer, condition), $"{kind} {condition.Days} days");
                Assert.IsFalse(adventurer.State.InInfirmary);
            }
        }

        [Test]
        public void InInfirmary_WoundsHealInSeventyPercent_AndCostFiveADay()
        {
            world = new StateWorld(infirmary: new FakeInfirmary(beds: 4));
            for (int i = 0; i < 20; i++)
            {
                Adventurer adventurer = world.Add();
                ConditionKind kind = i % 2 == 0 ? ConditionKind.LightWound : ConditionKind.HeavyWound;
                Condition condition = Wound(world, adventurer, kind);
                if (kind == ConditionKind.LightWound) world.Do(ctx => HealthService.Admit(ctx, adventurer, self: true)); // лёгкий ложится сам
                int expected = (int)Math.Ceiling(condition.Days * 0.7 - 1e-6);
                Assert.AreEqual(expected, MidnightsToHeal(world, adventurer, condition), $"{kind} {condition.Days} days");
                Assert.IsFalse(condition.IsComplicated, "в Лазарете осложнений нет");
                Assert.IsFalse(adventurer.State.InInfirmary, "выздоровел — койка свободна");
                world.Do(ctx => AdventurerLifecycle.Retire(ctx, adventurer, LeaveReason.Left));
            }

            Adventurer patient = world.Add();
            Wound(world, patient, ConditionKind.HeavyWound);
            world.TickToHour(0);
            Assert.IsTrue(patient.State.InInfirmary);
            patient.State.Wallet = 100;
            world.TickToHour(world.Balance.Time.MorningHour);
            Assert.AreEqual(Activity.Infirmary, patient.State.Activity);
            world.TickToHour(world.Balance.Time.NightHour - 1);
            Assert.AreEqual(Activity.Infirmary, patient.State.Activity, "в таверну не идёт");
            Assert.AreEqual(95, patient.State.Wallet, "Лазарет — 5 за день");
        }

        [Test]
        public void InfirmaryWithoutMedic_DoesNotHeal()
        {
            world = new StateWorld(infirmary: new FakeInfirmary(beds: 4, hasMedic: false));
            world.Data.Set("health.complicationChance", 0f);
            Adventurer adventurer = world.Add();
            Condition condition = Wound(world, adventurer, ConditionKind.LightWound);
            Assert.AreEqual((int)Math.Ceiling(condition.Days * 1.5 - 1e-6), MidnightsToHeal(world, adventurer, condition));
        }

        [Test]
        public void HealerLevel_SpeedsUpInfirmary()
        {
            world = new StateWorld(infirmary: new FakeInfirmary(beds: 1, speed: 2f));
            Adventurer adventurer = world.Add();
            Condition condition = Wound(world, adventurer, ConditionKind.HeavyWound);
            Assert.AreEqual((int)Math.Ceiling(condition.Days * 0.35 - 1e-6), MidnightsToHeal(world, adventurer, condition));
        }

        [Test]
        public void Beds_HeavyWoundedLaidAutomatically_EarlierFirst_LightOnlyByChoice()
        {
            var infirmary = new FakeInfirmary(beds: 1);
            world = new StateWorld(infirmary: infirmary);
            world.Data.Set("health.complicationChance", 0f);
            Adventurer lightEarly = world.Add();
            Adventurer heavyLate = world.Add();
            Adventurer heavyLater = world.Add();

            Wound(world, lightEarly, ConditionKind.LightWound);
            Wound(world, heavyLate, ConditionKind.HeavyWound);
            world.Simulation.Tick();
            Wound(world, heavyLater, ConditionKind.HeavyWound);
            world.Simulation.Tick();

            Assert.IsTrue(heavyLate.State.InInfirmary, "тяжёлого кладут в тот же час");
            Assert.IsFalse(heavyLater.State.InInfirmary, "коек нет — ждёт");
            Assert.IsFalse(lightEarly.State.InInfirmary, "лёгкого сами не кладут");

            infirmary.BedCount = 2;
            world.Simulation.Tick();
            Assert.IsTrue(heavyLater.State.InInfirmary, "освободилась койка — следующий тяжёлый");

            infirmary.BedCount = 3;
            world.Simulation.Tick();
            Assert.IsFalse(lightEarly.State.InInfirmary, "лёгкий ложится только решением");
        }

        [Test]
        public void Beds_HeavyDoesNotBumpLight_AndNoMedicEmptiesInfirmary()
        {
            var infirmary = new FakeInfirmary(beds: 1);
            world = new StateWorld(infirmary: infirmary);
            Adventurer light = world.Add();
            Adventurer heavy = world.Add();
            Wound(world, light, ConditionKind.LightWound);
            world.Do(ctx => HealthService.Admit(ctx, light, self: true));
            Wound(world, heavy, ConditionKind.HeavyWound);
            world.Simulation.Tick();

            Assert.IsTrue(light.State.InInfirmary, "койка держится до выздоровления");
            Assert.IsFalse(heavy.State.InInfirmary, "тяжёлый не вытесняет лёгкого");

            infirmary.Medic = false;
            world.Simulation.Tick();
            Assert.IsFalse(light.State.InInfirmary || heavy.State.InInfirmary, "без Лекаря Лазарет не лечит — все выходят");
        }

        [Test]
        public void HeavyWoundWithoutInfirmary_ComplicatesTwentyPercent_PlusSevenDaysAndStress()
        {
            world = new StateWorld(17u);
            const int wounds = 1000;
            var conditions = new List<(Adventurer adventurer, Condition condition, int days)>();
            for (int i = 0; i < wounds; i++)
            {
                Adventurer adventurer = world.Add();
                Condition condition = Wound(world, adventurer, ConditionKind.HeavyWound);
                conditions.Add((adventurer, condition, condition.Days));
            }
            foreach ((Adventurer adventurer, _, _) in conditions) adventurer.State.Stress = 50f;

            List<SimEvent> events = world.Collect(() => world.TickToHour(0));
            int complicated = conditions.Count(c => c.condition.IsComplicated);
            world.TickToHour(0);
            Assert.AreEqual(complicated, conditions.Count(c => c.condition.IsComplicated), "бросок один на рану");

            TestContext.WriteLine($"{complicated} of {wounds} complicated");
            Assert.That(complicated, Is.EqualTo(0.2f * wounds).Within(0.2f * wounds * 0.2f));
            Assert.AreEqual(complicated, events.Count(e => e.Type == SimEventType.WoundComplicated));

            // У всех одинаковый день; осложнение — +7 дней и стресс +10.
            float calmStress = conditions.First(c => !c.condition.IsComplicated).adventurer.State.Stress;
            foreach ((Adventurer adventurer, Condition condition, int days) in conditions)
            {
                Assert.AreEqual(condition.IsComplicated ? days + 7 : days, condition.Days);
                Assert.AreEqual(condition.IsComplicated ? calmStress + 10f : calmStress, adventurer.State.Stress, 1e-3f);
            }
        }

        [Test]
        public void OwnWound_AddsStress_LightWound_LowersProfile()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            adventurer.State.Stress = 20f;

            Wound(world, adventurer, ConditionKind.LightWound);
            Assert.AreEqual(30f, adventurer.State.Stress, 1e-4f);
            Assert.AreEqual(25.5f, AdventurerStats.Effective(adventurer, StatId.Agility, world.Registry), 1e-4f, "× 0,85");
            Assert.IsTrue(StateRules.CanTakeQuests(adventurer.State, world.Balance.State), "с лёгкой раной — можно");

            adventurer.State.Fatigue = 80f;
            Assert.AreEqual(30f * 0.85f * 0.8f, AdventurerStats.Effective(adventurer, StatId.Agility, world.Registry), 1e-4f);

            Wound(world, adventurer, ConditionKind.HeavyWound);
            Assert.AreEqual(50f, adventurer.State.Stress, 1e-4f);
            Assert.IsFalse(StateRules.CanTakeQuests(adventurer.State, world.Balance.State), "с тяжёлой — нельзя");
        }

        [Test]
        public void LightWounds_DoNotStack_LongestStays()
        {
            world = new StateWorld(5u);
            Adventurer adventurer = world.Add();
            var days = new List<int>();
            for (int i = 0; i < 10; i++) days.Add(Wound(world, adventurer, ConditionKind.LightWound).Days);

            Assert.AreEqual(1, adventurer.State.Conditions.Count);
            Assert.AreEqual(days.Max(), adventurer.State.Conditions[0].Days);
        }

        [Test]
        public void SecondHeavyWound_BecomesMaimed()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            Condition first = Wound(world, adventurer, ConditionKind.HeavyWound);

            List<SimEvent> events = world.Collect(() =>
            {
                Assert.AreSame(first, Wound(world, adventurer, ConditionKind.HeavyWound));
                world.Simulation.Tick();
            });

            Assert.AreEqual(1, adventurer.State.Conditions.Count);
            Assert.IsTrue(adventurer.TryGetTrait("Maimed", out TraitInstance maimed));
            Assert.IsTrue(maimed.Revealed, "Калека раскрывается сразу");
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.AdventurerWounded && e.TryGet("maimed", out bool m) && m));
            Assert.AreEqual(21f, AdventurerStats.Permanent(adventurer, maimed.AffectedStat.Value, world.Registry), 1e-4f, "−30% навсегда");
        }

        [Test]
        public void HealedWound_Event_BackToSchedule()
        {
            world = new StateWorld();
            world.Data.Set("health.complicationChance", 0f);
            world.Data.Set("decisions.bestChoiceChance", 1f); // вечером — всегда в таверну (лучший вариант)
            Adventurer adventurer = world.Add();
            Condition condition = Wound(world, adventurer, ConditionKind.HeavyWound);

            List<SimEvent> events = world.Collect(() => MidnightsToHeal(world, adventurer, condition));
            SimEvent healed = events.Single(e => e.Type == SimEventType.WoundHealed);
            Assert.AreEqual(adventurer.Id, healed.Participants[0]);
            Assert.AreEqual(EventImportance.Normal, healed.Importance);

            world.TickToHour(world.Balance.Time.EveningHour + 1); // решение в начале вечера — таверна со следующего часа
            Assert.AreEqual(Activity.Tavern, adventurer.State.Activity);
        }
    }
}
