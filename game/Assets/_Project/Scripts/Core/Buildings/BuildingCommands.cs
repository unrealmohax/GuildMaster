using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Игрок заказывает постройку: стройка встаёт в очередь и начинается, как только нет другой стройки и в казне хватает денег
    /// (цена списывается при начале). Постройка уже есть или в очереди, нет такого определения — ничего.
    /// </summary>
    public sealed class StartBuildingCommand : ICommand
    {
        public StartBuildingCommand(string definitionId)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
        }

        public string DefinitionId { get; }

        public void Apply(SimContext ctx) => BuildingService.Enqueue(ctx, DefinitionId);
    }

    /// <summary>Игрок задаёт порядок очереди строек: id всех построек очереди, каждая один раз. Не так — ничего.</summary>
    public sealed class ReorderBuildQueueCommand : ICommand
    {
        public ReorderBuildQueueCommand(IReadOnlyList<int> buildingIds)
        {
            BuildingIds = buildingIds ?? throw new ArgumentNullException(nameof(buildingIds));
        }

        public IReadOnlyList<int> BuildingIds { get; }

        public void Apply(SimContext ctx) => BuildingService.Reorder(ctx, BuildingIds);
    }

    /// <summary>
    /// Игрок заказывает улучшение готовой постройки с уровнями (Арсенал): улучшение встаёт в ту же очередь строек. Постройки нет,
    /// не готова, уже на наибольшем уровне или улучшение уже заказано — ничего.
    /// </summary>
    public sealed class UpgradeBuildingCommand : ICommand
    {
        public UpgradeBuildingCommand(int buildingId)
        {
            BuildingId = buildingId;
        }

        public int BuildingId { get; }

        public void Apply(SimContext ctx) => BuildingService.EnqueueUpgrade(ctx, BuildingId);
    }

    /// <summary>Игрок снимает стройку или улучшение из очереди. Начатое снять нельзя.</summary>
    public sealed class CancelBuildingCommand : ICommand
    {
        public CancelBuildingCommand(int buildingId)
        {
            BuildingId = buildingId;
        }

        public int BuildingId { get; }

        public void Apply(SimContext ctx) => BuildingService.Cancel(ctx, BuildingId);
    }
}
