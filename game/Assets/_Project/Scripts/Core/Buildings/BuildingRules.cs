using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Чистые вопросы о постройках: готова ли постройка с назначением, сколько в ней мест и сколько занято, содержание.
    /// Мир не меняют. Кто занимает места, записано у людей: жильё (<see cref="Adventurer.Housing"/>), койка
    /// (<see cref="AdventurerState.InInfirmary"/>), занятие (<see cref="Activity.Training"/>); считаются только люди в гильдии.
    /// </summary>
    public static class BuildingRules
    {
        /// <summary>Постройка с назначением <paramref name="function"/> есть в данных и готова.</summary>
        public static bool IsReady(WorldState world, DataRegistry data, BuildingFunction function, out BuildingDefinition definition)
        {
            definition = data.HasDefinitions ? data.BuildingWith(function) : null;
            return definition != null && world.Buildings.IsReady(definition.Id);
        }

        public static bool IsReady(WorldState world, DataRegistry data, BuildingFunction function) => IsReady(world, data, function, out _);

        /// <summary>Мест у готовой постройки (0 — без ограничения или постройки нет — смотреть <see cref="IsReady(WorldState, DataRegistry, BuildingFunction)"/>).</summary>
        public static int Capacity(BuildingDefinition definition) => definition != null ? definition.Capacity : 0;

        /// <summary>Сколько людей живёт в Общежитии.</summary>
        public static int DormitoryResidents(WorldState world)
        {
            int count = 0;
            foreach (Adventurer adventurer in world.Adventurers.Active)
            {
                if (adventurer.Housing == Housing.Dorm) count++;
            }
            return count;
        }

        /// <summary>Сколько людей лежит в Лазарете.</summary>
        public static int Patients(WorldState world)
        {
            int count = 0;
            foreach (Adventurer adventurer in world.Adventurers.Active)
            {
                if (adventurer.State.InInfirmary) count++;
            }
            return count;
        }

        /// <summary>
        /// Сколько людей на дворе со следующего часа: уже тренируются и не решили иначе или выбрали тренировку в этом часу.
        /// <paramref name="except"/> — не считать (тот, кто сейчас решает).
        /// </summary>
        public static int Trainees(WorldState world, Adventurer except = null)
        {
            int count = 0;
            foreach (Adventurer adventurer in world.Adventurers.Active)
            {
                if (adventurer == except) continue;
                AdventurerState state = adventurer.State;
                if ((state.PlannedActivity ?? state.Activity) == Activity.Training) count++;
            }
            return count;
        }

        /// <summary>Содержание готовых построек в месяц.</summary>
        public static int MonthlyUpkeep(WorldState world, DataRegistry data)
        {
            int total = 0;
            foreach (Building building in world.Buildings.All)
            {
                if (building.IsReady && data.TryGet(building.DefinitionId, out BuildingDefinition definition)) total += definition.UpkeepPerMonth;
            }
            return total;
        }
    }
}
