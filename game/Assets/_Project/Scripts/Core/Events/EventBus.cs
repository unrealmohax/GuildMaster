using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// События текущего такта. Системы публикуют, FeedSystem и AutopauseSystem читают;
    /// после такта список очищается.
    /// </summary>
    public sealed class EventBus
    {
        private readonly WorldState world;
        private readonly List<SimEvent> events = new List<SimEvent>();

        internal EventBus(WorldState world)
        {
            this.world = world;
        }

        /// <summary>События, накопленные с конца прошлого такта, в порядке публикации.</summary>
        public IReadOnlyList<SimEvent> Events => events;

        /// <summary>Система, которая сейчас работает; ставится симуляцией.</summary>
        internal string CurrentSource { get; set; } = string.Empty;

        public SimEvent Publish(SimEventType type, EventImportance importance = EventImportance.Normal, params int[] participants)
        {
            var simEvent = new SimEvent(type, importance, world.Time.TotalHours, CurrentSource, participants);
            events.Add(simEvent);
            return simEvent;
        }

        internal void Clear() => events.Clear();
    }
}
