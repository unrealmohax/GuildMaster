using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 12 такта: постройки гильдии (<see cref="BuildingService"/>).
    /// <list type="number">
    /// <item>Каждый час — стройка: срок вышел — постройка готова; нет стройки — начинается голова очереди, если хватает денег.</item>
    /// <item>В 00:00 и в час, когда готово Общежитие, — переселение в Общежитие.</item>
    /// <item>Каждый час — тренировка: у каждого на дворе час занятия (<see cref="Growth.Train"/>); что тренируется, выбирается
    /// в первый час занятия (<see cref="Growth.PickTrainingStat"/>, поток этой системы).</item>
    /// <item>В начале месяца — содержание готовых построек (обязательный расход).</item>
    /// </list>
    /// Пока построек, кроме стартовых, нет и никто не тренируется, бросков нет. Гильдия закрыта — ничего.
    /// </summary>
    public sealed class BuildingSystem : ISimSystem
    {
        public string Name => nameof(BuildingSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions || ctx.World.Treasury.IsClosed) return;

            GameTime time = ctx.World.Time;
            bool dormitoryWasReady = BuildingRules.IsReady(ctx.World, ctx.Data, BuildingFunction.Dormitory);
            BuildingService.CompleteIfDue(ctx);
            BuildingService.TryStartNext(ctx);

            bool dormitoryOpened = !dormitoryWasReady && BuildingRules.IsReady(ctx.World, ctx.Data, BuildingFunction.Dormitory);
            if (time.Hour == 0 || dormitoryOpened) BuildingService.SettleDormitory(ctx);

            Train(ctx);

            if (time.Hour == 0 && time.Day == 1) BuildingService.PayUpkeep(ctx);
        }

        private static void Train(SimContext ctx)
        {
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                AdventurerState state = adventurer.State;
                if (state.Activity != Activity.Training) continue;

                if (!state.TrainingStat.HasValue) state.TrainingStat = Growth.PickTrainingStat(adventurer, ctx.Data, ctx.Rng);
                StatId stat = state.TrainingStat.Value;
                float gain = Growth.Train(ctx, adventurer, stat, 1f);
                if (ctx.Log.IsOn(SimLogLevel.Trace))
                    ctx.Log.Write(SimLogLevel.Trace, "train #{0} {1} {2} +{3}", adventurer.Id, adventurer.Name, stat, AdventurerLog.Number(gain));
            }
        }
    }
}
