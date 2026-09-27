namespace GuildMaster.Core
{
    /// <summary>
    /// Часть мира: гильдия как целое — репутация 0..100. От репутации зависят поток и ранги заказов и приток кандидатов.
    /// Меняется только через <see cref="ReputationService"/>.
    /// </summary>
    public sealed class GuildState
    {
        /// <summary>Репутация на начало игры.</summary>
        public float StartReputation { get; private set; }

        /// <summary>Текущая репутация, 0..<c>maxReputation</c>.</summary>
        public float Reputation { get; internal set; }

        internal void Initialize(float reputation)
        {
            StartReputation = reputation;
            Reputation = reputation;
        }
    }
}
