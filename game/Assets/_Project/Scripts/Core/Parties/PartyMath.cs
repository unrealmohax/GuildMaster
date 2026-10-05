using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Как человек оценивает заказ с группой. Всё — глазами того, кто смотрит: требования искажены неточностью описания
    /// и его ошибкой оценки риска, профиль группы — сумма эффективных параметров × Слаженность / 100 (один — × 1), к перекрытию —
    /// синергии пар (друзья, соперники…) в пределах потолка. Коэффициенты — <see cref="DecisionsBalance"/>. Случайных чисел не тратит.
    /// Расчёт — через <see cref="PartyLens"/> (взгляд одного человека на один заказ); функции здесь — обёртки для разовых вопросов.
    /// </summary>
    public static class PartyMath
    {
        /// <summary>Шанс раунда, каким его видит <paramref name="viewer"/>, если пойдёт эта группа (0..1).</summary>
        public static float PerceivedChance(Adventurer viewer, Order order, IReadOnlyList<Adventurer> members, RelationBook relations, DataRegistry data) =>
            new PartyLens(viewer, order, false, 0f, relations, data).Chance(members);

        /// <summary>Воспринимаемое перекрытие профиля группы, без синергий.</summary>
        public static float PerceivedOverlap(Adventurer viewer, Order order, IReadOnlyList<Adventurer> members, DataRegistry data) =>
            new PartyLens(viewer, order, false, 0f, null, data).Overlap(members);

        /// <summary>
        /// Доля <paramref name="viewer"/> в награде: поровну в постоянной группе, иначе по <see cref="Adventurer.PowerScore"/>
        /// (у всех ноль — поровну).
        /// </summary>
        public static float ExpectedShare(Order order, float commission, IReadOnlyList<Adventurer> members, Adventurer viewer, bool equal)
        {
            float pool = QuestChoices.ExpectedShare(order, commission, 1);
            if (members.Count <= 1) return pool;
            if (equal) return pool / members.Count;

            float total = 0f;
            foreach (Adventurer member in members) total += Math.Max(0f, member.PowerScore);
            return total > 0f ? pool * Math.Max(0f, viewer.PowerScore) / total : pool / members.Count;
        }

        /// <summary>
        /// Товарищи: один — 0; в группе <c>companionsInGroup</c>, + <c>companionsFriendBonus</c>, если в группе друг,
        /// + <c>companionsPermanentBonus</c>, если группа постоянная. Одиночке (отрицательный полюс оси Люди) —
        /// −<c>lonerGroupCompanions</c> × сила полюса.
        /// </summary>
        public static float Companions(Adventurer viewer, IReadOnlyList<Adventurer> members, bool permanent, RelationBook relations, DataRegistry data)
        {
            if (members.Count <= 1) return 0f;
            DecisionsBalance decisions = data.Balance.Decisions;
            float people = viewer.GetAxis(AxisId.People);
            if (people < 0f) return -decisions.LonerGroupCompanions * AxisMath.Strength(people, AxisPole.Negative);

            float score = decisions.CompanionsInGroup;
            foreach (Adventurer member in members)
            {
                if (member.Id != viewer.Id && RelationService.AreFriends(relations, viewer.Id, member.Id, data.Balance.Adventurers))
                {
                    score += decisions.CompanionsFriendBonus;
                    break;
                }
            }
            if (permanent) score += decisions.CompanionsPermanentBonus;
            return score;
        }

        /// <summary>
        /// Оценки варианта «этот заказ с этой группой» по мотивам (индекс — <see cref="Motive"/>): как у заказа в одиночку,
        /// но шанс — шанс группы, доля — своя часть, Товарищи — <see cref="Companions"/>. Группа из одного — те же оценки,
        /// что у взятия заказа в одиночку.
        /// </summary>
        public static void OrderScores(Adventurer viewer, Order order, IReadOnlyList<Adventurer> members, bool permanent, float commission,
            RelationBook relations, DataRegistry data, float[] scores) =>
            new PartyLens(viewer, order, permanent, commission, relations, data).Scores(members, scores);

        /// <summary>Ценность оценок для человека: Σ вес мотива × оценка (оценки ограничены −1..1).</summary>
        public static float Value(Adventurer viewer, float[] scores, DataRegistry data) => Value(Motives.Weigh(viewer, data, false), scores);

        internal static float Value(MotiveWeights weights, float[] scores)
        {
            float value = 0f;
            for (int m = 0; m < scores.Length; m++)
                value += weights[(Motive)m] * Math.Max(-DecisionActions.ScoreLimit, Math.Min(DecisionActions.ScoreLimit, scores[m]));
            return value;
        }

        /// <summary>Ценность варианта «этот заказ с этой группой» для <paramref name="viewer"/>.</summary>
        public static float GroupValue(Adventurer viewer, Order order, IReadOnlyList<Adventurer> members, bool permanent, float commission,
            RelationBook relations, DataRegistry data) =>
            new PartyLens(viewer, order, permanent, commission, relations, data).Value(members);

        /// <summary>Средняя по группе ценность варианта «всей группой» (постоянная группа решает так).</summary>
        public static float MeanGroupValue(Order order, IReadOnlyList<Adventurer> members, bool permanent, float commission,
            RelationBook relations, DataRegistry data, ProfileCache profiles = null, float safetyMultiplier = 1f)
        {
            if (members.Count == 0) return 0f;
            float sum = 0f;
            foreach (Adventurer member in members)
                sum += new PartyLens(member, order, permanent, commission, relations, data, profiles, safetyMultiplier).Value(members);
            return sum / members.Count;
        }

        /// <summary>
        /// Оценка напарника инициатором: прирост воспринимаемого перекрытия × <c>partnerOverlapGainWeight</c> + отношения /
        /// <c>partnerRelationDivisor</c> + <c>partnerFriendBonus</c>, если друзья, − |разница рангов| × <c>partnerRankDifferencePenalty</c>
        /// + прибавка, если гильдия разрешила этим двоим ходить вместе.
        /// </summary>
        public static float PartnerScore(Adventurer initiator, Adventurer candidate, float overlapGain, RelationBook relations, DataRegistry data)
        {
            DecisionsBalance decisions = data.Balance.Decisions;
            float relation = relations.GetValue(initiator.Id, candidate.Id);
            bool friends = RelationService.AreFriends(relations, initiator.Id, candidate.Id, data.Balance.Adventurers);
            int rankGap = Math.Abs(QuestMath.RankNumber(initiator.GuildRank) - QuestMath.RankNumber(candidate.GuildRank));
            return overlapGain * decisions.PartnerOverlapGainWeight + relation / decisions.PartnerRelationDivisor
                + (friends ? decisions.PartnerFriendBonus : 0f) - rankGap * decisions.PartnerRankDifferencePenalty
                + DilemmaRules.PartnerBonus(initiator, candidate);
        }

        /// <summary>
        /// Лучший напарник для группы: кандидат с высшей оценкой напарника глазами того, чей взгляд <paramref name="lens"/> (при
        /// равенстве — первый в списке). <paramref name="trace"/> получает каждую оценку (для лога). Нет кандидатов — <c>null</c>.
        /// </summary>
        public static Adventurer BestPartner(PartyLens lens, List<Adventurer> group, IReadOnlyList<Adventurer> candidates,
            Action<Adventurer, float, float> trace = null)
        {
            if (candidates.Count == 0) return null;
            float before = lens.Overlap(group);
            Adventurer best = null;
            float bestScore = 0f;
            foreach (Adventurer candidate in candidates)
            {
                group.Add(candidate);
                float gain = lens.Overlap(group) - before;
                group.RemoveAt(group.Count - 1);
                float score = PartnerScore(lens.Viewer, candidate, gain, lens.Relations, lens.Data);
                trace?.Invoke(candidate, gain, score);
                if (best == null || score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>
        /// Предполагаемая группа инициатора (<paramref name="lens"/> — его взгляд на заказ, группа под задание): он сам, потом
        /// по одному лучший свободный кандидат по оценке напарника — пока это повышает его ценность варианта и в группе меньше
        /// <c>maxPartySize</c>. <see cref="PresumedParty.Values"/> — ценность после каждого шага (первая — он один).
        /// </summary>
        public static PresumedParty Presume(PartyLens lens, IReadOnlyList<Adventurer> candidates)
        {
            var group = new List<Adventurer> { lens.Viewer };
            var values = new List<float> { lens.Value(group) };
            var left = new List<Adventurer>(candidates);
            int max = lens.Data.Balance.Rounds.MaxPartySize;
            while (group.Count < max && left.Count > 0)
            {
                Adventurer best = BestPartner(lens, group, left);
                group.Add(best);
                float value = lens.Value(group);
                if (value <= values[values.Count - 1])
                {
                    group.RemoveAt(group.Count - 1);
                    break;
                }
                values.Add(value);
                left.Remove(best);
            }
            return new PresumedParty(group, values);
        }
    }

    /// <summary>
    /// Взгляд одного человека на один заказ: требования, какими он их видит, и его веса мотивов считаются один раз, профили
    /// людей берутся из <see cref="ProfileCache"/>. Дальше — перекрытие, шанс, оценки и ценность для любой группы.
    /// </summary>
    public sealed class PartyLens
    {
        private readonly float[] requirements;
        private readonly float[] profile = new float[Vocabulary.StatCount];
        private readonly float[] scores = new float[Vocabulary.MotiveCount];
        private readonly ProfileCache profiles;
        private readonly MotiveWeights weights;
        private readonly bool permanent;
        private readonly float commission;

        /// <param name="safetyMultiplier">Множитель мотива «Безопасность» в оценке заказа (распоряжения).</param>
        public PartyLens(Adventurer viewer, Order order, bool permanent, float commission, RelationBook relations, DataRegistry data,
            ProfileCache profiles = null, float safetyMultiplier = 1f)
        {
            Viewer = viewer;
            Order = order;
            Relations = relations;
            Data = data;
            this.permanent = permanent;
            this.commission = commission;
            this.profiles = profiles ?? new ProfileCache(data);
            requirements = QuestMath.PerceivedRequirements(order.Profile, order.DescriptionAccuracy, viewer, data);
            weights = Motives.Weigh(viewer, data, false);
            if (safetyMultiplier != 1f) weights.Multiply(Motive.Safety, safetyMultiplier);
        }

        public Adventurer Viewer { get; }
        public Order Order { get; }
        public RelationBook Relations { get; }
        public DataRegistry Data { get; }

        /// <summary>Воспринимаемое перекрытие профиля группы, без синергий.</summary>
        public float Overlap(IReadOnlyList<Adventurer> members)
        {
            Array.Clear(profile, 0, profile.Length);
            bool solo = members.Count == 1;
            foreach (Adventurer member in members)
            {
                float[] part = solo ? profiles.Solo(member) : profiles.Grouped(member);
                for (int i = 0; i < profile.Length; i++) profile[i] += part[i];
            }
            return QuestMath.Overlap(Data.Stats.RadarOrder, requirements, profile);
        }

        /// <summary>Шанс раунда: перекрытие + синергии пар (в группе), 0..1.</summary>
        public float Chance(IReadOnlyList<Adventurer> members)
        {
            float synergy = members.Count > 1 ? QuestMath.Synergy(members, Relations, Data) : 0f;
            return Math.Max(0f, Math.Min(1f, Overlap(members) + synergy));
        }

        /// <summary>Оценки варианта «этот заказ с этой группой» по мотивам.</summary>
        public void Scores(IReadOnlyList<Adventurer> members, float[] result)
        {
            float chance = Chance(members);
            QuestChoices.OrderScores(Viewer, Order, chance, commission, Data, result);
            float share = PartyMath.ExpectedShare(Order, commission, members, Viewer, permanent);
            result[(int)Motive.Money] = Math.Min(1f, share / QuestChoices.ExpenseScale(Viewer, Data)) * chance;
            result[(int)Motive.Companions] = PartyMath.Companions(Viewer, members, permanent, Relations, Data);
        }

        /// <summary>Ценность варианта для этого человека.</summary>
        public float Value(IReadOnlyList<Adventurer> members)
        {
            Array.Clear(scores, 0, scores.Length);
            Scores(members, scores);
            return PartyMath.Value(weights, scores);
        }
    }

    /// <summary>
    /// Профили людей для оценки групп: один — эффективные параметры × множитель одиночки, в группе — × Слаженность / 100.
    /// Считаются один раз на человека (в пределах часа параметры не меняются).
    /// </summary>
    public sealed class ProfileCache
    {
        private readonly DataRegistry data;
        private readonly Dictionary<int, float[]> solo = new Dictionary<int, float[]>();
        private readonly Dictionary<int, float[]> grouped = new Dictionary<int, float[]>();

        public ProfileCache(DataRegistry data)
        {
            this.data = data;
        }

        public float[] Solo(Adventurer person)
        {
            if (!solo.TryGetValue(person.Id, out float[] profile)) solo[person.Id] = profile = Build(person, data.Balance.Rounds.SoloProfileMultiplier);
            return profile;
        }

        public float[] Grouped(Adventurer person)
        {
            if (!grouped.TryGetValue(person.Id, out float[] profile)) grouped[person.Id] = profile = Build(person, person.GetStat(StatId.Cohesion) / 100f);
            return profile;
        }

        private float[] Build(Adventurer person, float multiplier)
        {
            var profile = new float[Vocabulary.StatCount];
            for (int i = 0; i < profile.Length; i++)
            {
                var stat = (StatId)i;
                if (Vocabulary.IsDiagramAxis(stat)) profile[i] = AdventurerStats.Effective(person, stat, data) * multiplier;
            }
            return profile;
        }
    }

    /// <summary>Предполагаемая группа: люди по порядку выбора (первый — инициатор) и ценность для инициатора после каждого шага.</summary>
    public sealed class PresumedParty
    {
        internal PresumedParty(List<Adventurer> members, List<float> values)
        {
            Members = members;
            Values = values;
        }

        public IReadOnlyList<Adventurer> Members { get; }

        /// <summary>Ценность для инициатора, если группа — первые (индекс + 1) человек.</summary>
        public IReadOnlyList<float> Values { get; }

        public float Value => Values[Values.Count - 1];

        /// <summary>
        /// Приемлемый минимум: наименьший размер (от 2), при котором ценность выше <paramref name="bestOther"/>; такого нет —
        /// вся предполагаемая группа.
        /// </summary>
        public int MinimumAbove(float bestOther)
        {
            for (int size = 2; size <= Values.Count; size++)
            {
                if (Values[size - 1] > bestOther) return size;
            }
            return Values.Count;
        }
    }
}
