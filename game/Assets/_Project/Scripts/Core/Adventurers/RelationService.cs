using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Отношения между людьми: изменение значения и счётчика совместных заданий, метки.
    /// Кто и на сколько меняет (таверна, задания, бегство, ссоры, дилеммы) — вызывающие системы, суммы — <see cref="AdventurersBalance"/>.
    /// </summary>
    public static class RelationService
    {
        /// <summary>Край шкалы отношений: −100..+100.</summary>
        public const float Limit = 100f;

        /// <summary>Сдвинуть отношения пары на <paramref name="delta"/> в пределах ±100. Возвращает новое значение.</summary>
        public static float Change(SimContext ctx, int a, int b, float delta)
        {
            Relation relation = ctx.World.Relations.GetOrCreate(a, b);
            relation.Value = Clamp(relation.Value + delta);
            return relation.Value;
        }

        /// <summary>Поставить значение (готовые пары на старте, дилеммы). Возвращает новое значение.</summary>
        public static float Set(SimContext ctx, int a, int b, float value)
        {
            Relation relation = ctx.World.Relations.GetOrCreate(a, b);
            relation.Value = Clamp(value);
            return relation.Value;
        }

        /// <summary>+1 совместное задание (для «давних напарников»).</summary>
        public static int AddJointQuest(SimContext ctx, int a, int b)
        {
            Relation relation = ctx.World.Relations.GetOrCreate(a, b);
            relation.JointQuests++;
            return relation.JointQuests;
        }

        /// <summary>+1 совместно выполненное задание (для постоянных групп).</summary>
        public static int AddJointSuccess(SimContext ctx, int a, int b)
        {
            Relation relation = ctx.World.Relations.GetOrCreate(a, b);
            relation.JointSuccesses++;
            return relation.JointSuccesses;
        }

        public static RelationLabels LabelsOf(float value, int jointQuests, AdventurersBalance balance)
        {
            RelationLabels labels = RelationLabels.None;
            if (value >= balance.FriendsThreshold) labels |= RelationLabels.Friends;
            if (value <= balance.DislikeThreshold) labels |= RelationLabels.Dislike;
            if (jointQuests >= balance.LongPartnersQuests) labels |= RelationLabels.LongPartners;
            return labels;
        }

        public static RelationLabels LabelsOf(RelationBook relations, int a, int b, AdventurersBalance balance) =>
            LabelsOf(relations.GetValue(a, b), relations.GetJointQuests(a, b), balance);

        public static bool AreFriends(RelationBook relations, int a, int b, AdventurersBalance balance) =>
            (LabelsOf(relations, a, b, balance) & RelationLabels.Friends) != 0;

        private static float Clamp(float value) => Math.Max(-Limit, Math.Min(Limit, value));
    }
}
