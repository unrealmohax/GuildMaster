namespace GuildMaster.Core
{
    /// <summary>
    /// Выдаёт целочисленные Id сущностям мира. Ссылки между сущностями — только по Id.
    /// </summary>
    public sealed class IdGenerator
    {
        private int next = 1;

        /// <summary>Последний выданный Id (0 — ещё ни одного).</summary>
        public int LastIssued => next - 1;

        internal int Next() => next++;
    }
}
