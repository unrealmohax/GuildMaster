using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>Стадия постройки.</summary>
    public enum BuildingState
    {
        /// <summary>В очереди на стройку: деньги ещё не списаны.</summary>
        Planned,

        /// <summary>Строится: деньги списаны, готова в <see cref="Building.ConstructionEndsAtHours"/>.</summary>
        UnderConstruction,

        /// <summary>Готова и работает.</summary>
        Ready,
    }

    /// <summary>
    /// Постройка гильдии: определение, уровень, стадия и сроки. Кто в ней живёт, лечится или тренируется, записано у людей
    /// (жильё, койка, занятие) — счёт по людям даёт <see cref="BuildingRules"/>.
    /// </summary>
    public sealed class Building
    {
        internal Building(int id, string definitionId)
        {
            Id = id;
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            Level = 1;
        }

        public int Id { get; }
        public string DefinitionId { get; }
        public int Level { get; internal set; }
        public BuildingState State { get; internal set; }

        /// <summary>Когда поставлена в очередь (у построек со старта — начало игры).</summary>
        public long QueuedAtHours { get; internal set; }

        /// <summary>Когда началась стройка.</summary>
        public long ConstructionStartedAtHours { get; internal set; }

        /// <summary>Когда стройка кончится (у готовой — когда кончилась).</summary>
        public long ConstructionEndsAtHours { get; internal set; }

        /// <summary>Сколько заплачено за стройку.</summary>
        public int PaidCost { get; internal set; }

        public bool IsReady => State == BuildingState.Ready;
    }

    /// <summary>
    /// Постройки гильдии: все (в порядке появления), текущая стройка и очередь. Одна стройка одновременно, остальные ждут
    /// в очереди в порядке, который задаёт игрок. Каждое определение — не больше одной постройки.
    /// </summary>
    public sealed class BuildingBook
    {
        private readonly List<Building> all = new List<Building>();
        private readonly List<Building> queue = new List<Building>();
        private int nextId = 1;

        internal BuildingBook()
        {
        }

        /// <summary>Все постройки: готовые, строящаяся, в очереди — в порядке появления.</summary>
        public IReadOnlyList<Building> All => all;

        /// <summary>Очередь строек, первая начнётся следующей.</summary>
        public IReadOnlyList<Building> Queue => queue;

        /// <summary>Постройка, которая строится сейчас; <c>null</c> — стройки нет.</summary>
        public Building Current { get; internal set; }

        public bool TryGetByDefinition(string definitionId, out Building building)
        {
            foreach (Building entry in all)
            {
                if (entry.DefinitionId == definitionId)
                {
                    building = entry;
                    return true;
                }
            }
            building = null;
            return false;
        }

        public bool TryGetById(int id, out Building building)
        {
            foreach (Building entry in all)
            {
                if (entry.Id == id)
                {
                    building = entry;
                    return true;
                }
            }
            building = null;
            return false;
        }

        /// <summary>Постройка этого определения есть и готова.</summary>
        public bool IsReady(string definitionId) => TryGetByDefinition(definitionId, out Building building) && building.IsReady;

        internal Building Create(string definitionId)
        {
            var building = new Building(nextId++, definitionId);
            all.Add(building);
            return building;
        }

        internal void Enqueue(Building building) => queue.Add(building);

        internal void RemoveFromQueue(Building building) => queue.Remove(building);

        /// <summary>Убрать постройку совсем (снята из очереди).</summary>
        internal void Remove(Building building)
        {
            queue.Remove(building);
            all.Remove(building);
        }

        internal void SetQueueOrder(List<Building> order)
        {
            queue.Clear();
            queue.AddRange(order);
        }
    }
}
