using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Новый заказ. Порядок бросков: ранг по смеси для репутации → «сложный не по времени» (всегда один бросок) → тип по весам
    /// → расстояние → профиль (<see cref="BuildProfile"/>) → награда → срок на доске → заказчик, место, враг, груз → шаблон
    /// описания → число намёков и сами намёки → точность описания. Порядок не менять без причины: сдвинутся все заказы.
    /// </summary>
    internal static class OrderGenerator
    {
        /// <summary>Сгенерировать заказ. <paramref name="fixedRank"/> — ранг без смеси и без «сложного не по времени».</summary>
        /// <param name="errors">Ошибки подстановки в описании (метка без значения и т.п.).</param>
        /// <param name="fixedType">Тип задан заранее — без броска типа; <c>null</c> — по весам.</param>
        public static Order Generate(Rng rng, DataRegistry data, float reputation, int id, long now, GuildRank? fixedRank, List<string> errors,
            QuestTypeDefinition fixedType = null)
        {
            OrdersBalance balance = data.Balance.Orders;

            GuildRank rank;
            bool hardEarly = false;
            if (fixedRank.HasValue)
            {
                rank = fixedRank.Value;
            }
            else
            {
                GuildRank rolled = (GuildRank)rng.PickWeightedIndex(RankMixFor(balance, reputation).Weights);
                rank = rolled;
                if (rng.Chance(balance.HardEarlyChance))
                {
                    rank = (GuildRank)Math.Min((int)GuildRank.C, (int)rolled + balance.HardEarlyRankBonus);
                    hardEarly = rank != rolled;
                }
            }

            QuestTypeDefinition type = fixedType ?? PickType(rng, data);
            OrderDistance distance = rng.Chance(balance.FarChance) ? OrderDistance.Far : OrderDistance.Near;

            var order = new Order(id, type.Id, rank, distance)
            {
                IsHardEarly = hardEarly,
                ArrivedAtHours = now,
                Status = OrderStatus.Incoming,
            };

            float[] profile = BuildProfile(rng, data, type, rank, distance);
            for (int i = 0; i < profile.Length; i++) order.SetRequirement((StatId)i, profile[i]);

            order.PartySizeCeiling = type.MaxPartySizeCeiling;
            foreach (StatCeiling ceiling in type.StatCeilings)
            {
                if (ceiling != null) order.SetStatCeiling(ceiling.Stat, ceiling.For(rank));
            }

            order.Reward = Reward(rng, data, rank, distance);
            order.BoardDays = rng.RangeInclusive(balance.BoardDays.Min, balance.BoardDays.Max);
            order.IsImportant = IsImportant(order, balance);

            Describe(rng, data, type, order, errors);
            order.DescriptionAccuracy = rng.Range(balance.DescriptionAccuracy.Min, balance.DescriptionAccuracy.Max);
            return order;
        }

        /// <summary>Строка смеси рангов для репутации: последняя, у которой порог не выше репутации.</summary>
        public static RankMixEntry RankMixFor(OrdersBalance balance, float reputation)
        {
            IReadOnlyList<RankMixEntry> mix = balance.RankMix;
            RankMixEntry found = mix[0];
            foreach (RankMixEntry entry in mix)
            {
                if (entry.FromReputation <= reputation && entry.FromReputation >= found.FromReputation) found = entry;
            }
            return found;
        }

        /// <summary>Сколько заказов приходит в день: база + репутация / делитель (дробная часть — шанс ещё одного).</summary>
        public static float OrdersPerDay(OrdersBalance balance, float reputation) =>
            balance.BaseOrdersPerDay + reputation / balance.ReputationPerExtraOrder;

        /// <summary>Важный заказ: сложный не по времени или награда не меньше порога.</summary>
        public static bool IsImportant(Order order, OrdersBalance balance) =>
            order.IsHardEarly || order.Reward >= balance.ImportantRewardThreshold;

        /// <summary>
        /// Тип + ранг → профиль требований по всем параметрам (<see cref="StatId"/>). Главные оси типа — из диапазона главных осей
        /// ранга, второстепенные — второстепенных; далеко и Выживания нет среди осей — Выживание второстепенной. Каждая ось
        /// шаблона × случайный множитель; затем все оси диаграммы не меньше порога. У Слаженности — 0 (не ось диаграммы).
        /// </summary>
        public static float[] BuildProfile(Rng rng, DataRegistry data, QuestTypeDefinition type, GuildRank rank, OrderDistance distance)
        {
            OrdersBalance orders = data.Balance.Orders;
            RankEntry ranks = data.Balance.Ranks.For(rank);
            var profile = new float[Vocabulary.StatCount];
            var used = new bool[Vocabulary.StatCount];

            foreach (StatId stat in type.MainAxes)
            {
                if (used[(int)stat]) continue;
                used[(int)stat] = true;
                profile[(int)stat] = RangeOf(rng, ranks.MainAxes);
            }
            foreach (StatId stat in type.SecondaryAxes)
            {
                if (used[(int)stat]) continue;
                used[(int)stat] = true;
                profile[(int)stat] = RangeOf(rng, ranks.SecondaryAxes);
            }
            if (distance == OrderDistance.Far && !used[(int)StatId.Survival])
            {
                used[(int)StatId.Survival] = true;
                profile[(int)StatId.Survival] = RangeOf(rng, ranks.SecondaryAxes);
            }

            for (int i = 0; i < profile.Length; i++)
            {
                if (used[i]) profile[i] *= rng.Range(orders.AxisVariance.Min, orders.AxisVariance.Max);
            }
            for (int i = 0; i < profile.Length; i++)
            {
                profile[i] = Vocabulary.IsDiagramAxis((StatId)i) ? Math.Max(profile[i], orders.RequirementFloor) : 0f;
            }
            return profile;
        }

        /// <summary>Награда ранга × дальность, округлённая до <c>rewardRounding</c>.</summary>
        public static int Reward(Rng rng, DataRegistry data, GuildRank rank, OrderDistance distance)
        {
            OrdersBalance orders = data.Balance.Orders;
            IntRange range = data.Balance.Ranks.For(rank).Reward;
            float reward = rng.RangeInclusive(range.Min, range.Max);
            if (distance == OrderDistance.Far) reward *= orders.FarRewardMultiplier;
            int step = Math.Max(1, orders.RewardRounding);
            return (int)Math.Round(reward / step, MidpointRounding.AwayFromZero) * step;
        }

        /// <summary>Самые большие оси профиля по убыванию; при равенстве — по порядку параметров.</summary>
        public static List<StatId> LargestAxes(Order order, int count)
        {
            var axes = new List<StatId>();
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                if (Vocabulary.IsDiagramAxis((StatId)i)) axes.Add((StatId)i);
            }
            axes.Sort((a, b) =>
            {
                int byValue = order.Requirement(b).CompareTo(order.Requirement(a));
                return byValue != 0 ? byValue : a.CompareTo(b);
            });
            if (axes.Count > count) axes.RemoveRange(count, axes.Count - count);
            return axes;
        }

        private static float RangeOf(Rng rng, IntRange range) => rng.Range((float)range.Min, range.Max);

        private static QuestTypeDefinition PickType(Rng rng, DataRegistry data)
        {
            IReadOnlyList<QuestTypeDefinition> types = data.All<QuestTypeDefinition>();
            var weights = new float[types.Count];
            for (int i = 0; i < types.Count; i++) weights[i] = types[i].GenerationWeight;
            return types[rng.PickWeightedIndex(weights)];
        }

        /// <summary>
        /// Заказчик, место, враг, груз, описание по шаблону типа и намёки на 1–2 самые большие оси; далеко — намёк на путь,
        /// потолок размера группы — свой намёк. Одинаковые предложения не повторяются.
        /// </summary>
        private static void Describe(Rng rng, DataRegistry data, QuestTypeDefinition type, Order order, List<string> errors)
        {
            OrderTextTemplates texts = data.OrderTexts;
            QuestTypeTexts typeTexts = TextsFor(texts, type);

            order.Client = rng.Pick(texts.Clients);
            order.Place = rng.Pick(typeTexts.Places);
            order.Enemy = rng.Pick(typeTexts.Enemies);
            order.Cargo = typeTexts.Cargo.Count > 0 ? rng.Pick(typeTexts.Cargo) : null;
            string template = rng.Pick(typeTexts.Descriptions);

            var sentences = new List<string> { TextRenderer.Render(template, new OrderTextSource(order), errors) };

            IntRange hintCount = data.Balance.Orders.HintCount;
            int hints = rng.RangeInclusive(hintCount.Min, hintCount.Max);
            foreach (StatId stat in LargestAxes(order, hints))
            {
                IReadOnlyList<string> lines = HintLines(texts, stat);
                if (lines == null || lines.Count == 0) continue;
                order.AddHint(stat);
                AddSentence(sentences, rng.Pick(lines));
            }
            if (order.Distance == OrderDistance.Far) AddSentence(sentences, texts.FarHint);
            if (order.PartySizeCeiling > 0) AddSentence(sentences, texts.PartySizeCeilingHint);

            order.Description = string.Join(". ", sentences) + ".";
        }

        private static void AddSentence(List<string> sentences, string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence) || sentences.Contains(sentence)) return;
            sentences.Add(sentence);
        }

        private static QuestTypeTexts TextsFor(OrderTextTemplates texts, QuestTypeDefinition type)
        {
            foreach (QuestTypeTexts typeTexts in texts.QuestTypes)
            {
                if (typeTexts != null && typeTexts.QuestType == type) return typeTexts;
            }
            throw new InvalidOperationException($"No order texts for quest type '{type.Id}'");
        }

        private static IReadOnlyList<string> HintLines(OrderTextTemplates texts, StatId stat)
        {
            foreach (AxisHint hint in texts.Hints)
            {
                if (hint != null && hint.Stat == stat) return hint.Lines;
            }
            return null;
        }
    }
}
