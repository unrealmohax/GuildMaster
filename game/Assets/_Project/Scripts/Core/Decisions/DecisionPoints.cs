using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    public enum DecisionPointKind
    {
        /// <summary>Начало ночи: сон, без выбора.</summary>
        Night,

        /// <summary>Начало вечера: таверна или отдых.</summary>
        Evening,

        /// <summary>Утро, раз в сутки: что делать сегодня.</summary>
        Morning,

        /// <summary>Человек освободился (кончился срыв, зажила рана, вступил в гильдию): что делать дальше, если не ночь.</summary>
        Freed,
    }

    /// <summary>Точка решения: когда наступает для свободного человека и какие варианты в ней есть.</summary>
    public sealed class DecisionPoint
    {
        public DecisionPoint(DecisionPointKind kind, Func<SimContext, Adventurer, bool> isDue, IReadOnlyList<DecisionAction> options,
            Action<SimContext, Adventurer> onDecided = null, bool offersOrders = false, bool offersServices = false)
        {
            Kind = kind;
            IsDue = isDue;
            Options = options;
            OnDecided = onDecided;
            OffersOrders = offersOrders;
            OffersServices = offersServices;
        }

        /// <summary>
        /// В этой точке можно пойти на двор или лечь в Лазарет: варианты добавляются, только когда постройка готова (двор) или
        /// есть рана, свободная койка и Лекарь (Лазарет).
        /// </summary>
        public bool OffersServices { get; }

        /// <summary>В этой точке можно взять заказ с доски: к вариантам добавляется по варианту на заказ (<see cref="OrderChoice"/>).</summary>
        public bool OffersOrders { get; }

        public DecisionPointKind Kind { get; }

        /// <summary>Точка наступила для этого человека (он уже свободен).</summary>
        public Func<SimContext, Adventurer, bool> IsDue { get; }

        public IReadOnlyList<DecisionAction> Options { get; }

        /// <summary>Что отметить после решения (утро — «сегодня уже решал»).</summary>
        public Action<SimContext, Adventurer> OnDecided { get; }
    }

    /// <summary>
    /// Точки решения по порядку: в час срабатывает первая наступившая. Новая точка — строка в <see cref="Default"/>.
    /// <list type="bullet">
    /// <item>Ночь — в час начала ночи, сон без выбора.</item>
    /// <item>Вечер — в час начала вечера.</item>
    /// <item>Утро — в первый утренний час, когда человек свободен, раз в сутки (обычно в начале утра; в первый день игры
    /// и после срыва — позже).</item>
    /// <item>Освободился — в прошлом часу человек не был свободен, а сейчас свободен и не ночь; сюда же — только что
    /// вступивший и вернувшийся с задания.</item>
    /// </list>
    /// Утром и когда освободился можно ещё взять заказ с доски, пойти на двор или лечь в Лазарет. Решения на задании (после провала, по находке) — точка
    /// «по событию», их принимает система заданий.
    /// </summary>
    public static class DecisionPoints
    {
        private static readonly DecisionAction[] RestOrTavern = { DecisionActions.Rest, DecisionActions.Tavern };

        public static IReadOnlyList<DecisionPoint> Default { get; } = new[]
        {
            new DecisionPoint(DecisionPointKind.Night,
                (ctx, a) => ctx.World.Time.Hour == ctx.Data.Balance.Time.NightHour,
                new[] { DecisionActions.Sleep }),
            new DecisionPoint(DecisionPointKind.Evening,
                (ctx, a) => ctx.World.Time.Hour == ctx.Data.Balance.Time.EveningHour,
                RestOrTavern),
            new DecisionPoint(DecisionPointKind.Morning,
                (ctx, a) => ctx.Rhythm.PhaseAt(ctx.World.Time.Hour) == DayPhase.Morning && a.State.MorningDecisionDay != Today(ctx),
                RestOrTavern,
                (ctx, a) => a.State.MorningDecisionDay = Today(ctx),
                offersOrders: true, offersServices: true),
            new DecisionPoint(DecisionPointKind.Freed,
                (ctx, a) => !a.State.WasFreeLastHour && ctx.Rhythm.PhaseAt(ctx.World.Time.Hour) != DayPhase.Night,
                RestOrTavern,
                offersOrders: true, offersServices: true),
        };

        /// <summary>Номер суток от начала календаря.</summary>
        public static long Today(SimContext ctx) => ctx.World.Time.TotalHours / ctx.Calendar.HoursPerDay;

        /// <summary>
        /// Человек сам решает, чем заняться: не на задании, не в запое и не «сел и не смог подняться», не в Лазарете,
        /// без тяжёлой раны, не пропускает день (Пьяница). Отказ от заданий решениям о свободном времени не мешает.
        /// </summary>
        public static bool CanDecide(AdventurerState state, long now) =>
            !state.IsOnQuest()
            && state.Breakdown != BreakdownKind.Binge && state.Breakdown != BreakdownKind.Collapse
            && !state.InInfirmary && !state.HasHeavyWound()
            && now >= state.SkipsDayUntilHours;
    }

    /// <summary>Запрет варианта: правило и причина для лога. Новый запрет — строка в <see cref="DecisionBans.Default"/>.</summary>
    public sealed class DecisionBan
    {
        public DecisionBan(string reason, Func<SimContext, Adventurer, DecisionAction, bool> applies)
        {
            Reason = reason;
            Applies = applies;
        }

        /// <summary>Причина запрета для лога.</summary>
        public string Reason { get; }

        /// <summary>Вариант запрещён этому человеку сейчас.</summary>
        public Func<SimContext, Adventurer, DecisionAction, bool> Applies { get; }
    }

    /// <summary>
    /// Запреты вариантов. Утром и днём таверна — только Пьянице или при стрессе выше <c>daytimeTavernStress</c>. Заказ нельзя
    /// взять ни одному, ни с группой: выше ранга гильдии человека (кроме своего экзамена на следующий ранг); при тяжёлой ране,
    /// усталости выше 90, срыве; если выйти в следующем часу уже поздно; чужой экзамен; экзамен — только одному; заказ уже взят
    /// (кроме приглашения: заказ уже у той группы, которая зовёт). Тренировка: двор полон (мест — вместимость двора, считаются
    /// и те, кто выбрал двор в этом часу); уже тренировался сегодня; есть рана; усталость выше 90; в кошельке меньше платы.
    /// Распоряжение «только группой»: заказ ранга из его области нельзя взять одному (экзамен — можно).
    /// </summary>
    public static class DecisionBans
    {
        public static IReadOnlyList<DecisionBan> Default { get; } = new[]
        {
            new DecisionBan("tavern in daytime: not a drunkard, stress not above daytimeTavernStress", IsDaytimeTavern),
            new DecisionBan("order above guild rank", (ctx, a, action) => OrderOf(ctx, action, out Order o) && !o.IsPromotion && !GuildRanks.CanTakeOrder(a, o.Rank)),
            new DecisionBan("no quests: heavy wound, fatigue above ban, breakdown",
                (ctx, a, action) => action.IsOrder && !StateRules.CanTakeQuests(a.State, ctx.Data.Balance.State)),
            new DecisionBan("too late to depart next hour",
                (ctx, a, action) => action.IsOrder && !ctx.Rhythm.CanStartQuest(ctx.World.Time.Hour + 1)),
            new DecisionBan("someone else's promotion", (ctx, a, action) => OrderOf(ctx, action, out Order o) && o.IsPromotion && o.OwnerId != a.Id),
            new DecisionBan("promotion is taken alone",
                (ctx, a, action) => action.Kind != DecisionActionKind.TakeOrder && OrderOf(ctx, action, out Order o) && o.IsPromotion),
            new DecisionBan("order taken by others",
                (ctx, a, action) => action.Kind != DecisionActionKind.JoinParty && OrderOf(ctx, action, out Order o) && o.Status != OrderStatus.OnBoard),
            new DecisionBan("training yard is full", IsYardFull),
            new DecisionBan("already trained today",
                (ctx, a, action) => action.Kind == DecisionActionKind.Train && a.State.TrainedOnDay == DecisionPoints.Today(ctx)),
            new DecisionBan("no training while wounded",
                (ctx, a, action) => action.Kind == DecisionActionKind.Train && a.State.Conditions.Count > 0),
            new DecisionBan("too tired to train",
                (ctx, a, action) => action.Kind == DecisionActionKind.Train && StateRules.IsTooTiredForQuests(a.State, ctx.Data.Balance.State)),
            new DecisionBan("cannot pay for training",
                (ctx, a, action) => action.Kind == DecisionActionKind.Train && a.State.Wallet < WalletService.Coins(ctx.Data.Balance.Expenses.Training)),
            new DecisionBan("solo forbidden by decree",
                (ctx, a, action) => action.Kind == DecisionActionKind.TakeOrder && OrderOf(ctx, action, out Order o)
                    && DecreeRules.IsSoloBanned(ctx.World, ctx.Data, o)),
        };

        private static bool IsYardFull(SimContext ctx, Adventurer adventurer, DecisionAction action)
        {
            if (action.Kind != DecisionActionKind.Train) return false;
            if (!BuildingRules.IsReady(ctx.World, ctx.Data, BuildingFunction.TrainingYard, out BuildingDefinition yard)) return true;
            int capacity = BuildingRules.Capacity(yard);
            return capacity > 0 && BuildingRules.Trainees(ctx.World, adventurer) >= capacity;
        }

        private static bool OrderOf(SimContext ctx, DecisionAction action, out Order order)
        {
            order = null;
            return action.IsOrder && ctx.World.Orders.TryGetOrder(action.OrderId, out order);
        }

        private static bool IsDaytimeTavern(SimContext ctx, Adventurer adventurer, DecisionAction action)
        {
            if (action.Kind != DecisionActionKind.Tavern) return false;
            DayPhase phase = ctx.Rhythm.PhaseAt(ctx.World.Time.Hour);
            if (phase != DayPhase.Morning && phase != DayPhase.Day) return false;
            if (TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, ctx.Data) != null) return false;
            return adventurer.State.Stress <= ctx.Data.Balance.Decisions.DaytimeTavernStress;
        }
    }
}
