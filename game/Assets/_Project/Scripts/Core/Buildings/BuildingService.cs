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

        /// <summary>Снять стройку из очереди (начатую снять нельзя).</summary>
        internal static bool Cancel(SimContext ctx, int buildingId)
        {
            BuildingBook book = ctx.World.Buildings;
            if (!book.TryGetById(buildingId, out Building building) || building.State != BuildingState.Planned) return false;

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
            if (!TreasuryService.CanAfford(ctx.World, definition.Cost)) return false;
            if (!TreasuryService.Debit(ctx, LedgerCategories.Construction, definition.Cost, definition.Id)) return false;

            long now = ctx.World.Time.TotalHours;
            book.RemoveFromQueue(building);
            book.Current = building;
            building.State = BuildingState.UnderConstruction;
            building.PaidCost = definition.Cost;
            building.ConstructionStartedAtHours = now;
            building.ConstructionEndsAtHours = now + ctx.Calendar.DaysToHours(definition.BuildDays);
            Publish(ctx, SimEventType.BuildingStarted, EventImportance.Normal, definition)
                .With("cost", definition.Cost)
                .With("endsAt", building.ConstructionEndsAtHours);
            return true;
        }

        /// <summary>Стройка кончилась (срок вышел) — постройка готова.</summary>
        internal static void CompleteIfDue(SimContext ctx)
        {
            BuildingBook book = ctx.World.Buildings;
            Building building = book.Current;
            if (building == null || ctx.World.Time.TotalHours < building.ConstructionEndsAtHours) return;

            book.Current = null;
            building.State = BuildingState.Ready;
            Publish(ctx, SimEventType.BuildingReady, EventImportance.Notable, ctx.Data.Get<BuildingDefinition>(building.DefinitionId));
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

        /// <summary>Содержание готовых построек — обязательный расход, даже в минус; запись на постройку.</summary>
        internal static void PayUpkeep(SimContext ctx)
        {
            foreach (Building building in ctx.World.Buildings.All)
            {
                if (!building.IsReady || !ctx.Data.TryGet(building.DefinitionId, out BuildingDefinition definition)) continue;
                TreasuryService.Debit(ctx, LedgerCategories.Upkeep, definition.UpkeepPerMonth, definition.Id);
            }
        }

        private static SimEvent Publish(SimContext ctx, SimEventType type, EventImportance importance, BuildingDefinition definition) =>
            ctx.Events.Publish(type, importance)
                .With("building", definition.NameForms)
                .With("definition", definition.Id);
    }
}
