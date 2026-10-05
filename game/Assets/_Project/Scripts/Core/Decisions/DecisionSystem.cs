using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 8 такта: люди сами выбирают, чем заняться. Каждый час:
    /// <list type="number">
    /// <item>в начале ночи — итоги вечера в таверне (<see cref="TavernEvening"/>);</item>
    /// <item>по каждому свободному (<see cref="DecisionPoints.CanDecide"/>) — первая наступившая точка решения
    /// (<see cref="DecisionPoints.Default"/>) и решение:
    /// запреты (<see cref="DecisionBans"/>) → веса мотивов (<see cref="Motives"/>) → оценки вариантов (<see cref="DecisionActions"/>),
    /// ценность = Σ вес × оценка → лучший с вероятностью <c>bestChoiceChance</c>, иначе второй → главные причины;</item>
    /// <item>вечером отмечает, кто в таверне.</item>
    /// </list>
    /// Утром и когда освободился — ещё тренировка на дворе и лечение в Лазарете (<see cref="DecisionPoint.OffersServices"/>);
    /// выбравший лечение сразу занимает койку.
    /// Выбранное занятие действует со следующего часа (<see cref="AdventurerState.PlannedActivity"/>).
    /// Лог: решение и причины — <see cref="SimLogLevel.Info"/>, запреты и сон — <see cref="SimLogLevel.Debug"/>, оценки по
    /// вариантам и мотивам — <see cref="SimLogLevel.Trace"/>. Причины обычных решений игроку не показываются.
    /// Группы (утром постоянные решают первыми, «собрать группу», приглашения) — во второй части класса.
    /// </summary>
    public sealed partial class DecisionSystem : ISimSystem
    {
        private readonly List<DecisionAction> candidates = new List<DecisionAction>();
        private readonly List<DecisionAction> allowed = new List<DecisionAction>();
        private readonly List<Option> options = new List<Option>();

        /// <summary>Кто уже решил в этом часу (в том числе ответом на приглашение) — второй раз не решает.</summary>
        private readonly HashSet<int> decidedThisHour = new HashSet<int>();

        private readonly IInfirmary infirmary;

        public DecisionSystem() : this(BuildingInfirmary.Instance)
        {
        }

        /// <summary>Свой запрос к Лазарету (тот же, что у <see cref="HealthSystem"/>) — для тестов.</summary>
        public DecisionSystem(IInfirmary infirmary)
        {
            this.infirmary = infirmary ?? throw new System.ArgumentNullException(nameof(infirmary));
        }

        public string Name => nameof(DecisionSystem);

        public void Tick(SimContext ctx)
        {
            int hour = ctx.World.Time.Hour;
            if (hour == ctx.Data.Balance.Time.NightHour) TavernEvening.Settle(ctx);

            bool evening = ctx.Rhythm.PhaseAt(hour) == DayPhase.Evening;
            long now = ctx.World.Time.TotalHours;
            var scope = new DecisionScope(ctx);
            decidedThisHour.Clear();
            if (ctx.Rhythm.PhaseAt(hour) == DayPhase.Morning) PermanentPartiesDecide(scope);
            foreach (Adventurer adventurer in new List<Adventurer>(ctx.World.Adventurers.Active))
            {
                AdventurerState state = adventurer.State;
                if (evening && state.Activity == Activity.Tavern) state.InTavernThisEvening = true;

                bool free = DecisionPoints.CanDecide(state, now);
                if (free && !decidedThisHour.Contains(adventurer.Id) && state.PlannedOrderId == 0)
                {
                    DecisionPoint point = FindPoint(ctx, adventurer);
                    if (point != null) Decide(scope, adventurer, point);
                }
                state.WasFreeLastHour = free;
            }
        }

        private static DecisionPoint FindPoint(SimContext ctx, Adventurer adventurer)
        {
            foreach (DecisionPoint point in DecisionPoints.Default)
            {
                if (point.IsDue(ctx, adventurer)) return point;
            }
            return null;
        }

        private void Decide(DecisionScope scope, Adventurer adventurer, DecisionPoint point)
        {
            SimContext ctx = scope.Ctx;
            candidates.Clear();
            candidates.AddRange(point.Options);
            if (point.OffersOrders) candidates.AddRange(OrderChoice.Actions(ctx, adventurer));
            if (point.OffersServices) AddServices(ctx, adventurer, candidates);
            FilterBans(ctx, adventurer, candidates, allowed);
            if (point.OffersOrders) AddSeekParty(scope, adventurer, allowed);
            if (allowed.Count == 0)
            {
                WriteNoOptions(ctx.Log, adventurer, point);
                return;
            }

            if (allowed.Count == 1 && !allowed[0].HasScores)
            {
                adventurer.State.PlannedActivity = allowed[0].Activity;
                if (ctx.Log.IsOn(SimLogLevel.Debug)) WriteNoChoice(ctx.Log, adventurer, point, allowed[0]);
                point.OnDecided?.Invoke(ctx, adventurer);
                return;
            }

            Evaluate(scope, adventurer, allowed, options);
            if (ctx.Log.IsOn(SimLogLevel.Trace)) WriteScores(ctx.Log, adventurer, Point(point), options);

            if (!Choose(ctx, adventurer, options, out Option chosen, out bool best))
            {
                point.OnDecided?.Invoke(ctx, adventurer);
                return;
            }

            adventurer.State.PlannedActivity = chosen.Action.Activity;
            if (chosen.Action.Kind == DecisionActionKind.Heal) HealthService.Admit(ctx, adventurer, self: true);
            if (ctx.Log.IsOn(SimLogLevel.Info)) WriteDecision(ctx.Log, adventurer, Point(point), chosen, best, options, ctx.Data.Balance.Decisions.MaxReasons);

            if (chosen.Action.Kind == DecisionActionKind.TakeOrder)
            {
                OrderChoice.Take(ctx, adventurer, chosen.Action, allowed);
            }
            else if (chosen.Action.Kind == DecisionActionKind.SeekParty)
            {
                Gather(scope, adventurer, point, chosen, new List<Option>(options), new List<DecisionAction>(allowed));
            }
            else if (point.OffersOrders)
            {
                Option? bestOrder = BestOrder(options);
                if (bestOrder.HasValue) OrderChoice.Refused(ctx, adventurer, bestOrder.Value.Action, point.Kind == DecisionPointKind.Morning);
            }
            point.OnDecided?.Invoke(ctx, adventurer);
        }

        /// <summary>
        /// Варианты построек: тренировка — если двор готов (запреты двора — <see cref="DecisionBans"/>); лечение — если есть рана,
        /// Лекарь и свободная койка. Построек нет — вариантов нет, и выбор идёт как без них.
        /// </summary>
        private void AddServices(SimContext ctx, Adventurer adventurer, List<DecisionAction> actions)
        {
            if (!ctx.Data.HasDefinitions) return;
            if (BuildingRules.IsReady(ctx.World, ctx.Data, BuildingFunction.TrainingYard)) actions.Add(DecisionActions.Train);
            if (adventurer.State.Conditions.Count > 0 && HealthSystem.FreeBeds(ctx, infirmary) > 0) actions.Add(DecisionActions.Heal);
        }

        /// <summary>
        /// Оценить варианты: ценность каждого для человека (в вариантах с заказом «Безопасность» × множитель распоряжений);
        /// по убыванию ценности, при равенстве — по порядку.
        /// </summary>
        private static void Evaluate(DecisionScope scope, Adventurer adventurer, List<DecisionAction> actions, List<Option> result)
        {
            result.Clear();
            foreach (DecisionAction action in actions)
            {
                MotiveWeights weights = Motives.Weigh(adventurer, scope.Ctx.Data, action.InTavern);
                if (action.IsOrder && scope.SafetyMultiplier != 1f) weights.Multiply(Motive.Safety, scope.SafetyMultiplier);
                float[] scores = DecisionActions.Scores(scope, adventurer, action);
                float value = 0f;
                for (int m = 0; m < scores.Length; m++) value += weights[(Motive)m] * scores[m];
                result.Add(new Option(action, weights, scores, value, result.Count));
            }
            result.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Order.CompareTo(b.Order));
        }

        /// <summary>
        /// Лучший вариант с вероятностью <c>bestChoiceChance</c>, иначе второй (один вариант — без броска). Кошмары: от выбранного
        /// заказа можно отказаться — тогда лучший из остальных. <c>false</c> — отказался от всех вариантов.
        /// </summary>
        private static bool Choose(SimContext ctx, Adventurer adventurer, List<Option> sorted, out Option chosen, out bool best)
        {
            chosen = sorted[0];
            best = true;
            if (sorted.Count > 1 && !ctx.RollChance(ctx.Data.Balance.Decisions.BestChoiceChance, "decision-best", adventurer))
            {
                chosen = sorted[1];
                best = false;
            }

            while (OrderChoice.RefusesFromNightmares(ctx, adventurer, chosen.Action))
            {
                sorted.Remove(chosen);
                if (sorted.Count == 0) return false;
                chosen = sorted[0];
                best = true;
            }
            return true;
        }

        /// <summary>Самый ценный вариант «взять заказ» (варианты уже по убыванию ценности); нет — <c>null</c>.</summary>
        private static Option? BestOrder(List<Option> sorted)
        {
            foreach (Option option in sorted)
            {
                if (option.Action.Kind == DecisionActionKind.TakeOrder) return option;
            }
            return null;
        }

        private static void FilterBans(SimContext ctx, Adventurer adventurer, List<DecisionAction> actions, List<DecisionAction> result)
        {
            result.Clear();
            foreach (DecisionAction action in actions)
            {
                DecisionBan ban = FindBan(ctx, adventurer, action);
                if (ban == null)
                {
                    result.Add(action);
                    continue;
                }
                if (ctx.Log.IsOn(SimLogLevel.Debug))
                {
                    StringBuilder line = ctx.Log.Begin(SimLogLevel.Debug);
                    AdventurerLog.AppendName(line.Append("ban "), adventurer)
                        .Append(' ').Append(action.Label).Append(": ").Append(ban.Reason)
                        .Append(" stress=").Append(AdventurerLog.Number(adventurer.State.Stress));
                    ctx.Log.Commit();
                }
            }
        }

        private static DecisionBan FindBan(SimContext ctx, Adventurer adventurer, DecisionAction action)
        {
            foreach (DecisionBan ban in DecisionBans.Default)
            {
                if (ban.Applies(ctx, adventurer, action)) return ban;
            }
            return null;
        }

        /// <summary>
        /// Главные причины выбора: мотивы с наибольшим положительным вкладом (вес × оценка), не больше <paramref name="max"/>;
        /// у мотива, усиленного состоянием, — фактор: «Comfort 0.95 (stress 72)».
        /// </summary>
        public static List<(Motive Motive, float Contribution)> MainReasons(MotiveWeights weights, float[] scores, int max)
        {
            var reasons = new List<(Motive, float)>();
            for (int m = 0; m < scores.Length; m++)
            {
                float contribution = weights[(Motive)m] * scores[m];
                if (contribution > 0f) reasons.Add(((Motive)m, contribution));
            }
            reasons.Sort((a, b) => a.Item2 != b.Item2 ? b.Item2.CompareTo(a.Item2) : a.Item1.CompareTo(b.Item1));
            if (reasons.Count > max) reasons.RemoveRange(max, reasons.Count - max);
            return reasons;
        }

        /// <summary>«decide #3 Имя evening: Tavern 2.21 (best; Rest 1.43) because Comfort 0.95 (stress 72), Safety 1, Companions 0.6».</summary>
        private static void WriteDecision(SimLogger log, Adventurer adventurer, string point, Option chosen, bool best,
            List<Option> all, int maxReasons)
        {
            StringBuilder line = log.Begin(SimLogLevel.Info);
            AdventurerLog.AppendName(line.Append("decide "), adventurer)
                .Append(' ').Append(point).Append(": ").Append(chosen.Action.Label).Append(' ').Append(AdventurerLog.Number(chosen.Value))
                .Append(" (").Append(best ? "best" : "second");
            foreach (Option other in all)
            {
                if (other.Action == chosen.Action) continue;
                line.Append("; ").Append(other.Action.Label).Append(' ').Append(AdventurerLog.Number(other.Value));
            }
            line.Append(") because ");

            List<(Motive Motive, float Contribution)> reasons = MainReasons(chosen.Weights, chosen.Scores, maxReasons);
            for (int i = 0; i < reasons.Count; i++)
            {
                if (i > 0) line.Append(", ");
                line.Append(reasons[i].Motive).Append(' ').Append(AdventurerLog.Number(reasons[i].Contribution));
                if (chosen.Weights.TryGetFactor(reasons[i].Motive, out StateFactor factor, out float value))
                    line.Append(" (").Append(FactorName(factor)).Append(' ').Append(AdventurerLog.Number(value)).Append(')');
            }
            if (reasons.Count == 0) line.Append('-');
            log.Commit();
        }

        /// <summary>«scores #3 Имя evening Tavern=2.21: Money 1x-0.01 Glory 1x0 …».</summary>
        private static void WriteScores(SimLogger log, Adventurer adventurer, string point, List<Option> all)
        {
            foreach (Option option in all)
            {
                StringBuilder line = log.Begin(SimLogLevel.Trace);
                AdventurerLog.AppendName(line.Append("scores "), adventurer)
                    .Append(' ').Append(point).Append(' ').Append(option.Action.Label).Append('=').Append(AdventurerLog.Number(option.Value))
                    .Append(':');
                for (int m = 0; m < option.Scores.Length; m++)
                {
                    line.Append(' ').Append((Motive)m).Append(' ')
                        .Append(AdventurerLog.Number(option.Weights[(Motive)m])).Append('x').Append(AdventurerLog.Number(option.Scores[m]));
                }
                log.Commit();
            }
        }

        private static void WriteNoChoice(SimLogger log, Adventurer adventurer, DecisionPoint point, DecisionAction action)
        {
            StringBuilder line = log.Begin(SimLogLevel.Debug);
            AdventurerLog.AppendName(line.Append("decide "), adventurer)
                .Append(' ').Append(Point(point)).Append(": ").Append(action.Label).Append(" (no choice)");
            log.Commit();
        }

        private static void WriteNoOptions(SimLogger log, Adventurer adventurer, DecisionPoint point)
        {
            if (!log.IsOn(SimLogLevel.Debug)) return;
            StringBuilder line = log.Begin(SimLogLevel.Debug);
            AdventurerLog.AppendName(line.Append("decide "), adventurer).Append(' ').Append(Point(point)).Append(": no options");
            log.Commit();
        }

        private static string Point(DecisionPoint point) => point.Kind.ToString().ToLower(CultureInfo.InvariantCulture);

        private static string FactorName(StateFactor factor)
        {
            switch (factor)
            {
                case StateFactor.LowWallet: return "wallet";
                case StateFactor.Fatigue: return "fatigue";
                case StateFactor.Stress: return "stress";
                default: return "light wound";
            }
        }

        private readonly struct Option
        {
            public Option(DecisionAction action, MotiveWeights weights, float[] scores, float value, int order)
            {
                Action = action;
                Weights = weights;
                Scores = scores;
                Value = value;
                Order = order;
            }

            public DecisionAction Action { get; }
            public MotiveWeights Weights { get; }
            public float[] Scores { get; }
            public float Value { get; }
            public int Order { get; }
        }
    }
}
