using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Особые черты и раскрытие осей.</summary>
    public sealed class TraitTests
    {
        private PeopleData data;
        private Simulation simulation;

        [SetUp]
        public void SetUp()
        {
            data = new PeopleData();
            simulation = Simulation.CreateDefault(data.Registry, 11u);
        }

        [TearDown]
        public void TearDown() => data.Dispose();

        /// <summary>Новый человек в гильдии без черт, все параметры 30.</summary>
        private Adventurer Blank()
        {
            var adventurer = new Adventurer(simulation.World.Ids.Next());
            for (int i = 0; i < Vocabulary.StatCount; i++) adventurer.SetStat((StatId)i, 30f);
            ArchetypeService.Initialize(adventurer, data.Registry);
            simulation.World.Adventurers.AddActive(adventurer);
            return adventurer;
        }

        private bool Acquire(Adventurer adventurer, string traitId, int partnerId = 0, StatId? stat = null)
        {
            bool result = false;
            SimulationRun.Do(simulation, ctx => result = TraitService.TryAcquire(ctx, adventurer, traitId, partnerId, stat));
            return result;
        }

        private SpecialTraitDefinition Trait(string id) => data.Registry.Get<SpecialTraitDefinition>(id);

        [Test]
        public void OneTraitPerCategory_Strictly()
        {
            Adventurer adventurer = Blank();
            Assert.IsTrue(Acquire(adventurer, "Veteran"));

            Assert.IsFalse(TraitRules.CanAdd(adventurer, Trait("Deserter"), data.Registry), "Ветеран + Дезертир: обе «Из прошлого»");
            Assert.IsFalse(Acquire(adventurer, "Deserter"));
            Assert.IsTrue(TraitRules.CanAdd(adventurer, Trait("Drunkard"), data.Registry));

            Assert.IsTrue(Acquire(adventurer, "Family"));
            Assert.IsFalse(TraitRules.CanAdd(adventurer, Trait("Rival"), data.Registry), "Семейный и Соперник — обе «Отношения»");
            Assert.IsFalse(Acquire(adventurer, "Family"), "черта уже есть");
        }

        [Test]
        public void IncompatiblePair_IronNervesAndNightmares_BothWays()
        {
            Adventurer brave = Blank();
            Assert.IsTrue(Acquire(brave, "IronNerves"));
            Assert.IsFalse(Acquire(brave, "Nightmares"));

            Adventurer haunted = Blank();
            Assert.IsTrue(Acquire(haunted, "Nightmares"));
            Assert.IsFalse(TraitRules.CanAdd(haunted, Trait("IronNerves"), data.Registry));
        }

        [Test]
        public void NoMoreThanFourTraits()
        {
            Adventurer adventurer = Blank();
            foreach (string id in new[] { "Veteran", "Drunkard", "IronNerves", "Family" }) Assert.IsTrue(Acquire(adventurer, id));

            Assert.IsFalse(Acquire(adventurer, "Grieving", partnerId: 1));
            Assert.IsFalse(Acquire(adventurer, "Maimed"));
            Assert.AreEqual(4, adventurer.Traits.Count);
        }

        [Test]
        public void NewAcquiredTrait_ReplacesOldAcquired_ExceptMaimed()
        {
            Adventurer adventurer = Blank();
            Assert.IsTrue(Acquire(adventurer, "Nightmares"));

            Assert.IsTrue(Acquire(adventurer, "Grieving", partnerId: 1));
            CollectionAssert.AreEquivalent(new[] { "Grieving" }, adventurer.Traits.Select(t => t.TraitId));
            SimEvent acquired = simulation.Events.Events.Last(e => e.Type == SimEventType.TraitAcquired);
            Assert.IsTrue(acquired.TryGet("replaced", out string replaced) && replaced == "Nightmares");

            Assert.IsTrue(Acquire(adventurer, "Maimed"), "Калека не вытесняет и правило категорий на него не действует");
            CollectionAssert.AreEquivalent(new[] { "Grieving", "Maimed" }, adventurer.Traits.Select(t => t.TraitId));

            Assert.IsTrue(Acquire(adventurer, "Tested"));
            CollectionAssert.AreEquivalent(new[] { "Maimed", "Tested" }, adventurer.Traits.Select(t => t.TraitId), "Калеку не вытесняют");

            Assert.IsFalse(Acquire(adventurer, "Maimed"), "второй раз — нельзя");
        }

        [Test]
        public void Maimed_TakesStatFromEffectList()
        {
            Adventurer adventurer = Blank();
            Assert.IsTrue(Acquire(adventurer, "Maimed"));

            StatId? stat = adventurer.Traits.Single().AffectedStat;
            Assert.IsTrue(stat.HasValue);
            CollectionAssert.Contains(new[] { StatId.Strength, StatId.Endurance, StatId.Agility, StatId.Reaction }, stat.Value);
            Assert.That(AdventurerStats.Effective(adventurer, stat.Value, data.Registry), Is.EqualTo(21f).Within(0.001f), "30 × 0,7");
        }

        [Test]
        public void PartnerTrait_RequiresPartner()
        {
            Adventurer adventurer = Blank();
            Assert.Throws<System.ArgumentException>(() => SimulationRun.Do(simulation, ctx => TraitService.TryAcquire(ctx, adventurer, "Rival")));
        }

        [Test]
        public void TraitsAreHidden_UntilTheirTrigger_RevealIsImportantWithAutopause()
        {
            Adventurer adventurer = Blank();
            Assert.IsTrue(Acquire(adventurer, "Veteran"));
            TraitInstance veteran = adventurer.Traits.Single();
            Assert.IsFalse(veteran.Revealed);

            bool wrong = true, right = false, again = true;
            SimulationRun.Do(simulation, ctx =>
            {
                wrong = RevealService.TryRevealTrait(ctx, adventurer, "Veteran", RevealTrigger.DeserterFled);
                right = RevealService.TryRevealTrait(ctx, adventurer, "Veteran", RevealTrigger.VeteranFirstTension);
                again = RevealService.TryRevealTrait(ctx, adventurer, "Veteran", RevealTrigger.VeteranFirstTension);
            });

            Assert.IsFalse(wrong, "чужой триггер не раскрывает");
            Assert.IsTrue(right);
            Assert.IsFalse(again, "раскрывается один раз");
            Assert.IsTrue(veteran.Revealed);

            List<SimEvent> events = SimulationRun.Collect(simulation, sim => sim.Tick());
            SimEvent revealed = events.Single(e => e.Type == SimEventType.TraitRevealed);
            Assert.AreEqual(EventImportance.Important, revealed.Importance);
            Assert.IsTrue(revealed.TryGet("feedKey", out string key) && key == "reveal.trait.veteran");
            Assert.IsTrue(simulation.PauseRequested, "раскрытие ставит автопаузу");
            Assert.AreEqual(AutopauseKind.TraitRevealed, simulation.World.Autopause.Triggers.Single().Kind);
        }

        [Test]
        public void OnAcquireTraits_RevealImmediately_TestedGivesCohesion()
        {
            Adventurer adventurer = Blank();
            float cohesion = adventurer.GetStat(StatId.Cohesion);

            Assert.IsTrue(Acquire(adventurer, "Tested"));

            Assert.IsTrue(adventurer.Traits.Single().Revealed);
            Assert.AreEqual(cohesion + data.Balance.Traits.TestedCohesionBonus, adventurer.GetStat(StatId.Cohesion));
            simulation.Tick();
            Assert.IsTrue(simulation.PauseRequested);
        }

        [Test]
        public void AxisReveal_OnlyPoleTrigger_NotNeutral()
        {
            Adventurer adventurer = Blank();
            adventurer.SetAxis(AxisId.Risk, -50f);
            adventurer.SetAxis(AxisId.Money, 20f);

            bool wrongPole = true, neutral = true, coward = false;
            SimulationRun.Do(simulation, ctx =>
            {
                wrongPole = RevealService.TryRevealAxis(ctx, adventurer, AxisId.Risk, RevealTrigger.RecklessRush);
                neutral = RevealService.TryRevealAxis(ctx, adventurer, AxisId.Money, RevealTrigger.GreedyRefusedPay);
                coward = RevealService.TryRevealAxis(ctx, adventurer, AxisId.Risk, RevealTrigger.CowardPanicOrFlee);
            });

            Assert.IsFalse(wrongPole);
            Assert.IsFalse(neutral, "|значение| < 30 — раскрывать нечего");
            Assert.IsTrue(coward);
            Assert.IsTrue(adventurer.IsAxisRevealed(AxisId.Risk));
            Assert.IsFalse(adventurer.IsAxisRevealed(AxisId.Money));

            List<SimEvent> events = SimulationRun.Collect(simulation, sim => sim.Tick());
            SimEvent revealed = events.Single(e => e.Type == SimEventType.AxisRevealed);
            Assert.AreEqual(EventImportance.Important, revealed.Importance);
            Assert.IsTrue(revealed.TryGet("pole", out AxisPole pole) && pole == AxisPole.Negative);
            Assert.IsTrue(revealed.TryGet("extreme", out bool extreme) && !extreme, "−50 — «склонен», не крайний");
            Assert.IsTrue(simulation.PauseRequested);
        }

        [Test]
        public void NeutralAxes_RevealThemselvesAfterSixtyDays_WithoutAutopause()
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            IReadOnlyList<Adventurer> people = simulation.World.Adventurers.Active.ToList();
            int neutralAxes = people.Sum(p => p.Axes.Count(v => AxisMath.IsNeutral(v, balance)));
            Assert.That(neutralAxes, Is.GreaterThan(0));

            long revealAt = simulation.Calendar.StartTotalHours + simulation.Calendar.DaysToHours(balance.NeutralRevealDays);
            var events = new List<SimEvent>();
            bool paused = false;
            simulation.TickCompleted += tickEvents => events.AddRange(tickEvents);
            while (simulation.World.Time.TotalHours < revealAt + simulation.Calendar.HoursPerDay)
            {
                simulation.Tick();
                paused |= simulation.ConsumePauseRequest();
                if (simulation.World.Time.TotalHours < revealAt) Assert.IsFalse(people.Any(p => p.RevealedAxes.Any(r => r)), "раньше 60 дней");
            }

            foreach (Adventurer adventurer in people)
            {
                for (int axis = 0; axis < Vocabulary.AxisCount; axis++)
                {
                    bool isNeutral = AxisMath.IsNeutral(adventurer.GetAxis((AxisId)axis), balance);
                    Assert.AreEqual(isNeutral, adventurer.IsAxisRevealed((AxisId)axis), $"{adventurer.Name} {(AxisId)axis}");
                }
            }
            List<SimEvent> balanced = events.Where(e => e.Type == SimEventType.AxisBalanced).ToList();
            Assert.AreEqual(neutralAxes, balanced.Count);
            Assert.IsTrue(balanced.All(e => e.Importance == EventImportance.Notable));
            Assert.IsFalse(AutopauseRules.Default.ContainsKey(SimEventType.AxisBalanced));
            Assert.IsFalse(paused, "«уравновешен» — без автопаузы");
        }
    }
}
