using System;

namespace GuildMaster.Core
{
    /// <summary>
    /// Что видит интерфейс: мир на чтение и отправка команд. Менять мир мимо команд нельзя —
    /// сеттеры состояния internal в Core.
    /// </summary>
    public interface ISimulationClient
    {
        WorldState World { get; }
        DataRegistry Data { get; }
        Calendar Calendar { get; }
        DayRhythm Rhythm { get; }

        /// <summary>Игра окончена (гильдия закрыта): время больше не идёт.</summary>
        bool IsFinished { get; }

        void Send(ICommand command);

        /// <summary>Мир изменился: прошёл такт или на паузе применились команды.</summary>
        event Action StateChanged;
    }
}
