using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    public enum Gender
    {
        Male,
        Female,
    }

    /// <summary>Где живёт человек: город или Общежитие. Общежития пока нет — все живут в городе.</summary>
    public enum Housing
    {
        City,
        Dorm,
    }

    /// <summary>Почему человек в архиве.</summary>
    public enum LeaveReason
    {
        None,
        Left,
        Died,
        Disappeared,
        Expelled,
    }

    /// <summary>
    /// Авантюрист. Часть мира: снаружи Core только чтение, менять — системами и службами Core
    /// (<see cref="Growth"/>, <see cref="TraitService"/>, <see cref="RevealService"/>, <see cref="GuildRanks"/>…).
    /// Параметры — базовые значения по <see cref="StatId"/>; эффективные — <see cref="AdventurerStats"/>.
    /// Занятие — в <see cref="AdventurerState"/>.
    /// </summary>
    public sealed class Adventurer
    {
        private readonly float[] stats = new float[Vocabulary.StatCount];
        private readonly float[] axes = new float[Vocabulary.AxisCount];
        private readonly bool[] revealedAxes = new bool[Vocabulary.AxisCount];
        private readonly long[] axisRevealedAtHours = new long[Vocabulary.AxisCount];
        private readonly List<TraitInstance> traits = new List<TraitInstance>();

        internal Adventurer(int id)
        {
            Id = id;
        }

        public int Id { get; }
        public string Name { get; internal set; } = string.Empty;
        public Gender Gender { get; internal set; }

        /// <summary>Только для вида: старения в прототипе нет.</summary>
        public int Age { get; internal set; }

        /// <summary>Базовые параметры по <see cref="StatId"/>: не ниже естественного минимума, выше 99 — только ростом.</summary>
        public IReadOnlyList<float> Stats => stats;

        /// <summary>Оси характера по <see cref="AxisId"/>, −100..+100.</summary>
        public IReadOnlyList<float> Axes => axes;

        /// <summary>Раскрыта ли ось (полюс или «уравновешен»), по <see cref="AxisId"/>.</summary>
        public IReadOnlyList<bool> RevealedAxes => revealedAxes;

        /// <summary>Особые черты (0–4), в порядке появления.</summary>
        public IReadOnlyList<TraitInstance> Traits => traits;

        public GuildRank GuildRank { get; internal set; }

        /// <summary>Очки ранга. Дробные: Частичный успех даёт очки × 0,5.</summary>
        public float RankPoints { get; internal set; }

        /// <summary>С какого момента доступно задание на повышение (после провала — через <c>promotionRetryDays</c>).</summary>
        public long PromotionReadyAtHours { get; internal set; }

        /// <summary>Архетип по параметрам (<see cref="ArchetypeCalculator"/>), id <see cref="ArchetypeDefinition"/>.</summary>
        public string ArchetypeId { get; internal set; } = string.Empty;

        /// <summary>Ранг характеристик — оценка лучшей роли (у Мастера на все руки — среднее всех 14).</summary>
        public float PowerScore { get; internal set; }

        /// <summary>Показатели состояния, раны, занятие.</summary>
        public AdventurerState State { get; } = new AdventurerState();

        public Housing Housing { get; internal set; }

        /// <summary>Когда вступил в гильдию; у кандидата — когда пришёл.</summary>
        public long JoinedAtHours { get; internal set; }

        /// <summary>Заданий выполнено и не выполнено (с тех, где человек дошёл до конца).</summary>
        public int QuestsCompleted { get; internal set; }
        public int QuestsFailed { get; internal set; }

        /// <summary>Беглец: бросил группу на задании и вернулся в гильдию.</summary>
        public bool IsDeserter { get; internal set; }

        /// <summary>Когда последний раз сбежал с задания (для отчёта месяца); 0 — не сбегал.</summary>
        public long LastFledAtHours { get; internal set; }

        /// <summary>Сколько утр подряд человек мог взять заказ, был здоров и бодр, но не взял (для раскрытия лени).</summary>
        public int IdleOrderDays { get; internal set; }

        /// <summary>Группа, с которой человек сейчас собирается или идёт на задание; 0 — ни с какой.</summary>
        public int PartyId { get; internal set; }

        /// <summary>Постоянная группа человека; 0 — не состоит.</summary>
        public int PermanentPartyId { get; internal set; }

        /// <summary>Когда ушёл из гильдии (архив); 0 — в гильдии.</summary>
        public long LeftAtHours { get; internal set; }

        public LeaveReason LeaveReason { get; internal set; }

        public float GetStat(StatId stat) => stats[(int)stat];

        public float GetAxis(AxisId axis) => axes[(int)axis];

        public bool IsAxisRevealed(AxisId axis) => revealedAxes[(int)axis];

        /// <summary>Когда ось раскрыта (для отчёта месяца); 0 — не раскрыта.</summary>
        public long GetAxisRevealedAtHours(AxisId axis) => axisRevealedAtHours[(int)axis];

        public bool HasTrait(string traitId) => TryGetTrait(traitId, out _);

        public bool TryGetTrait(string traitId, out TraitInstance trait)
        {
            foreach (TraitInstance instance in traits)
            {
                if (instance.TraitId == traitId)
                {
                    trait = instance;
                    return true;
                }
            }
            trait = null;
            return false;
        }

        internal void SetStat(StatId stat, float value) => stats[(int)stat] = value;

        internal void SetAxis(AxisId axis, float value) => axes[(int)axis] = value;

        internal void SetAxisRevealed(AxisId axis, long atHours = 0)
        {
            revealedAxes[(int)axis] = true;
            axisRevealedAtHours[(int)axis] = atHours;
        }

        internal void AddTrait(TraitInstance trait) => traits.Add(trait);

        internal bool RemoveTrait(TraitInstance trait) => traits.Remove(trait);
    }
}
