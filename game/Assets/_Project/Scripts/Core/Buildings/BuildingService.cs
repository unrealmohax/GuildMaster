using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Изменения построек: старт игры, очередь и стройка, готовность, переселение в Общежитие.
    /// <list type="bullet">
    /// <item>Одна стройка одновременно. Новая стройка встаёт в конец очереди; голова очереди начинается, как только стройки нет
    /// и денег в казне не меньше цены: цена списывается (<see cref="LedgerCategories.Construction"/>), срок — дни из
    /// определения, стройка идёт круглые сутки. Денег не хватает — очередь ждёт, следующие не обгоняют голову.</item>
    /// <item>Каждое определение — не больше одной постройки; постройки не разрушаются.</item>
    /// <item>Улучшение (только у постройки с уровнями — Арсенал) — та же стройка в той же очереди: цена × <c>levelCostMultiplier</c>^(уровень − 1),
    /// срок × <c>levelTimeMultiplier</c>^(уровень − 1), где уровень — новый; пока идёт, постройка работает на прежнем уровне.</item>
    /// <item>Содержание — × уровень; Зал гильдии ещё берёт <c>upkeepPerAdventurer</c> за каждого человека в гильдии.</item>
    /// <item>Общежитие: пока есть места, из города переселяются те, кто дольше в гильдии (при равенстве — меньший id).</item>
    /// </list>
    /// </summary>
    public static class BuildingService
    {
        /// <summary>Постройки «есть на старте» — готовы с начала игры, без событий.</summary>
        internal static void ApplyStart(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;

            long now = ctx.World.Time.TotalHours;
            foreach (BuildingDefinition definition in ctx.Data.All<BuildingDefinition>())
            {
                if (!definition.BuiltAtStart) continue;
                Building building = ctx.World.Buildings.Create(definition.Id);
                building.State = BuildingState.Ready;
                building.QueuedAtHours = now;
                building.ConstructionStartedAtHours = now;
                building.ConstructionEndsAtHours = now;
            }
        }

        /// <summary>
        /// Поставить стройку в очередь; можно начать сразу — начинается. Нельзя (нет такого определения, постройка уже есть или
        /// в очереди) — <c>false</c>.
        /// </summary>
        internal static bool Enqueue(SimContext ctx, string definitionId)
        {
            if (!ctx.Data.TryGet(definitionId, out BuildingDefinition definition)) return false;
            BuildingBook book = ctx.World.Buildings;
            if (book.TryGetByDefinition(definitionId, out _)) return false;

            Building building = book.Create(definitionId);
            building.State = BuildingState.Planned;
            building.QueuedAtHours = ctx.World.Time.TotalHours;
            book.Enqueue(building);

            if (!TryStartNext(ctx) || book.Current != building)
                Publish(ctx, SimEventType.BuildingQueued, EventImportance.Normal, definition);
            return true;
        }

        /// <summary>
        /// Поставить улучшение готовой постройки в очередь; можно начать сразу — начинается. Нельзя (постройки нет, не готова,
        /// уже на наибольшем уровне, улучшение уже в очереди) — <c>false</c>.
        /// </summary>
        internal static bool EnqueueUpgrade(SimContext ctx, int buildingId)
        {
            BuildingBook book = ctx.World.Buildings;
            if (!book.TryGetById(buildingId, out Building building) || !building.IsReady || building.IsUpgrading) return false;
            if (!ctx.Data.TryGet(building.DefinitionId, out BuildingDefinition definition) || building.Level >= definition.MaxLevel) return false;

            building.UpgradingToLevel = building.Level + 1;
            building.QueuedAtHours = ctx.World.Time.TotalHours;
            book.Enqueue(building);

            if (!TryStartNext(ctx) || book.Current != building)
                Publish(ctx, SimEventType.BuildingQueued, EventImportance.Normal, definition).With("level", building.UpgradingToLevel);
            return true;
        }

        /// <summary>Цена стройки до уровня <paramref name="level"/> (1 — сама постройка): цена × <c>levelCostMultiplier</c>^(уровень − 1).</summary>
        public static int CostFor(BuildingDefinition definition, int level) =>
            (int)Math.Round(definition.Cost * Math.Pow(definition.LevelCostMultiplier, level - 1), MidpointRounding.AwayFromZero);

        /// <summary>Срок стройки до уровня <paramref name="level"/>, дней: срок × <c>levelTimeMultiplier</c>^(уровень − 1).</summary>
        public static int DaysFor(BuildingDefinition definition, int level) =>
            (int)Math.Round(definition.BuildDays * Math.Pow(definition.LevelTimeMultiplier, level - 1), MidpointRounding.AwayFromZero);

        /// <summary>Снять стройку или улучшение из очереди (начатое снять нельзя).</summary>
        internal static bool Cancel(SimContext ctx, int buildingId)
        {
            BuildingBook book = ctx.World.Buildings;
            if (!book.TryGetById(buildingId, out Building building) || book.Current == building) return false;
            if (building.IsUpgrading)
            {
                book.RemoveFromQueue(building);
                building.UpgradingToLevel = 0;
                if (ctx.Data.TryGet(building.DefinitionId, out BuildingDefinition upgraded))
                    Publish(ctx, SimEventType.BuildingCancelled, EventImportance.Normal, upgraded).With("level", building.Level + 1);
                TryStartNext(ctx);
                return true;
            }
            if (building.State != BuildingState.Planned) return false;

            book.Remove(building);
            if (ctx.Data.TryGet(building.DefinitionId, out BuildingDefinition definition))
                Publish(ctx, SimEventType.BuildingCancelled, EventImportance.Normal, definition);
            TryStartNext(ctx);
            return true;
        }

        /// <summary>
        /// Новый порядок очереди: <paramref name="order"/> — id построек очереди, каждая ровно один раз. Не так — ничего.
        /// Новая голова может начаться сразу.
        /// </summary>
        internal static bool Reorder(SimContext ctx, IReadOnlyList<int> order)
        {
            BuildingBook book = ctx.World.Buildings;
            if (order == null || order.Count != book.Queue.Count) return false;

            var result = new List<Building>(order.Count);
            foreach (int id in order)
            {
                Building found = null;
                foreach (Building queued in book.Queue)
                {
                    if (queued.Id == id) found = queued;
                }
                if (found == null || result.Contains(found)) return false;
                result.Add(found);
            }

            book.SetQueueOrder(result);
            ctx.Events.Publish(SimEventType.BuildQueueReordered).With("count", result.Count);
            TryStartNext(ctx);
            return true;
        }

        /// <summary>Начать голову очереди, если стройки нет и денег хватает. Возвращает, началась ли стройка.</summary>
        internal static bool TryStartNext(SimContext ctx)
        {
            BuildingBook book = ctx.World.Buildings;
            if (book.Current != null || book.Queue.Count == 0) return false;

            Building building = book.Queue[0];
            BuildingDefinition definition = ctx.Data.Get<BuildingDefinition>(building.DefinitionId);
            int level = building.IsUpgrading ? building.UpgradingToLevel : 1;
            int cost = CostFor(definition, level);
            if (!TreasuryService.CanAfford(ctx.World, cost)) return false;
            LedgerCategory category = definition.SettlementProject ? LedgerCategories.Projects : LedgerCategories.Construction;
            if (!TreasuryService.Debit(ctx, category, cost, definition.Id)) return false;

            long now = ctx.World.Time.TotalHours;
            book.RemoveFromQueue(building);
            book.Current = building;
            if (!building.IsUpgrading) building.State = BuildingState.UnderConstruction;
            building.PaidCost += cost;
            building.ConstructionStartedAtHours = now;
            building.ConstructionEndsAtHours = now + ctx.Calendar.DaysToHours(DaysFor(definition, level));
            SimEvent started = Publish(ctx, SimEventType.BuildingStarted, EventImportance.Normal, definition)
                .With("cost", cost)
                .With("endsAt", building.ConstructionEndsAtHours);
            if (building.IsUpgrading) started.With("level", level);
            return true;
        }

        /// <summary>
        /// Стройка кончилась (срок вышел): улучшение — новый уровень; постройка — готова, в поселении прибавляются её работники,
        /// а у проекта поселения — и его жители (<see cref="SettlementService.ProjectReady"/>).
        /// </summary>
        internal static void CompleteIfDue(SimContext ctx)
        {
            BuildingBook book = ctx.World.Buildings;
            Building building = book.Current;
            if (building == null || ctx.World.Time.TotalHours < building.ConstructionEndsAtHours) return;

            book.Current = null;
            BuildingDefinition definition = ctx.Data.Get<BuildingDefinition>(building.DefinitionId);
            if (building.IsUpgrading)
            {
                building.Level = building.UpgradingToLevel;
                building.UpgradingToLevel = 0;
                Publish(ctx, SimEventType.BuildingUpgraded, EventImportance.Notable, definition).With("level", building.Level).With("count", building.Level);
                return;
            }

            building.State = BuildingState.Ready;
            Publish(ctx, SimEventType.BuildingReady, EventImportance.Notable, definition);
            SettlementService.BuildingReady(ctx);
            if (definition.SettlementProject) SettlementService.ProjectReady(ctx, definition.Function);
        }

        /// <summary>Переселить людей из города в Общежитие, пока есть места.</summary>
        internal static void SettleDormitory(SimContext ctx)
        {
            if (!BuildingRules.IsReady(ctx.World, ctx.Data, BuildingFunction.Dormitory, out BuildingDefinition dormitory)) return;

            int free = BuildingRules.Capacity(dormitory) - BuildingRules.DormitoryResidents(ctx.World);
            if (free <= 0) return;

            var city = new List<Adventurer>();
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                if (adventurer.Housing == Housing.City) city.Add(adventurer);
            }
            city.Sort((a, b) => a.JoinedAtHours != b.JoinedAtHours ? a.JoinedAtHours.CompareTo(b.JoinedAtHours) : a.Id.CompareTo(b.Id));

            for (int i = 0; i < city.Count && i < free; i++)
            {
                city[i].Housing = Housing.Dorm;
                ctx.Events.Publish(SimEventType.MovedToDormitory, EventImportance.Normal, city[i].Id);
            }
        }

        /// <summary>
        /// Содержание — обязательный расход, даже в минус: запись на каждую готовую постройку (× уровень) и запись «за людей»
        /// (<c>upkeepPerAdventurer</c> × людей в гильдии, комментарий <see cref="PeopleUpkeepComment"/>, связанный id — сколько людей).
        /// </summary>
        internal static void PayUpkeep(SimContext ctx)
        {
            foreach (Building building in ctx.World.Buildings.All)
            {
                if (!building.IsReady || !ctx.Data.TryGet(building.DefinitionId, out BuildingDefinition definition)) continue;
                TreasuryService.Debit(ctx, LedgerCategories.Upkeep, definition.UpkeepPerMonth * building.Level, definition.Id);
            }
            TreasuryService.Debit(ctx, LedgerCategories.Upkeep, BuildingRules.PeopleUpkeep(ctx.World, ctx.Data), PeopleUpkeepComment,
                ctx.World.Adventurers.Active.Count);
        }

        /// <summary>Комментарий записи журнала «содержание за людей».</summary>
        public const string PeopleUpkeepComment = "people";

        private static SimEvent Publish(SimContext ctx, SimEventType type, EventImportance importance, BuildingDefinition definition) =>
            ctx.Events.Publish(type, importance)
                .With("building", definition.NameForms)
                .With("definition", definition.Id);
    }
}
