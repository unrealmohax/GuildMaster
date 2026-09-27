using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Синергия одной пары в раунде: кто, какая, добавка к шансу.</summary>
    public readonly struct PairSynergy
    {
        public PairSynergy(int a, int b, string kind, float value)
        {
            A = a;
            B = b;
            Kind = kind;
            Value = value;
        }

        public int A { get; }
        public int B { get; }

        /// <summary>friends / longPartners / lovers / rivals / dislike.</summary>
        public string Kind { get; }

        public float Value { get; }
    }

    /// <summary>
    /// Расчёт задания без побочных эффектов: площадь перекрытия фигуры группы и фигуры требований на диаграмме,
    /// профиль группы, синергии, потолки, воспринимаемый шанс. Мир не меняет, случайных чисел не тратит.
    /// <para>
    /// Диаграмма — 13 осей в порядке <see cref="StatCatalog.RadarOrder"/> (Слаженность — не ось), угол между соседними
    /// осями θ = 2π / 13. Перекрытие — доля площади фигуры требований, которую накрывает фигура группы.
    /// </para>
    /// </summary>
    public static class QuestMath
    {
        /// <summary>
        /// Перекрытие: Σ площадей пересечения по секторам / Σ площадей требований. Сектор — между соседними осями (последняя
        /// замыкается на первую). В секторе отрезки требований и группы не пересекаются, если разности по двум осям одного знака
        /// (или одна из них 0), — пересечение — треугольник на меньших значениях; иначе — четырёхугольник O, меньшее на первой
        /// оси, точка пересечения отрезков, меньшее на второй. Требований нет (площадь 0) — перекрытие 1.
        /// </summary>
        /// <param name="radar">Порядок осей по кругу.</param>
        /// <param name="requirements">Требования по <see cref="StatId"/>.</param>
        /// <param name="group">Профиль группы по <see cref="StatId"/>.</param>
        public static float Overlap(IReadOnlyList<StatId> radar, IReadOnlyList<float> requirements, IReadOnlyList<float> group)
        {
            int n = radar.Count;
            if (n < 3) return 1f;

            double theta = 2.0 * Math.PI / n;
            double sin = Math.Sin(theta);
            double cos = Math.Cos(theta);
            double required = 0.0;
            double covered = 0.0;

            for (int i = 0; i < n; i++)
            {
                StatId first = radar[i];
                StatId second = radar[(i + 1) % n];
                double q0 = Math.Max(0f, requirements[(int)first]);
                double q1 = Math.Max(0f, requirements[(int)second]);
                double g0 = Math.Max(0f, group[(int)first]);
                double g1 = Math.Max(0f, group[(int)second]);

                required += 0.5 * q0 * q1 * sin;
                covered += SectorIntersection(q0, q1, g0, g1, sin, cos);
            }

            if (required <= 0.0) return 1f;
            return (float)Math.Max(0.0, Math.Min(1.0, covered / required));
        }

        /// <summary>Площадь пересечения двух треугольников сектора (с вершиной в центре).</summary>
        private static double SectorIntersection(double q0, double q1, double g0, double g1, double sin, double cos)
        {
            double d0 = g0 - q0;
            double d1 = g1 - q1;
            double m0 = Math.Min(q0, g0);
            double m1 = Math.Min(q1, g1);
            if (d0 * d1 >= 0.0) return 0.5 * m0 * m1 * sin;

            // Точка пересечения прямых q0·u0 → q1·u1 и g0·u0 → g1·u1, u0 = (1, 0), u1 = (cos, sin).
            // Точка на первой прямой: (q0 + t·(q1·cos − q0), t·q1·sin); на второй: (g0 + s·(g1·cos − g0), s·g1·sin).
            double ax = q0, ay = 0.0, bx = q1 * cos - q0, by = q1 * sin;
            double cx = g0, cy = 0.0, dx = g1 * cos - g0, dy = g1 * sin;
            double denominator = bx * dy - by * dx;
            if (Math.Abs(denominator) < 1e-12) return 0.5 * m0 * m1 * sin;
            double t = ((cx - ax) * dy - (cy - ay) * dx) / denominator;
            double px = ax + t * bx;
            double py = ay + t * by;

            // Четырёхугольник O, m0·u0, X, m1·u1 — формула шнурков (вершины по кругу, без массивов: расчёт идёт очень часто).
            double x0 = 0.0, y0 = 0.0, x1 = m0, y1 = 0.0, x2 = px, y2 = py, x3 = m1 * cos, y3 = m1 * sin;
            double area = 0.0;
            area += x0 * y1 - x1 * y0;
            area += x1 * y2 - x2 * y1;
            area += x2 * y3 - x3 * y2;
            area += x3 * y0 - x0 * y3;
            return Math.Abs(area) * 0.5;
        }

        /// <summary>
        /// Профиль группы по <see cref="StatId"/>: Σ по присутствующим эффективный параметр × множитель (один — × 1,
        /// в группе — × Слаженность / 100) × паника × бросок вперёд. У Слаженности — 0 (не ось).
        /// </summary>
        /// <param name="panicked">Кто в панике в этом раунде (профиль × <c>panicProfileMultiplier</c>); может быть <c>null</c>.</param>
        /// <param name="rushing">Кто бросился вперёд (× <c>rushProfileMultiplier</c>); может быть <c>null</c>.</param>
        public static float[] GroupProfile(IReadOnlyList<Adventurer> members, DataRegistry data, IReadOnlyList<int> panicked = null,
            IReadOnlyList<int> rushing = null)
        {
            var profile = new float[Vocabulary.StatCount];
            bool solo = members.Count == 1;
            TensionBalance tension = data.Balance.Tension;
            foreach (Adventurer member in members)
            {
                float multiplier = solo ? data.Balance.Rounds.SoloProfileMultiplier : member.GetStat(StatId.Cohesion) / 100f;
                if (panicked != null && Contains(panicked, member.Id)) multiplier *= tension.PanicProfileMultiplier;
                if (rushing != null && Contains(rushing, member.Id)) multiplier *= tension.RushProfileMultiplier;
                for (int i = 0; i < profile.Length; i++)
                {
                    var stat = (StatId)i;
                    if (!Vocabulary.IsDiagramAxis(stat)) continue;
                    profile[i] += AdventurerStats.Effective(member, stat, data) * multiplier;
                }
            }
            return profile;
        }

        /// <summary>
        /// Синергии каждой пары: друзья, давние напарники, влюблённые (партнёр по черте), соперники (партнёр по черте),
        /// неприязнь. Сумма — не больше <c>maxTotalSynergy</c> по модулю.
        /// </summary>
        public static float Synergy(IReadOnlyList<Adventurer> members, RelationBook relations, DataRegistry data, List<PairSynergy> pairs = null)
        {
            RoundsBalance rounds = data.Balance.Rounds;
            float total = 0f;
            for (int i = 0; i < members.Count; i++)
            {
                for (int j = i + 1; j < members.Count; j++)
                {
                    Adventurer a = members[i];
                    Adventurer b = members[j];
                    RelationLabels labels = RelationService.LabelsOf(relations, a.Id, b.Id, data.Balance.Adventurers);
                    if ((labels & RelationLabels.Friends) != 0) Add(pairs, ref total, a, b, "friends", rounds.FriendsSynergy);
                    if ((labels & RelationLabels.LongPartners) != 0) Add(pairs, ref total, a, b, "longPartners", rounds.LongPartnersSynergy);
                    if ((labels & RelationLabels.Dislike) != 0) Add(pairs, ref total, a, b, "dislike", rounds.DislikeSynergy);
                    if (ArePartners(a, b, TraitHook.LoverPartner, data)) Add(pairs, ref total, a, b, "lovers", rounds.LoversSynergy);
                    if (ArePartners(a, b, TraitHook.RivalPartner, data)) Add(pairs, ref total, a, b, "rivals", rounds.RivalsSynergy);
                }
            }
            float limit = rounds.MaxTotalSynergy;
            return Math.Max(-limit, Math.Min(limit, total));
        }

        /// <summary>У двоих черта с партнёром этого вида, связанная друг с другом (у любого из двух).</summary>
        public static bool ArePartners(Adventurer a, Adventurer b, TraitHook hook, DataRegistry data)
        {
            TraitInstance ofA = TraitRules.FindWithHook(a, hook, data);
            TraitInstance ofB = TraitRules.FindWithHook(b, hook, data);
            return (ofA != null && ofA.PartnerId == b.Id) || (ofB != null && ofB.PartnerId == a.Id);
        }

        /// <summary>
        /// Потолок сработал: людей больше потолка размера группы или значение группы по оси выше потолка оси (0 — нет потолка).
        /// </summary>
        public static bool CeilingHit(Order order, int partySize, IReadOnlyList<float> group)
        {
            if (order.PartySizeCeiling > 0 && partySize > order.PartySizeCeiling) return true;
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                int ceiling = order.StatCeiling((StatId)i);
                if (ceiling > 0 && group[i] > ceiling) return true;
            }
            return false;
        }

        /// <summary>
        /// Ошибка оценки риска человека: множитель воспринимаемых требований. Эффекты <see cref="EffectKind.RiskPerception"/>:
        /// у осей — доля × сила оси (трус −50 при +0,3 → × 1,15), у особых черт — целиком (Хвастун −0,4 → × 0,6).
        /// Несколько — перемножаются.
        /// </summary>
        public static float RiskPerception(Adventurer adventurer, DataRegistry data)
        {
            float multiplier = 1f;
            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                float value = adventurer.GetAxis((AxisId)i);
                AxisPole pole = AxisMath.PoleOf(value);
                foreach (TraitEffect effect in data.Axis((AxisId)i).Pole(pole).Effects)
                {
                    if (effect.Kind != EffectKind.RiskPerception) continue;
                    multiplier *= 1f + effect.Value * AxisMath.Strength(value, pole);
                }
            }
            foreach (TraitInstance trait in adventurer.Traits)
            {
                foreach (TraitEffect effect in data.Get<SpecialTraitDefinition>(trait.TraitId).Effects)
                {
                    if (effect.Kind == EffectKind.RiskPerception) multiplier *= 1f + effect.Value;
                }
            }
            return Math.Max(0f, multiplier);
        }

        /// <summary>
        /// Требования, какими их видит человек: настоящие × неточность описания заказа × его ошибка оценки риска.
        /// </summary>
        public static float[] PerceivedRequirements(IReadOnlyList<float> requirements, float descriptionAccuracy, Adventurer viewer, DataRegistry data)
        {
            float multiplier = descriptionAccuracy * RiskPerception(viewer, data);
            var perceived = new float[requirements.Count];
            for (int i = 0; i < perceived.Length; i++) perceived[i] = requirements[i] * multiplier;
            return perceived;
        }

        /// <summary>Воспринимаемое перекрытие человеком, если он пойдёт один: свой профиль против требований, какими он их видит.</summary>
        public static float PerceivedSoloOverlap(Adventurer adventurer, Order order, DataRegistry data)
        {
            float[] perceived = PerceivedRequirements(order.Profile, order.DescriptionAccuracy, adventurer, data);
            return Overlap(data.Stats.RadarOrder, perceived, GroupProfile(new[] { adventurer }, data));
        }

        /// <summary>Настоящее перекрытие группы (без синергий): профиль группы против настоящих требований.</summary>
        public static float RealOverlap(IReadOnlyList<Adventurer> party, Order order, DataRegistry data) =>
            Overlap(data.Stats.RadarOrder, order.Profile, GroupProfile(party, data));

        /// <summary>Настоящее перекрытие человека одного: свой профиль против настоящих требований.</summary>
        public static float RealSoloOverlap(Adventurer adventurer, Order order, DataRegistry data) =>
            Overlap(data.Stats.RadarOrder, order.Profile, GroupProfile(new[] { adventurer }, data));

        /// <summary>
        /// Профиль события в пути или находки: оси определения — значение из диапазона главных или второстепенных осей ранга,
        /// × случайный множитель оси; остальные оси диаграммы — порог требований. Порядок бросков — по осям определения.
        /// </summary>
        public static float[] EncounterProfile(Rng rng, DataRegistry data, EncounterDefinition encounter, GuildRank rank)
        {
            OrdersBalance orders = data.Balance.Orders;
            RankEntry entry = data.Balance.Ranks.For(rank);
            IntRange range = encounter.ValueSource == RankValueSource.MainAxes ? entry.MainAxes : entry.SecondaryAxes;
            var profile = new float[Vocabulary.StatCount];
            foreach (StatId stat in encounter.Axes)
            {
                if (profile[(int)stat] > 0f) continue;
                profile[(int)stat] = rng.Range((float)range.Min, range.Max) * rng.Range(orders.AxisVariance.Min, orders.AxisVariance.Max);
            }
            for (int i = 0; i < profile.Length; i++)
            {
                profile[i] = Vocabulary.IsDiagramAxis((StatId)i) ? Math.Max(profile[i], orders.RequirementFloor) : 0f;
            }
            return profile;
        }

        /// <summary>Ранг, сдвинутый на <paramref name="offset"/>, в пределах G–C.</summary>
        public static GuildRank ShiftRank(GuildRank rank, int offset) =>
            (GuildRank)Math.Max((int)GuildRank.G, Math.Min((int)GuildRank.C, (int)rank + offset));

        /// <summary>Номер ранга для формул: G = 1 … C = 5.</summary>
        public static int RankNumber(GuildRank rank) => (int)rank + 1;

        private static void Add(List<PairSynergy> pairs, ref float total, Adventurer a, Adventurer b, string kind, float value)
        {
            total += value;
            pairs?.Add(new PairSynergy(a.Id, b.Id, kind, value));
        }

        private static bool Contains(IReadOnlyList<int> ids, int id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return true;
            }
            return false;
        }
    }
}
