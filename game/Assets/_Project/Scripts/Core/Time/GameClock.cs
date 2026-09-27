using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Ход игры в реальном времени: пауза, скорость, накопление секунд.
    /// Не часть мира и на результат симуляции не влияет — решает только, сколько тактов сделать за кадр.
    /// Реальное время сам не читает: секунды кадра передаёт <c>GameRunner</c>. Числа — из <see cref="TimeBalance"/>.
    /// </summary>
    public sealed class GameClock
    {
        private readonly TimeBalance time;
        private float accumulatedSeconds;

        /// <param name="debugSpeedAllowed">Отладочная сборка (редактор или Development Build): доступна скорость ×50.</param>
        public GameClock(TimeBalance time, bool debugSpeedAllowed, bool startPaused = false)
        {
            this.time = time ?? throw new ArgumentNullException(nameof(time));
            if (time.SpeedMultipliers.Count == 0) throw new ArgumentException("No game speeds", nameof(time));

            DebugSpeedAllowed = debugSpeedAllowed;
            Paused = startPaused;
        }

        public bool DebugSpeedAllowed { get; }
        public bool Paused { get; private set; }

        /// <summary>Номер обычной скорости в <see cref="TimeBalance.SpeedMultipliers"/> (0 — ×1).</summary>
        public int SpeedIndex { get; private set; }

        /// <summary>Включена отладочная скорость (<see cref="TimeBalance.DebugSpeedMultiplier"/>).</summary>
        public bool DebugSpeed { get; private set; }

        public IReadOnlyList<int> Speeds => time.SpeedMultipliers;

        public int Multiplier => DebugSpeed
            ? time.DebugSpeedMultiplier
            : time.SpeedMultipliers[Math.Min(SpeedIndex, time.SpeedMultipliers.Count - 1)];

        /// <summary>Пауза или скорость изменились.</summary>
        public event Action Changed;

        public void Pause() => SetPaused(true);

        public void Resume() => SetPaused(false);

        public void TogglePause() => SetPaused(!Paused);

        /// <summary>Выбрать обычную скорость по номеру (0 — первая) и снять паузу. Нет такой — false.</summary>
        public bool SetSpeed(int index)
        {
            if (index < 0 || index >= time.SpeedMultipliers.Count) return false;

            SpeedIndex = index;
            DebugSpeed = false;
            accumulatedSeconds = 0f;
            Paused = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Включить отладочную скорость и снять паузу. Вне отладочной сборки — false, ничего не меняется.</summary>
        public bool SetDebugSpeed()
        {
            if (!DebugSpeedAllowed) return false;

            DebugSpeed = true;
            accumulatedSeconds = 0f;
            Paused = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Прошло <paramref name="realSeconds"/> реального времени: сколько тактов сделать. На паузе — 0.
        /// Не больше <see cref="TimeBalance.MaxTicksPerFrame"/>; хвост подвисшего кадра не догоняется.
        /// </summary>
        public int TakeTicks(float realSeconds)
        {
            if (Paused || realSeconds <= 0f) return 0;

            float secondsPerHour = time.RealSecondsPerHour;
            accumulatedSeconds += realSeconds * Multiplier;

            int ticks = 0;
            while (accumulatedSeconds >= secondsPerHour && ticks < time.MaxTicksPerFrame)
            {
                accumulatedSeconds -= secondsPerHour;
                ticks++;
            }

            if (ticks == time.MaxTicksPerFrame) accumulatedSeconds = Math.Min(accumulatedSeconds, secondsPerHour);
            return ticks;
        }

        private void SetPaused(bool paused)
        {
            if (Paused == paused) return;

            Paused = paused;
            accumulatedSeconds = 0f;
            Changed?.Invoke();
        }
    }
}
