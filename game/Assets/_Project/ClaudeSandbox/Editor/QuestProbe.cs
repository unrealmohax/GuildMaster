using System.Collections.Generic;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Кто какие заказы берёт: год симуляции без игрока на 10 зёрнах; на каждое взятие — полюса осей Риск и Деньги
    /// взявшего, реальное перекрытие в одиночку (насколько заказ ему по силам) и награда относительно средней по рангу.
    /// Итог — в консоль: трусы должны брать заказы полегче смелых, жадные — дороже бескорыстных.
    /// </summary>
    public static class QuestProbe
    {
        private sealed class Bucket
        {
            public int Count;
            public double Overlap;
            public double RewardShare;

            public string Line(string name) => Count == 0
                ? $"{name}: —"
                : $"{name}: {Count} взятий, перекрытие {Overlap / Count:0.00}, награда/средняя {RewardShare / Count:0.00}";
        }

        [MenuItem("GuildMaster/Sandbox/Quests/Order Preferences 10 Years")]
        private static void Preferences()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null)
            {
                Debug.Log("[QuestProbe] no GameConfig");
                return;
            }

            DataRegistry data = DataRegistry.FromConfig(config);
            var buckets = new Dictionary<string, Bucket>();
            int taken = 0, days = 0;
            for (uint seed = 1; seed <= 10; seed++)
            {
                Simulation simulation = Simulation.CreateDefault(data, seed);
                simulation.TickCompleted += events => Count(simulation, data, events, buckets, ref taken);
                long hours = simulation.Calendar.DaysToHours(360);
                for (long i = 0; i < hours; i++) simulation.Tick();
                days += 360;
            }

            var text = new StringBuilder($"[QuestProbe] {taken} взятий за {days} дней ({(float)taken / days:0.00} в день)\n");
            foreach (string name in new[] { "трус", "смелый", "риск нейтр.", "жадный", "бескорыстный", "деньги нейтр." })
                text.AppendLine(buckets.TryGetValue(name, out Bucket bucket) ? bucket.Line(name) : name + ": —");
            Debug.Log(text.ToString());
        }

        private static void Count(Simulation simulation, DataRegistry data, IReadOnlyList<SimEvent> events, Dictionary<string, Bucket> buckets,
            ref int taken)
        {
            WorldState world = simulation.World;
            foreach (SimEvent simEvent in events)
            {
                if (simEvent.Type != SimEventType.OrderTaken || simEvent.Participants.Count == 0) continue;
                if (!simEvent.TryGet("order", out int orderId) || !world.Orders.TryGetOrder(orderId, out Order order) || order.IsPromotion) continue;
                if (!world.Adventurers.TryGetActive(simEvent.Participants[0], out Adventurer adventurer)) continue;

                taken++;
                IntRange range = data.Balance.Ranks.For(order.Rank).Reward;
                float share = order.Reward / ((range.Min + range.Max) * 0.5f);
                float overlap = QuestMath.RealSoloOverlap(adventurer, order, data);
                Add(buckets, Pole(adventurer.GetAxis(AxisId.Risk), "трус", "смелый", "риск нейтр."), overlap, share);
                Add(buckets, Pole(adventurer.GetAxis(AxisId.Money), "бескорыстный", "жадный", "деньги нейтр."), overlap, share);
            }
        }

        private static string Pole(float value, string negative, string positive, string neutral) =>
            value <= -30f ? negative : value >= 30f ? positive : neutral;

        private static void Add(Dictionary<string, Bucket> buckets, string name, float overlap, float share)
        {
            if (!buckets.TryGetValue(name, out Bucket bucket)) buckets[name] = bucket = new Bucket();
            bucket.Count++;
            bucket.Overlap += overlap;
            bucket.RewardShare += share;
        }
    }
}
