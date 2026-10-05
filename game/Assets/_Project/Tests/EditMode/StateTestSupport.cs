using System;
using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Мир для тестов состояния и здоровья: реальные определения, числа по умолчанию, системы по умолчанию,
    /// стартовая шестёрка сразу уходит в архив — в гильдии только люди, которых тест добавил сам. Системы заказов нет,
    /// стартовые заказы сняты с доски: заданий нет, тест видит только события своих людей. Стартовый персонал (Регистратор,
    /// Трактирщик) остаётся, но с уровнем <see cref="NeutralStaffLevel"/> — эффект должности × 1, снятие стресса в таверне
    /// не зависит от зерна. Обращений по умолчанию нет (система обращений убрана): их ответы по сроку меняли бы довольство,
    /// кошельки и состав гильдии; тесты обращений включают её — <c>dilemmas: true</c>.
    /// </summary>
    internal sealed class StateWorld : IDisposable
    {
        /// <summary>Уровень возможностей, при котором эффект должности — × 1.</summary>
        public const int NeutralStaffLevel = 50;

        public StateWorld(uint seed = 7u, IInfirmary infirmary = null, ISimSystem beforeState = null, SimLogger log = null, bool dilemmas = false)
        {
            Data = new PeopleData();
            List<ISimSystem> systems = SimulationSystems.CreateDefault();
            systems.RemoveAll(s => s is OrderSystem);
            if (!dilemmas) systems.RemoveAll(s => s is DilemmaSystem);
            if (infirmary != null)
            {
                systems[systems.FindIndex(s => s is HealthSystem)] = new HealthSystem(infirmary);
                systems[systems.FindIndex(s => s is DecisionSystem)] = new DecisionSystem(infirmary);
            }
            if (beforeState != null) systems.Insert(systems.FindIndex(s => s is StateSystem), beforeState);
            Simulation = new Simulation(Data.Registry, seed, systems, log);

            Do(ctx =>
            {
                foreach (Adventurer adventurer in ctx.World.Adventurers.Active.ToList()) AdventurerLifecycle.Retire(ctx, adventurer, LeaveReason.Left);
                foreach (Order order in ctx.World.Orders.Open.ToList()) ctx.World.Orders.Close(order, ctx.Data.Balance.Orders.ClosedOrdersLimit);
                foreach (StaffMember member in ctx.World.Staff.Members) member.Level = NeutralStaffLevel;
            });
        }

        public PeopleData Data { get; }
        public Simulation Simulation { get; }
        public DataRegistry Registry => Data.Registry;
        public BalanceSettings Balance => Data.Balance;
        public GameTime Time => Simulation.World.Time;

        /// <summary>Человек в гильдии: все параметры 30, оси 0 (без полюсов), без черт; состояние — ровное, денег много.</summary>
        public Adventurer Add(params string[] traits)
        {
            var adventurer = new Adventurer(Simulation.World.Ids.Next()) { Name = "Тест", JoinedAtHours = Time.TotalHours };
            for (int i = 0; i < Vocabulary.StatCount; i++) adventurer.SetStat((StatId)i, 30f);
            AdventurerState state = adventurer.State;
            state.Fatigue = 10f;
            state.Stress = 10f;
            state.Contentment = 50f;
            state.Loyalty = 50f;
            state.Wallet = 1000;
            foreach (string trait in traits) TraitService.AddAtGeneration(adventurer, Registry.Get<SpecialTraitDefinition>(trait), Time.TotalHours, 0);
            ArchetypeService.Initialize(adventurer, Registry);
            Simulation.World.Adventurers.AddActive(adventurer);
            return adventurer;
        }

        public List<Adventurer> AddMany(int count, params string[] traits) => Enumerable.Range(0, count).Select(_ => Add(traits)).ToList();

        public void Do(Action<SimContext> action) => SimulationRun.Do(Simulation, action);

        public T Do<T>(Func<SimContext, T> action)
        {
            T result = default;
            SimulationRun.Do(Simulation, ctx => result = action(ctx));
            return result;
        }

        /// <summary>Такты до ближайшего часа <paramref name="hour"/> (не текущего).</summary>
        public void TickToHour(int hour)
        {
            do Simulation.Tick();
            while (Time.Hour != hour);
        }

        public void Days(int days) => SimulationRun.Days(Simulation, days);

        /// <summary>События прогона.</summary>
        public List<SimEvent> Collect(Action run) => SimulationRun.Collect(Simulation, _ => run());

        public void Dispose() => Data.Dispose();
    }

    /// <summary>Система-действие: вызвать код в своём месте такта (например, держать стресс перед StateSystem).</summary>
    internal sealed class LambdaSystem : ISimSystem
    {
        private readonly Action<SimContext> action;

        public LambdaSystem(string name, Action<SimContext> action)
        {
            Name = name;
            this.action = action;
        }

        public string Name { get; }

        public void Tick(SimContext ctx) => action(ctx);
    }

    /// <summary>Допуск для частоты случайного события: <paramref name="sigmas"/> стандартных отклонения биномиального числа успехов.</summary>
    internal static class Frequency
    {
        public static float Tolerance(int trials, float chance, float sigmas = 3f) =>
            sigmas * (float)Math.Sqrt(trials * chance * (1f - chance));
    }

    /// <summary>
    /// Лазарет для тестов здоровья без построек и персонала: койки, Лекарь и скорость задаются прямо. Подменяет запрос
    /// и у лечения, и у решений (<see cref="StateWorld"/>).
    /// </summary>
    internal sealed class FakeInfirmary : IInfirmary
    {
        public FakeInfirmary(int beds, bool hasMedic = true, float speed = 1f)
        {
            BedCount = beds;
            Medic = hasMedic;
            Speed = speed;
        }

        public int BedCount { get; set; }
        public bool Medic { get; set; }
        public float Speed { get; set; }

        public int Beds(SimContext ctx) => BedCount;
        public bool HasMedic(SimContext ctx) => Medic;
        public float HealingSpeed(SimContext ctx) => Speed;
    }
}
