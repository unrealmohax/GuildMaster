using System;
using GuildMaster.Core;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    public sealed class RngTests
    {
        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new Rng(42, 7);
            var b = new Rng(42, 7);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void DifferentSeedOrStream_GivesDifferentSequence()
        {
            Assert.AreNotEqual(Take(new Rng(42, 7)), Take(new Rng(43, 7)));
            Assert.AreNotEqual(Take(new Rng(42, 7)), Take(new Rng(42, 8)));
        }

        [Test]
        public void KnownSeed_GivesKnownValues()
        {
            // Фиксирует алгоритм: если числа поменялись — поменялись все сохранённые зёрна и сценарии.
            var rng = new Rng(42, 54);
            Assert.AreEqual(new uint[] { 0xa15c02b7, 0x7b47f409, 0xba1d3330 }, new[] { rng.NextUInt(), rng.NextUInt(), rng.NextUInt() });
        }

        [Test]
        public void Range_StaysInBounds()
        {
            var rng = new Rng(1);
            for (int i = 0; i < 10000; i++)
            {
                int value = rng.Range(-3, 4);
                Assert.That(value, Is.InRange(-3, 3));
                int inclusive = rng.RangeInclusive(17, 35);
                Assert.That(inclusive, Is.InRange(17, 35));
                float f = rng.Range(0.5f, 2f);
                Assert.That(f, Is.GreaterThanOrEqualTo(0.5f).And.LessThan(2f));
                float unit = rng.NextFloat();
                Assert.That(unit, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Range(5, 5));
        }

        [Test]
        public void Range_CoversAllValues()
        {
            var rng = new Rng(2);
            var seen = new bool[7];
            for (int i = 0; i < 1000; i++) seen[rng.Range(0, 7)] = true;
            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void Chance_RespectsEdgesAndRate()
        {
            var rng = new Rng(3);
            int hits = 0;
            for (int i = 0; i < 10000; i++)
            {
                Assert.IsFalse(rng.Chance(0f));
                Assert.IsTrue(rng.Chance(1f));
                if (rng.Chance(0.25f)) hits++;
            }
            Assert.That(hits / 10000f, Is.EqualTo(0.25f).Within(0.02f));
        }

        [Test]
        public void PickWeighted_NeverPicksZeroWeight_AndFollowsWeights()
        {
            var rng = new Rng(4);
            var items = new[] { "a", "b", "c" };
            var weights = new[] { 3f, 0f, 1f };
            int a = 0;
            for (int i = 0; i < 10000; i++)
            {
                string picked = rng.PickWeighted(items, weights);
                Assert.AreNotEqual("b", picked);
                if (picked == "a") a++;
            }
            Assert.That(a / 10000f, Is.EqualTo(0.75f).Within(0.02f));
            Assert.Throws<ArgumentException>(() => rng.PickWeightedIndex(new[] { 0f, -1f }));
        }

        [Test]
        public void Normal_HasRequestedMeanAndDeviation()
        {
            var rng = new Rng(5);
            const int n = 20000;
            double sum = 0, sumSquares = 0;
            for (int i = 0; i < n; i++)
            {
                double x = rng.Normal(0f, 45f);
                sum += x;
                sumSquares += x * x;
            }
            double mean = sum / n;
            double deviation = Math.Sqrt(sumSquares / n - mean * mean);
            Assert.That(mean, Is.EqualTo(0).Within(1.5));
            Assert.That(deviation, Is.EqualTo(45).Within(1.5));
        }

        [Test]
        public void Streams_AreIndependent()
        {
            var calm = new RngService(99);
            var busy = new RngService(99);
            for (int i = 0; i < 500; i++) busy.Stream("Other").NextUInt();

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(calm.Stream("Quest").NextUInt(), busy.Stream("Quest").NextUInt());
            }
            Assert.AreNotEqual(Take(new RngService(99).Stream("Quest")), Take(new RngService(99).Stream("Order")));
        }

        private static string Take(Rng rng) => $"{rng.NextUInt()} {rng.NextUInt()} {rng.NextUInt()}";
    }
}
