using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Часть мира: переключатели автопаузы и причина последней автопаузы. Меняется только командой
    /// <see cref="SetAutopauseCommand"/> и <see cref="AutopauseSystem"/>. По умолчанию все виды включены.
    /// </summary>
    public sealed class AutopauseState
    {
        private readonly HashSet<AutopauseKind> disabled = new HashSet<AutopauseKind>();
        private readonly List<AutopauseTrigger> triggers = new List<AutopauseTrigger>();

        /// <summary>
        /// События, из-за которых встала пауза в конце последнего такта, в порядке публикации (для окна автопаузы).
        /// Пусто — последний такт закончился без автопаузы.
        /// </summary>
        public IReadOnlyList<AutopauseTrigger> Triggers => triggers;

        public bool IsEnabled(AutopauseKind kind) => !disabled.Contains(kind);

        internal void SetEnabled(AutopauseKind kind, bool enabled)
        {
            if (enabled) disabled.Remove(kind);
            else disabled.Add(kind);
        }

        internal void ClearTriggers() => triggers.Clear();

        internal void AddTrigger(AutopauseTrigger trigger) => triggers.Add(trigger);
    }

    /// <summary>Событие, вызвавшее автопаузу, и вид автопаузы, под который оно попало.</summary>
    public readonly struct AutopauseTrigger
    {
        public AutopauseTrigger(AutopauseKind kind, SimEvent simEvent)
        {
            Kind = kind;
            Event = simEvent;
        }

        public AutopauseKind Kind { get; }
        public SimEvent Event { get; }
    }
}
