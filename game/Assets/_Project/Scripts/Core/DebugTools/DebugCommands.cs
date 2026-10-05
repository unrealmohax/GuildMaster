using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Команды отладочной панели. Меняют мир так же, как игровые команды: через службы Core, случайность — поток
    /// <see cref="CommandSystem"/>, поэтому прогон с ними повторяется по зерну. Каждая пишет событие <see cref="SimEventType.DebugAction"/>
    /// (без строки ленты) — видно в логе, что мир правили руками.
    /// </summary>
    internal static class DebugAction
    {
        public static SimEvent Publish(SimContext ctx, string what, params int[] participants) =>
            ctx.Events.Publish(SimEventType.DebugAction, EventImportance.Normal, participants).With("what", what);
    }

    /// <summary>Деньги в казну (+) или из казны (−), отдельной статьёй журнала.</summary>
    public sealed class DebugMoneyCommand : ICommand
    {
        public DebugMoneyCommand(int amount)
        {
            Amount = amount;
        }

        public int Amount { get; }

        public void Apply(SimContext ctx)
        {
            if (Amount == 0) return;
            if (Amount > 0) TreasuryService.Credit(ctx, LedgerCategories.DebugIncome, Amount, "debug");
            else TreasuryService.Debit(ctx, LedgerCategories.DebugExpense, -Amount, "debug");
            DebugAction.Publish(ctx, "money").With("amount", Amount);
        }
    }

    /// <summary>Задать репутацию гильдии (обрезается пределами).</summary>
    public sealed class DebugSetReputationCommand : ICommand
    {
        public DebugSetReputationCommand(float value)
        {
            Value = value;
        }

        public float Value { get; }

        public void Apply(SimContext ctx)
        {
            float delta = Value - ctx.World.Guild.Reputation;
            if (Math.Abs(delta) < 0.001f) return;
            ReputationService.Change(ctx, delta, "debug");
            DebugAction.Publish(ctx, "reputation").With("value", ctx.World.Guild.Reputation);
        }
    }

    /// <summary>
    /// Новый человек в гильдии сразу, без кандидатства: тип (роль) задан, остальное — по генератору. Заданы черты — случайные
    /// черты «от рождения» заменяются ими (черты с партнёром пропускаются: партнёра нет).
    /// </summary>
    public sealed class DebugSpawnAdventurerCommand : ICommand
    {
        public DebugSpawnAdventurerCommand(string archetypeId, IReadOnlyList<string> traitIds = null)
        {
            ArchetypeId = archetypeId;
            TraitIds = traitIds ?? Array.Empty<string>();
        }

        public string ArchetypeId { get; }
        public IReadOnlyList<string> TraitIds { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;
            ctx.Data.TryGet(ArchetypeId, out ArchetypeDefinition type);

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Adventurer active in ctx.World.Adventurers.Active) names.Add(active.Name);
            GeneratedAdventurer generated = AdventurerGenerator.Generate(ctx.Rng, ctx.Data, ctx.World, ctx.World.Ids.Next(), atStart: false,
                names, type);
            Adventurer adventurer = generated.Adventurer;

            if (TraitIds.Count > 0)
            {
                foreach (TraitInstance trait in new List<TraitInstance>(adventurer.Traits))
                {
                    if (trait.PartnerId == 0) adventurer.RemoveTrait(trait);
                }
            }
            else
            {
                foreach (TraitInstance trait in new List<TraitInstance>(adventurer.Traits))
                {
                    if (trait.PartnerId != 0) adventurer.RemoveTrait(trait);
                }
            }

            ctx.World.Adventurers.AddActive(adventurer);
            ctx.Events.Publish(SimEventType.AdventurerJoined, EventImportance.Normal, adventurer.Id)
                .With("archetype", adventurer.ArchetypeId);

            foreach (string traitId in TraitIds)
            {
                if (!ctx.Data.TryGet(traitId, out SpecialTraitDefinition trait) || trait.RequiresPartner) continue;
                TraitService.TryAcquire(ctx, adventurer, traitId);
            }
            ArchetypeService.Recalculate(ctx, adventurer);
            DebugAction.Publish(ctx, "spawn-adventurer", adventurer.Id).With("type", generated.Type.Id);
        }
    }

    /// <summary>Задать параметр человека (обрезка 1..99); архетип пересчитывается.</summary>
    public sealed class DebugSetStatCommand : ICommand
    {
        public DebugSetStatCommand(int adventurerId, StatId stat, float value)
        {
            AdventurerId = adventurerId;
            Stat = stat;
            Value = value;
        }

        public int AdventurerId { get; }
        public StatId Stat { get; }
        public float Value { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.World.Adventurers.TryGetActive(AdventurerId, out Adventurer adventurer)) return;
            adventurer.SetStat(Stat, Math.Max(1f, Math.Min(ctx.Data.Balance.Adventurers.StatCap, Value)));
            ArchetypeService.Recalculate(ctx, adventurer);
            DebugAction.Publish(ctx, "stat", AdventurerId).With("stat", Stat).With("value", adventurer.GetStat(Stat));
        }
    }

    /// <summary>Задать ось характера (−100..100). Раскрытие оси не меняется.</summary>
    public sealed class DebugSetAxisCommand : ICommand
    {
        public DebugSetAxisCommand(int adventurerId, AxisId axis, float value)
        {
            AdventurerId = adventurerId;
            Axis = axis;
            Value = value;
        }

        public int AdventurerId { get; }
        public AxisId Axis { get; }
        public float Value { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.World.Adventurers.TryGetActive(AdventurerId, out Adventurer adventurer)) return;
            adventurer.SetAxis(Axis, AxisMath.Clamp(Value));
            DebugAction.Publish(ctx, "axis", AdventurerId).With("axis", Axis).With("value", adventurer.GetAxis(Axis));
        }
    }

    /// <summary>Задать показатель состояния (0..100): усталость, стресс, довольство, лояльность.</summary>
    public sealed class DebugSetStateCommand : ICommand
    {
        public DebugSetStateCommand(int adventurerId, StateStat stat, float value)
        {
            AdventurerId = adventurerId;
            Stat = stat;
            Value = value;
        }

        public int AdventurerId { get; }
        public StateStat Stat { get; }
        public float Value { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.World.Adventurers.TryGetActive(AdventurerId, out Adventurer adventurer)) return;
            AdventurerState state = adventurer.State;
            float value = StateRules.Clamp(Value);
            switch (Stat)
            {
                case StateStat.Fatigue: state.Fatigue = value; break;
                case StateStat.Stress: state.Stress = value; break;
                case StateStat.Contentment: state.Contentment = value; break;
                case StateStat.Loyalty: state.Loyalty = value; break;
            }
            DebugAction.Publish(ctx, "state", AdventurerId).With("stat", Stat).With("value", value);
        }
    }

    /// <summary>Новый заказ сразу на доске: тип и ранг заданы, остальное — по генератору (не важный, мимо Регистратора).</summary>
    public sealed class DebugSpawnOrderCommand : ICommand
    {
        public DebugSpawnOrderCommand(string questTypeId, GuildRank rank)
        {
            QuestTypeId = questTypeId;
            Rank = rank;
        }

        public string QuestTypeId { get; }
        public GuildRank Rank { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;
            ctx.Data.TryGet(QuestTypeId, out QuestTypeDefinition type);

            var errors = new List<string>();
            Order order = OrderGenerator.Generate(ctx.Rng, ctx.Data, ctx.World.Guild.Reputation, ctx.World.Orders.NextId(),
                ctx.World.Time.TotalHours, Rank, errors, type);
            foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "order #{0} description: {1}", order.Id, error);
            order.IsImportant = false;
            OrderSystem.Post(ctx, order);
            ctx.World.Orders.AddOpen(order);
            OrderSystem.WriteGenerated(ctx, "debug", order);
            OrderSystem.Publish(ctx, SimEventType.OrderPosted, order).With("by", "debug").With("expiresAt", order.ExpiresAtHours);
            DebugAction.Publish(ctx, "spawn-order").With("order", order.Id);
        }
    }

    /// <summary>Какое событие вызвать отладкой.</summary>
    public enum DebugEventKind
    {
        /// <summary>Событие в пути (засада, звери…) — в ближайший ходовой час задания в пути.</summary>
        Ambush,

        /// <summary>Находка — в ближайший ходовой час пути туда, если у задания её ещё нет.</summary>
        Discovery,

        /// <summary>Срыв человека — вид по его чертам.</summary>
        Breakdown,
    }

    /// <summary>
    /// Вызвать событие: засаду и находку — на задании (<see cref="TargetId"/> — id задания; событие случится в ближайший ходовой
    /// час обычным ходом задания), срыв — у человека (<see cref="TargetId"/> — id человека, сразу).
    /// </summary>
    public sealed class DebugEventCommand : ICommand
    {
        public DebugEventCommand(DebugEventKind kind, int targetId)
        {
            Kind = kind;
            TargetId = targetId;
        }

        public DebugEventKind Kind { get; }
        public int TargetId { get; }

        public void Apply(SimContext ctx)
        {
            switch (Kind)
            {
                case DebugEventKind.Ambush:
                {
                    if (!ctx.World.Quests.TryGetRun(TargetId, out QuestRun run) || run.IsPromotion) return;
                    if (run.Phase != QuestPhase.TravelOut && run.Phase != QuestPhase.TravelBack) return;
                    run.TravelEventHour = run.LegHoursDone + 1;
                    DebugAction.Publish(ctx, "ambush").With("quest", run.Id);
                    break;
                }
                case DebugEventKind.Discovery:
                {
                    if (!ctx.World.Quests.TryGetRun(TargetId, out QuestRun run) || run.IsPromotion) return;
                    if (run.Phase != QuestPhase.TravelOut || run.Discovery != null) return;
                    IReadOnlyList<DiscoveryDefinition> discoveries = ctx.Data.All<DiscoveryDefinition>();
                    if (discoveries.Count == 0) return;
                    run.Discovery = new QuestDiscovery(ctx.Rng.Pick(discoveries).Id, run.LegHoursDone + 1);
                    DebugAction.Publish(ctx, "discovery").With("quest", run.Id);
                    break;
                }
                case DebugEventKind.Breakdown:
                {
                    if (!ctx.World.Adventurers.TryGetActive(TargetId, out Adventurer adventurer)) return;
                    if (adventurer.State.IsOnQuest() || adventurer.State.Breakdown != BreakdownKind.None) return;
                    BreakdownKind kind = StateService.BreakdownKindOf(adventurer, ctx.Data);
                    if (kind == BreakdownKind.None) return;
                    StateService.StartBreakdown(ctx, adventurer, kind);
                    DebugAction.Publish(ctx, "breakdown", adventurer.Id);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Вызвать дилемму для человека (<see cref="AdventurerId"/>) в обход условий триггера, перезарядки и лимита открытых. Второй
    /// участник: у Влюблённых — партнёр по черте, иначе первый другой человек в гильдии; место ссоры из-за добычи — первое место
    /// из текстов заказов; брошенных у беглеца нет. Праздник Трактирщика — от Трактирщика (нет его — ничего).
    /// </summary>
    public sealed class DebugDilemmaCommand : ICommand
    {
        public DebugDilemmaCommand(DilemmaTrigger trigger, int adventurerId)
        {
            Trigger = trigger;
            AdventurerId = adventurerId;
        }

        public DilemmaTrigger Trigger { get; }
        public int AdventurerId { get; }

        public void Apply(SimContext ctx)
        {
            DilemmaDefinition definition = DilemmaRules.Find(ctx.Data, Trigger);
            if (definition == null) return;

            if (Trigger == DilemmaTrigger.TavernFeast)
            {
                if (!StaffRules.TryGetWithEffect(ctx.World, ctx.Data, StaffLevelEffect.TavernStressRelief, out StaffMember innkeeper)) return;
                Dilemma feast = DilemmaService.Open(ctx, definition, d => d.StaffId = innkeeper.Id);
                DebugAction.Publish(ctx, "dilemma").With("dilemma", feast.Id);
                return;
            }

            if (!ctx.World.Adventurers.TryGetActive(AdventurerId, out Adventurer subject)) return;
            Adventurer partner = null;
            if (Trigger == DilemmaTrigger.LoversSameParty || Trigger == DilemmaTrigger.LootDispute)
            {
                TraitInstance lover = TraitRules.FindWithHook(subject, TraitHook.LoverPartner, ctx.Data);
                if (Trigger == DilemmaTrigger.LoversSameParty && lover != null) ctx.World.Adventurers.TryGetActive(lover.PartnerId, out partner);
                if (partner == null)
                {
                    foreach (Adventurer other in ctx.World.Adventurers.Active)
                    {
                        if (other.Id == subject.Id) continue;
                        partner = other;
                        break;
                    }
                }
                if (partner == null) return;
            }

            Dilemma dilemma = DilemmaService.Open(ctx, definition, d =>
            {
                d.SubjectId = subject.Id;
                if (partner != null) d.PartnerId = partner.Id;
                if (Trigger == DilemmaTrigger.LoanRequest)
                {
                    d.Amount = DilemmaRules.LoanAmount(subject, ctx.Data.Balance);
                    d.Reason = FirstReason(definition, subject);
                }
                if (Trigger == DilemmaTrigger.LootDispute) d.Place = FirstPlace(ctx.Data);
            });
            DebugAction.Publish(ctx, "dilemma", subject.Id).With("dilemma", dilemma.Id);
        }

        private static string FirstReason(DilemmaDefinition definition, Adventurer subject)
        {
            string neutral = string.Empty;
            foreach (DilemmaReason reason in definition.Reasons)
            {
                if (reason == null) continue;
                if (reason.RequiresTrait != null && subject.HasTrait(reason.RequiresTrait.Id)) return reason.Text;
                if (reason.RequiresTrait == null && neutral.Length == 0) neutral = reason.Text;
            }
            return neutral;
        }

        private static NounForms FirstPlace(DataRegistry data)
        {
            if (data.OrderTexts == null) return null;
            foreach (QuestTypeTexts texts in data.OrderTexts.QuestTypes)
            {
                if (texts != null && texts.Places.Count > 0) return texts.Places[0];
            }
            return null;
        }
    }
}
