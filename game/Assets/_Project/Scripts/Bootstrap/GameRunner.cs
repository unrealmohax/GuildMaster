using System;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.UI;
using UnityEngine;

namespace GuildMaster.Bootstrap
{
    /// <summary>
    /// Запуск игры в сцене: создаёт симуляцию, отдаёт её интерфейсу, копит реальное время и крутит такты.
    /// Скорости, горячие клавиши и автопауза в интерфейсе — ТЗ 03.
    /// </summary>
    public sealed class GameRunner : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private UiRoot ui;

        [Header("Зерно")]
        [Tooltip("Случайное зерно при каждом запуске")]
        [SerializeField] private bool randomSeed = true;
        [SerializeField] private uint seed;

        [Header("Ход времени")]
        [SerializeField] private bool paused;
        [Tooltip("Множитель скорости: 1, 2, 4 (ТЗ 03)")]
        [SerializeField, Min(1)] private int speed = 1;

        [Header("Только чтение")]
        [SerializeField] private string currentTime;

        private float accumulatedSeconds;

        public Simulation Simulation { get; private set; }

        public bool Paused
        {
            get => paused;
            set
            {
                paused = value;
                accumulatedSeconds = 0f;
            }
        }

        public int Speed
        {
            get => speed;
            set => speed = Mathf.Max(1, value);
        }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError($"{nameof(GameRunner)}: GameConfig is not set", this);
                enabled = false;
                return;
            }

            if (randomSeed) seed = unchecked((uint)Guid.NewGuid().GetHashCode());
            Simulation = Simulation.CreateDefault(DataRegistry.FromConfig(config), seed);
            currentTime = Simulation.World.Time.ToString();
            Debug.Log($"[GuildMaster] Simulation started, seed {seed}", this);
        }

        private void Start()
        {
            if (Simulation != null && ui != null) ui.Bind(Simulation);
        }

        private void Update()
        {
            if (Simulation == null) return;

            if (paused)
            {
                Simulation.ApplyCommandsNow();
                return;
            }

            TimeBalance time = Simulation.Data.Balance.Time;
            accumulatedSeconds += UnityEngine.Time.unscaledDeltaTime * speed;

            int ticks = 0;
            while (accumulatedSeconds >= time.RealSecondsPerHour && ticks < time.MaxTicksPerFrame)
            {
                accumulatedSeconds -= time.RealSecondsPerHour;
                Simulation.Tick();
                ticks++;

                if (Simulation.ConsumePauseRequest())
                {
                    Paused = true;
                    break;
                }
            }

            // Кадр подвис — хвост не догоняем, чтобы не было лавины тактов.
            if (ticks == time.MaxTicksPerFrame) accumulatedSeconds = Mathf.Min(accumulatedSeconds, time.RealSecondsPerHour);

            if (ticks > 0) currentTime = Simulation.World.Time.ToString();
        }
    }
}
