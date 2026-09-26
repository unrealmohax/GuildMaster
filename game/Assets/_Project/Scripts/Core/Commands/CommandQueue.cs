using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>Очередь команд игрока до ближайшего применения.</summary>
    public sealed class CommandQueue
    {
        private readonly List<ICommand> pending = new List<ICommand>();

        public int Count => pending.Count;

        public void Enqueue(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            pending.Add(command);
        }

        /// <summary>Забрать все команды. Команды, отправленные во время применения, ждут следующего раза.</summary>
        internal ICommand[] TakeAll()
        {
            ICommand[] batch = pending.ToArray();
            pending.Clear();
            return batch;
        }
    }
}
