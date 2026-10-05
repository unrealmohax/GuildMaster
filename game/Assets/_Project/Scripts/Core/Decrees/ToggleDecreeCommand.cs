using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Игрок включает или выключает распоряжение. Включение: область (ранги — только у распоряжений с областью по рангам;
    /// пустая область — команда ничего не делает) и срок из допустимых. Уже включённое с другой областью или сроком — смена
    /// области и срока, срок — заново. Выключение включённого — отмена игроком (у льготы — недовольство).
    /// Команда, которая ничего не меняет, событий не пишет.
    /// </summary>
    public sealed class ToggleDecreeCommand : ICommand
    {
        public ToggleDecreeCommand(string decreeId, bool enabled, IEnumerable<GuildRank> ranks = null,
            DecreeDuration duration = DecreeDuration.Permanent)
        {
            DecreeId = decreeId;
            Enabled = enabled;
            Ranks = ranks != null ? new List<GuildRank>(ranks) : new List<GuildRank>();
            Duration = duration;
        }

        public string DecreeId { get; }
        public bool Enabled { get; }
        public IReadOnlyList<GuildRank> Ranks { get; }
        public DecreeDuration Duration { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions || !ctx.Data.TryGet(DecreeId, out DecreeDefinition decree)) return;
            bool isActive = ctx.World.Decrees.TryGetActive(decree.Id, out ActiveDecree active);

            if (!Enabled)
            {
                if (isActive) DecreeService.Revoke(ctx, decree, active);
                return;
            }

            if (!Contains(decree.AllowedDurations, Duration)) return;
            List<GuildRank> ranks = DecreeRules.NormalizeRanks(decree, Ranks);
            if (decree.ScopeKind == DecreeScopeKind.Ranks && ranks.Count == 0) return;

            if (!isActive)
            {
                DecreeService.Enable(ctx, decree, ranks, Duration);
                return;
            }
            if (active.Duration == Duration && SameRanks(active.Ranks, ranks)) return;
            DecreeService.Change(ctx, decree, active, ranks, Duration);
        }

        private static bool Contains(IReadOnlyList<DecreeDuration> list, DecreeDuration value)
        {
            foreach (DecreeDuration item in list)
            {
                if (item == value) return true;
            }
            return false;
        }

        private static bool SameRanks(IReadOnlyList<GuildRank> a, List<GuildRank> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
