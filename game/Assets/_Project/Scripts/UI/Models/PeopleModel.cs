using System;
using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Фильтр списка людей по занятию.</summary>
    public enum PeopleFilter
    {
        All,
        OnQuest,
        Resting,
        Tavern,
        Training,
        Infirmary,
        Breakdown,
    }

    /// <summary>Столбец сортировки списка людей.</summary>
    public enum PeopleColumn
    {
        Name,
        Archetype,
        Rank,
        Activity,
        Wounds,
        Fatigue,
        Stress,
        Contentment,
        Loyalty,
        Wallet,
    }

    /// <summary>Строка таблицы людей: только видимое игроку.</summary>
    public sealed class PersonRow
    {
        public int Id;
        public string Name;
        public string Archetype;
        public GuildRank Rank;
        public string Activity;
        public PeopleFilter Group;

        /// <summary>Раны: 0 — нет, 1 — лёгкая, 2 — тяжёлая; <see cref="Maimed"/> — отдельно.</summary>
        public int WoundLevel;

        public bool Maimed;
        public string WoundsText;
        public float Fatigue;
        public float Stress;
        public float Contentment;
        /// <summary>Номер слова лояльности (число игроку не показывается — и сортировка идёт по словам).</summary>
        public int LoyaltyLevel;

        public string LoyaltyWord;
        public int Wallet;
    }

    /// <summary>Кандидат в авантюристы: имя, архетип, сколько ещё ждёт.</summary>
    public sealed class CandidateRow
    {
        public int Id;
        public string Name;
        public string Archetype;
        public string Waits;
    }

    /// <summary>Таблица «Люди»: активные авантюристы с фильтром по занятию и сортировкой по столбцу; ниже — кандидаты.</summary>
    public sealed class PeopleModel
    {
        public PeopleFilter Filter { get; set; } = PeopleFilter.All;
        public PeopleColumn SortColumn { get; set; } = PeopleColumn.Name;
        public bool SortDescending { get; set; }

        public List<PersonRow> Rows { get; } = new List<PersonRow>();
        public List<CandidateRow> Candidates { get; } = new List<CandidateRow>();

        /// <summary>Сколько людей в каждой группе занятия (для подписей фильтров).</summary>
        public int[] Counts { get; } = new int[Enum.GetValues(typeof(PeopleFilter)).Length];

        /// <summary>Нажали на заголовок: тот же столбец — сменить направление, другой — по возрастанию.</summary>
        public void SortBy(PeopleColumn column)
        {
            if (SortColumn == column) SortDescending = !SortDescending;
            else
            {
                SortColumn = column;
                SortDescending = false;
            }
        }

        public void Refresh(ISimulationClient client)
        {
            DataRegistry data = client.Data;
            long now = client.World.Time.TotalHours;
            Rows.Clear();
            Array.Clear(Counts, 0, Counts.Length);

            foreach (Adventurer adventurer in client.World.Adventurers.Active)
            {
                PeopleFilter group = PersonText.FilterOf(adventurer, now);
                Counts[(int)PeopleFilter.All]++;
                Counts[(int)group]++;
                if (Filter != PeopleFilter.All && Filter != group) continue;

                AdventurerState state = adventurer.State;
                Rows.Add(new PersonRow
                {
                    Id = adventurer.Id,
                    Name = adventurer.Name,
                    Archetype = PersonText.Archetype(adventurer, data),
                    Rank = adventurer.GuildRank,
                    Activity = PersonText.Activity(adventurer, client),
                    Group = group,
                    WoundLevel = state.HasHeavyWound() ? 2 : state.HasLightWound() ? 1 : 0,
                    Maimed = PersonText.IsMaimed(adventurer, data),
                    WoundsText = PersonText.Wounds(adventurer, data),
                    Fatigue = state.Fatigue,
                    Stress = state.Stress,
                    Contentment = state.Contentment,
                    LoyaltyLevel = StateRules.LoyaltyWordIndex(state.Loyalty, data.Balance.State),
                    LoyaltyWord = PersonText.Loyalty(adventurer, data),
                    Wallet = state.Wallet,
                });
            }
            Rows.Sort(Compare);

            Candidates.Clear();
            foreach (Candidate candidate in client.World.Adventurers.Candidates)
            {
                Candidates.Add(new CandidateRow
                {
                    Id = candidate.Adventurer.Id,
                    Name = candidate.Adventurer.Name,
                    Archetype = PersonText.Archetype(candidate.Adventurer, data),
                    Waits = string.Format(UiStrings.CandidateUntilFormat, UiFormat.Duration(candidate.ExpiresAtHours - now, client.Calendar)),
                });
            }
        }

        private int Compare(PersonRow a, PersonRow b)
        {
            int result;
            switch (SortColumn)
            {
                case PeopleColumn.Archetype: result = string.CompareOrdinal(a.Archetype, b.Archetype); break;
                case PeopleColumn.Rank: result = a.Rank.CompareTo(b.Rank); break;
                case PeopleColumn.Activity: result = string.CompareOrdinal(a.Activity, b.Activity); break;
                case PeopleColumn.Wounds: result = (a.WoundLevel + (a.Maimed ? 3 : 0)).CompareTo(b.WoundLevel + (b.Maimed ? 3 : 0)); break;
                case PeopleColumn.Fatigue: result = a.Fatigue.CompareTo(b.Fatigue); break;
                case PeopleColumn.Stress: result = a.Stress.CompareTo(b.Stress); break;
                case PeopleColumn.Contentment: result = a.Contentment.CompareTo(b.Contentment); break;
                case PeopleColumn.Loyalty: result = a.LoyaltyLevel.CompareTo(b.LoyaltyLevel); break;
                case PeopleColumn.Wallet: result = a.Wallet.CompareTo(b.Wallet); break;
                default: result = string.CompareOrdinal(a.Name, b.Name); break;
            }
            if (result == 0) result = a.Id.CompareTo(b.Id);
            return SortDescending ? -result : result;
        }
    }
}
