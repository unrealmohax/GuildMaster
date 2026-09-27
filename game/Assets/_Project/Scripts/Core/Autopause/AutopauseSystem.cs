using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 16 такта: если в такте было событие из <see cref="AutopauseRules"/> и его вид включён — запомнить
    /// событие и попросить паузу. Случайных чисел не тратит.
    /// </summary>
    public sealed class AutopauseSystem : ISimSystem
    {
        private readonly IReadOnlyDictionary<SimEventType, AutopauseKind> rules;

        public AutopauseSystem() : this(AutopauseRules.Default)
        {
        }

        /// <summary>Свои правила вместо <see cref="AutopauseRules.Default"/> — для тестов.</summary>
        internal AutopauseSystem(IReadOnlyDictionary<SimEventType, AutopauseKind> rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public string Name => nameof(AutopauseSystem);

        public void Tick(SimContext ctx)
        {
            AutopauseState autopause = ctx.World.Autopause;
            autopause.ClearTriggers();

            foreach (SimEvent simEvent in ctx.Events.Events)
            {
                if (rules.TryGetValue(simEvent.Type, out AutopauseKind kind) && autopause.IsEnabled(kind))
                    autopause.AddTrigger(new AutopauseTrigger(kind, simEvent));
            }

            if (autopause.Triggers.Count > 0) ctx.RequestPause();
        }
    }
}
