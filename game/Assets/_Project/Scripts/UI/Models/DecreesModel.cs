using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Карточка распоряжения: что показано и что выбрано (область и срок).</summary>
    public sealed class DecreeCard
    {
        public string Id;
        public string Name;
        public string Description;
        public string Plus;
        public string Price;
        public bool IsBenefit;
        public bool HasRankScope;
        public IReadOnlyList<DecreeDuration> Durations;

        /// <summary>Включено сейчас (в мире).</summary>
        public bool IsOn;

        /// <summary>Выбранные ранги: у включённого — область в мире (или неприменённая правка), у выключенного — что будет при включении.</summary>
        public readonly List<GuildRank> Ranks = new List<GuildRank>();

        public DecreeDuration Duration;

        /// <summary>«осталось 6 дн. 3 ч», «бессрочно», «выключено».</summary>
        public string Remaining;

        /// <summary>Расходы гильдии по распоряжению в текущем месяце (по журналу, положительное число).</summary>
        public int MonthCost;
    }

    /// <summary>
    /// Экран «Распоряжения»: четыре карточки — название, описание, плюс и цена, включено ли, область по рангам, срок и сколько
    /// осталось. Действия шлют <see cref="ToggleDecreeCommand"/>: переключатель — включить с выбранными областью и сроком или
    /// выключить; ранг и срок у включённого — сразу командой (срок — заново), у выключенного — только выбор. Пока команда не
    /// применена (время идёт), следующая правка строится от отправленной, а не от мира.
    /// </summary>
    public sealed class DecreesModel
    {
        private readonly Dictionary<string, Draft> drafts = new Dictionary<string, Draft>();

        public List<DecreeCard> Cards { get; } = new List<DecreeCard>();

        public void Refresh(ISimulationClient client)
        {
            Cards.Clear();
            if (!client.Data.HasDefinitions) return;

            WorldState world = client.World;
            foreach (DecreeDefinition decree in client.Data.All<DecreeDefinition>())
            {
                bool isOn = world.Decrees.TryGetActive(decree.Id, out ActiveDecree active);
                Draft draft = DraftOf(decree, active, world.Time.TotalHours);
                var card = new DecreeCard
                {
                    Id = decree.Id,
                    Name = decree.DisplayName,
                    Description = decree.Description,
                    Plus = decree.PlusText,
                    Price = decree.MinusText,
                    IsBenefit = decree.IsBenefit,
                    HasRankScope = decree.ScopeKind == DecreeScopeKind.Ranks,
                    Durations = decree.AllowedDurations,
                    IsOn = draft.Enabled ?? isOn,
                    Duration = draft.Duration,
                    Remaining = RemainingText(isOn, active, world.Time.TotalHours, client.Calendar),
                    MonthCost = MonthCost(world, client.Calendar, decree.Id),
                };
                card.Ranks.AddRange(draft.Ranks);
                Cards.Add(card);
            }
        }

        public DecreeCard Find(string id) => Cards.Find(c => c.Id == id);

        /// <summary>Подпись срока: «Бессрочно», «7 дн.», «30 дн.» (дни — из баланса).</summary>
        public static string DurationLabel(DecreeDuration duration, DecreesBalance balance) =>
            duration == DecreeDuration.Permanent
                ? UiStrings.DecreeDurationPermanent
                : string.Format(UiStrings.DecreeDurationDaysFormat, DecreeRules.Days(duration, balance));

        /// <summary>Включить (с выбранными областью и сроком) или выключить.</summary>
        public void Toggle(ISimulationClient client, string id)
        {
            DecreeCard card = Find(id);
            if (card == null) return;
            Draft draft = drafts[id];
            bool enable = !card.IsOn;
            if (enable && card.HasRankScope && draft.Ranks.Count == 0) return;
            draft.Enabled = enable;
            Send(client, id, draft);
        }

        /// <summary>Добавить или убрать ранг из области; последний ранг убрать нельзя.</summary>
        public void ToggleRank(ISimulationClient client, string id, GuildRank rank)
        {
            DecreeCard card = Find(id);
            if (card == null || !card.HasRankScope) return;
            Draft draft = drafts[id];
            if (draft.Ranks.Contains(rank))
            {
                if (draft.Ranks.Count == 1) return;
                draft.Ranks.Remove(rank);
            }
            else
            {
                draft.Ranks.Add(rank);
                draft.Ranks.Sort();
            }
            if (card.IsOn) Send(client, id, draft);
        }

        /// <summary>Выбрать срок; у включённого — сразу, срок отсчитывается заново.</summary>
        public void SetDuration(ISimulationClient client, string id, DecreeDuration duration)
        {
            DecreeCard card = Find(id);
            if (card == null || card.Duration == duration) return;
            Draft draft = drafts[id];
            draft.Duration = duration;
            if (card.IsOn) Send(client, id, draft);
        }

        private void Send(ISimulationClient client, string id, Draft draft)
        {
            draft.SentAtHours = client.World.Time.TotalHours;
            client.Send(draft.Enabled == false
                ? new ToggleDecreeCommand(id, false)
                : new ToggleDecreeCommand(id, true, draft.Ranks, draft.Duration));
            Refresh(client);
        }

        /// <summary>
        /// Черновик карточки. Пока отправленная правка не применена (тот же час), он главнее мира; потом — у включённого
        /// берётся из мира, у выключенного остаётся выбор игрока.
        /// </summary>
        private Draft DraftOf(DecreeDefinition decree, ActiveDecree active, long now)
        {
            if (!drafts.TryGetValue(decree.Id, out Draft draft))
            {
                draft = new Draft { Duration = DecreeDuration.Permanent };
                draft.Ranks.AddRange(decree.DefaultRanks);
                drafts[decree.Id] = draft;
            }
            if (draft.SentAtHours == now) return draft;

            draft.Enabled = null;
            draft.SentAtHours = -1;
            if (active != null)
            {
                draft.Ranks.Clear();
                draft.Ranks.AddRange(active.Ranks);
                draft.Duration = active.Duration;
            }
            return draft;
        }

        private static string RemainingText(bool isOn, ActiveDecree active, long now, Calendar calendar)
        {
            if (!isOn) return UiStrings.DecreeOff;
            if (active.IsPermanent) return UiStrings.DecreePermanent;
            return string.Format(UiStrings.DecreeRemainingFormat, UiFormat.Duration(active.EndsAtHours - now, calendar));
        }

        private static int MonthCost(WorldState world, Calendar calendar, string id)
        {
            GameTime now = world.Time;
            long monthStart = calendar.ToTotalHours(now.Year, now.Month, 1, 0);
            IReadOnlyList<LedgerEntry> ledger = world.Treasury.Ledger;
            int cost = 0;
            for (int i = ledger.Count - 1; i >= 0; i--)
            {
                LedgerEntry entry = ledger[i];
                if (entry.TimeHours < monthStart) break;
                if (entry.Category == LedgerCategories.Decrees && entry.Comment == id) cost -= entry.Amount;
            }
            return cost;
        }

        private sealed class Draft
        {
            public bool? Enabled;
            public readonly List<GuildRank> Ranks = new List<GuildRank>();
            public DecreeDuration Duration;
            public long SentAtHours = -1;
        }
    }
}
