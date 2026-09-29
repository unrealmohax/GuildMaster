using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Задание в списке: тип, место, кто идёт, фаза; у вернувшихся — итог.</summary>
    public sealed class QuestRow
    {
        public int Id;
        public string Type;
        public string Place;
        public string Party;
        public string Phase;
        public bool Finished;

        /// <summary>У вернувшихся: «выполнено» / «не выполнено» (уровень результата скрыт).</summary>
        public string Outcome;

        public bool Success;
    }

    /// <summary>Участник выбранного задания и что с ним.</summary>
    public sealed class QuestMemberRow
    {
        public int Id;
        public string Name;

        /// <summary>0 — без ран, 1 — лёгкая, 2 — тяжёлая.</summary>
        public int WoundLevel;

        /// <summary>Паника, повернул назад, сбежал, погиб — через запятую; пусто — идёт.</summary>
        public string Status;

        public bool Dead;
    }

    /// <summary>
    /// Экран «Задания»: слева — идущие и недавно вернувшиеся (пока хранится их лента), справа — выбранное: заказ, участники,
    /// полоска фазы, проваленные раунды из пяти, лента задания.
    /// </summary>
    public sealed class QuestsModel
    {
        public const int FailureSteps = 5;

        public List<QuestRow> Active { get; } = new List<QuestRow>();
        public List<QuestRow> Recent { get; } = new List<QuestRow>();

        public int SelectedId { get; set; }

        /// <summary>Отладка: показывать шанс раунда и профиль заказа.</summary>
        public bool RevealAll { get; set; }

        public QuestRun Selected { get; private set; }
        public string DetailTitle { get; private set; } = string.Empty;
        public string DetailOrder { get; private set; } = string.Empty;
        public int DetailOrderId { get; private set; }
        public string DetailPhase { get; private set; } = string.Empty;
        public float DetailProgress { get; private set; }
        public int DetailFailedRounds { get; private set; }
        public List<QuestMemberRow> Members { get; } = new List<QuestMemberRow>();
        public string DetailHidden { get; private set; } = string.Empty;

        public void Refresh(ISimulationClient client)
        {
            WorldState world = client.World;
            Active.Clear();
            Recent.Clear();
            foreach (QuestRun run in world.Quests.Active) Active.Add(Row(run, client));
            for (int i = world.Quests.Finished.Count - 1; i >= 0; i--) Recent.Add(Row(world.Quests.Finished[i], client));

            if (!world.Quests.TryGetRun(SelectedId, out QuestRun selected)) selected = null;
            Selected = selected;
            BuildDetail(client);
        }

        public static string PhaseText(QuestRun run, DataRegistry data)
        {
            switch (run.Phase)
            {
                case QuestPhase.TravelOut: return UiText.Render(data, UiTextKeys.PhaseTravelOut);
                case QuestPhase.AtSite: return UiText.Render(data, UiTextKeys.PhaseAtSite, new UiTextSource().Set("число", run.Round));
                case QuestPhase.TravelBack: return UiText.Render(data, UiTextKeys.PhaseTravelBack);
                default: return UiText.Render(data, UiTextKeys.PhaseReturned);
            }
        }

        private static QuestRow Row(QuestRun run, ISimulationClient client)
        {
            bool finished = run.Phase == QuestPhase.Returned;
            return new QuestRow
            {
                Id = run.Id,
                Type = BoardModel.TypeName(client.Data, run.TypeId),
                Place = run.Place?.Nominative ?? string.Empty,
                Party = PartyText(run, client.World),
                Phase = PhaseText(run, client.Data),
                Finished = finished,
                Success = run.IsDone,
                Outcome = finished ? (run.IsDone ? UiStrings.Done : UiStrings.NotDone) : string.Empty,
            };
        }

        /// <summary>«Серые волки», «Ян и Мара», «Ян (один)».</summary>
        public static string PartyText(QuestRun run, WorldState world)
        {
            if (!string.IsNullOrEmpty(run.PartyTitle)) return run.PartyTitle;
            var names = new List<string>();
            foreach (int id in run.Departed)
            {
                if (world.Adventurers.TryGetKnown(id, out Adventurer person)) names.Add(person.Name);
            }
            if (names.Count == 1) return $"{names[0]} ({UiStrings.Solo})";
            return string.Join(", ", names);
        }

        private void BuildDetail(ISimulationClient client)
        {
            Members.Clear();
            DetailHidden = string.Empty;
            QuestRun run = Selected;
            if (run == null)
            {
                DetailTitle = UiStrings.SelectQuest;
                DetailOrder = string.Empty;
                DetailOrderId = 0;
                DetailPhase = string.Empty;
                DetailProgress = 0f;
                DetailFailedRounds = 0;
                return;
            }

            WorldState world = client.World;
            DetailTitle = $"{BoardModel.TypeName(client.Data, run.TypeId)} · {UiFormat.Rank(run.Rank)} · {run.Place?.Nominative}";
            DetailOrderId = run.OrderId;
            DetailOrder = world.Orders.TryGetOrder(run.OrderId, out Order order)
                ? $"{order.Client?.Nominative} · {(order.Distance == OrderDistance.Far ? UiStrings.Far : UiStrings.Near)} · {UiStrings.ColReward.ToLowerInvariant()} {UiFormat.Money(order.Reward + order.Surcharge)}"
                : string.Empty;

            DetailPhase = PhaseText(run, client.Data);
            if (run.Phase == QuestPhase.Returned) DetailPhase += " · " + (run.IsDone ? UiStrings.Done : UiStrings.NotDone);
            DetailProgress = ObserverQueries.PhaseProgress(run);
            DetailFailedRounds = System.Math.Min(FailureSteps, run.FailedRounds);

            foreach (int id in run.Departed)
            {
                if (!world.Adventurers.TryGetKnown(id, out Adventurer person)) continue;
                var statuses = new List<string>();
                if (Contains(run.Panicked, id)) statuses.Add(UiStrings.Panic);
                if (Contains(run.TurnedBack, id)) statuses.Add(UiStrings.TurnedBack);
                if (Contains(run.Fled, id)) statuses.Add(UiStrings.Fled);
                bool dead = Contains(run.Dead, id);
                if (dead) statuses.Add(UiStrings.Died);
                Members.Add(new QuestMemberRow
                {
                    Id = id,
                    Name = person.Name,
                    WoundLevel = dead ? 0 : person.State.HasHeavyWound() ? 2 : person.State.HasLightWound() ? 1 : 0,
                    Status = string.Join(", ", statuses),
                    Dead = dead,
                });
            }

            if (RevealAll && order != null) DetailHidden = BoardModel.Hidden(order, client);
        }

        private static bool Contains(IReadOnlyList<int> ids, int id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return true;
            }
            return false;
        }
    }
}
