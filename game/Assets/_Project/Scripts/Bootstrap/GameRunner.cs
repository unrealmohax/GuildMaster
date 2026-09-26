using System;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace GuildMaster.Bootstrap
{
    /// <summary>
    /// Запуск игры в сцене: создаёт симуляцию, отдаёт её интерфейсу, копит реальное время и крутит такты (ТЗ 03).
    /// Пауза и скорость — <see cref="GameClock"/>; горячие клавиши: Пробел — пауза / продолжить, 1 / 2 / 3 — скорости.
    /// Отладочная скорость — только в редакторе и Development Build (<see cref="Debug.isDebugBuild"/>).
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
        [Tooltip("Начать на паузе")]
        [FormerlySerializedAs("paused")]
        [SerializeField] private bool startPaused;

        [Header("Только чтение")]
        [SerializeField] private string currentTime;
        [SerializeField] private string currentSpeed;

        public Simulation Simulation { get; private set; }
        public GameClock Clock { get; private set; }

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
            Clock = new GameClock(Simulation.Data.Balance.Time, Debug.isDebugBuild, startPaused);
            Clock.Changed += OnClockChanged;
            ShowTime();
            ShowSpeed();
            Debug.Log($"[GuildMaster] Simulation started, seed {seed}, {currentSpeed}", this);
        }

        private void Start()
        {
            if (Simulation != null && ui != null) ui.Bind(Simulation);
        }

        private void OnDestroy()
        {
            if (Clock != null) Clock.Changed -= OnClockChanged;
        }

        private void Update()
        {
            if (Simulation == null) return;

            ReadHotkeys();

            if (Clock.Paused)
            {
                Simulation.ApplyCommandsNow();
                return;
            }

            int ticks = Clock.TakeTicks(UnityEngine.Time.unscaledDeltaTime);
            for (int i = 0; i < ticks; i++)
            {
                Simulation.Tick();
                if (Simulation.ConsumePauseRequest())
                {
                    Clock.Pause();
                    LogAutopause();
                    break;
                }
            }

            if (ticks > 0) ShowTime();
        }

        private void ReadHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame) Clock.TogglePause();
            if (keyboard.digit1Key.wasPressedThisFrame) Clock.SetSpeed(0);
            if (keyboard.digit2Key.wasPressedThisFrame) Clock.SetSpeed(1);
            if (keyboard.digit3Key.wasPressedThisFrame) Clock.SetSpeed(2);
        }

        private void OnClockChanged()
        {
            ShowSpeed();
            Debug.Log($"[GuildMaster] {currentSpeed} at {Simulation.World.Time}", this);
        }

        private void LogAutopause()
        {
            foreach (AutopauseTrigger trigger in Simulation.World.Autopause.Triggers)
            {
                Debug.Log($"[GuildMaster] Autopause ({trigger.Kind}): {trigger.Event.ToLogLine(Simulation.Calendar)}", this);
            }
        }

        private void ShowTime()
        {
            GameTime time = Simulation.World.Time;
            currentTime = $"{time} {Simulation.Rhythm.PhaseAt(time)}";
        }

        private void ShowSpeed()
        {
            currentSpeed = Clock.Paused ? $"Paused (x{Clock.Multiplier})" : $"x{Clock.Multiplier}";
        }
    }
}
