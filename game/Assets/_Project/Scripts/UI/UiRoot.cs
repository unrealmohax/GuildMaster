using GuildMaster.Core;
using UnityEngine;

namespace GuildMaster.UI
{
    /// <summary>
    /// Корень интерфейса на Canvas. Получает от Bootstrap симуляцию как <see cref="ISimulationClient"/>:
    /// читает мир и отправляет команды, но не меняет мир сам.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public ISimulationClient Client { get; private set; }

        public void Bind(ISimulationClient client)
        {
            Client = client;
        }
    }
}
