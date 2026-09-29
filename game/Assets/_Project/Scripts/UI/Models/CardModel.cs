using System;
using System.Collections.Generic;
using System.Globalization;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Параметр или показатель: название и число.</summary>
    public readonly struct NamedValue
    {
        public NamedValue(string name, float value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public float Value { get; }
    }

    /// <summary>Отношение с другим человеком: кто и что между ними.</summary>
    public readonly struct RelationRow
    {
        public RelationRow(int id, string name, string label)
        {
            Id = id;
            Name = name;
            Label = label;
        }

        public int Id { get; }
        public string Name { get; }
        public string Label { get; }
    }

    /// <summary>
    /// Карточка авантюриста: только то, что игрок знает. Нераскрытая ось — «?», нераскрытые черты не показываются и не
    /// считаются, лояльность — словами. С <see cref="RevealAll"/> (отладка) — числа осей и лояльности и скрытые черты.
    /// Карточка открывается и у ушедших, погибших и кандидатов.
    /// </summary>
    public sealed class CardModel
    {
        public int Id { get; private set; }
        public bool Found { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsCandidate { get; private set; }
        public bool RevealAll { get; private set; }

        public string Name { get; private set; } = string.Empty;
        public string Subtitle { get; private set; } = string.Empty;
        public string RankLine { get; private set; } = string.Empty;
        public float RankProgress { get; private set; }
        public string Power { get; private set; } = string.Empty;
        public string RoleProfile { get; private set; } = string.Empty;

        public List<NamedValue> Characteristics { get; } = new List<NamedValue>();
        public List<NamedValue> Skills { get; } = new List<NamedValue>();

        /// <summary>Оси: название оси и что о ней известно («?», полюс, «уравновешен»).</summary>
        public List<(string Axis, string Value)> Axes { get; } = new List<(string, string)>();

        public List<string> Traits { get; } = new List<string>();

        public float Fatigue { get; private set; }
        public float Stress { get; private set; }
        public float Contentment { get; private set; }
        public string Loyalty { get; private set; } = string.Empty;

        public string Health { get; private set; } = string.Empty;
        public string Money { get; private set; } = string.Empty;
        public List<RelationRow> Relations { get; } = new List<RelationRow>();
        public string Now { get; private set; } = string.Empty;
        public string History { get; private set; } = string.Empty;

        /// <summary>Последние строки ленты гильдии о человеке, от новых к старым.</summary>
        public List<FeedEntry> LastLines { get; } = new List<FeedEntry>();

        public const int LastLinesShown = 8;

        public static CardModel Build(ISimulationClient client, int id, bool revealAll = false)
        {
            var model = new CardModel { Id = id, RevealAll = revealAll };
            WorldState world = client.World;
            Adventurer person = EventTextSource.FindPerson(world, id);
            if (person == null) return model;

            model.Found = true;
            model.IsActive = world.Adventurers.IsActive(id);
            model.IsCandidate = world.Adventurers.TryGetCandidate(id, out _);
            model.Fill(person, client);
            return model;
        }

        private void Fill(Adventurer person, ISimulationClient client)
        {
            DataRegistry data = client.Data;
            WorldState world = client.World;
            bool female = person.Gender == Gender.Female;

            Name = person.Name;
            Subtitle = $"{(female ? UiStrings.Female : UiStrings.Male)}, {string.Format(UiStrings.AgeFormat, person.Age)} · {PersonText.Archetype(person, data)}";

            RanksBalance ranks = data.Balance.Ranks;
            if (GuildRanks.IsTopRank(person.GuildRank, ranks))
            {
                RankLine = string.Format(UiStrings.RankTop, UiFormat.Rank(person.GuildRank));
                RankProgress = 1f;
            }
            else
            {
                int toNext = GuildRanks.PointsToNext(person, ranks);
                RankLine = string.Format(UiStrings.RankPointsFormat, UiFormat.Rank(person.GuildRank), UiFormat.Number(person.RankPoints), toNext);
                RankProgress = toNext > 0 ? Math.Min(1f, person.RankPoints / toNext) : 1f;
            }
            Power = string.Format(UiStrings.PowerFormat, UiFormat.Number(person.PowerScore));

            FillRoles(person, data);
            FillStats(person, data);
            FillAxes(person, data, female);
            FillTraits(person, data, female);

            AdventurerState state = person.State;
            Fatigue = state.Fatigue;
            Stress = state.Stress;
            Contentment = state.Contentment;
            Loyalty = PersonText.Loyalty(person, data) + (RevealAll ? $" ({UiFormat.Number(state.Loyalty)})" : string.Empty);

            string wounds = PersonText.Wounds(person, data);
            bool maimed = PersonText.IsMaimed(person, data);
            string maimedName = maimed ? TraitName(HealthService.FindMaimedTrait(data), female) : string.Empty;
            Health = wounds.Length == 0 && !maimed ? UiStrings.Healthy : string.Join("; ", NonEmpty(wounds, maimedName));

            Money = $"{string.Format(UiStrings.WalletFormat, UiFormat.Money(state.Wallet))}, "
                    + $"{string.Format(UiStrings.DebtFormat, UiFormat.Money(state.DebtToGuild))}, "
                    + (person.Housing == Housing.Dorm ? UiStrings.HousingDormitory : UiStrings.HousingCity);

            FillRelations(person, world, data);
            Now = NowText(person, client);
            History = string.Format(UiStrings.QuestsDoneFormat, person.QuestsCompleted, person.QuestsFailed);

            IReadOnlyList<FeedEntry> feed = world.Feed.Guild;
            for (int i = feed.Count - 1; i >= 0 && LastLines.Count < LastLinesShown; i--)
            {
                if (Mentions(feed[i], person.Id)) LastLines.Add(feed[i]);
            }
        }

        private void FillRoles(Adventurer person, DataRegistry data)
        {
            RoleScore[] roles = ArchetypeCalculator.Profile(AdventurerStats.PermanentProfile(person, data), data);
            Array.Sort(roles, (a, b) => b.Score.CompareTo(a.Score));
            var parts = new List<string>(roles.Length);
            foreach (RoleScore role in roles) parts.Add($"{role.Role.DisplayName} {UiFormat.Number(role.Score)}");
            RoleProfile = string.Join(", ", parts);
        }

        private void FillStats(Adventurer person, DataRegistry data)
        {
            foreach (StatInfo info in data.Stats.Stats)
            {
                var value = new NamedValue(info.DisplayName, person.GetStat(info.Stat));
                if (info.IsSkill) Skills.Add(value);
                else Characteristics.Add(value);
            }
        }

        private void FillAxes(Adventurer person, DataRegistry data, bool female)
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                var axisId = (AxisId)i;
                AxisDefinition axis = data.Axis(axisId);
                float value = person.GetAxis(axisId);
                string known;
                if (person.IsAxisRevealed(axisId) || RevealAll)
                {
                    if (AxisMath.IsNeutral(value, balance)) known = female ? UiStrings.BalancedFemale : UiStrings.BalancedMale;
                    else
                    {
                        AxisPoleDefinition pole = axis.Pole(AxisMath.PoleOf(value));
                        known = female && !string.IsNullOrEmpty(pole.NameFemale) ? pole.NameFemale : pole.Name;
                    }
                    if (RevealAll) known += $" ({value.ToString("0", CultureInfo.InvariantCulture)}{(person.IsAxisRevealed(axisId) ? string.Empty : ", " + UiStrings.HiddenMark)})";
                }
                else
                {
                    known = UiStrings.Unknown;
                }
                Axes.Add((axis.DisplayName, known));
            }
        }

        private void FillTraits(Adventurer person, DataRegistry data, bool female)
        {
            foreach (TraitInstance trait in person.Traits)
            {
                if (!trait.Revealed && !RevealAll) continue;
                if (!data.TryGet(trait.TraitId, out SpecialTraitDefinition definition)) continue;
                string name = TraitName(definition, female);
                if (trait.AffectedStat.HasValue) name += $" ({data.Stats.Get(trait.AffectedStat.Value).DisplayName.ToLowerInvariant()})";
                if (!trait.Revealed) name += $" — {UiStrings.HiddenMark}";
                Traits.Add(name);
            }
        }

        private void FillRelations(Adventurer person, WorldState world, DataRegistry data)
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            foreach (Relation relation in world.Relations.All)
            {
                if (relation.A != person.Id && relation.B != person.Id) continue;
                int other = relation.GetOther(person.Id);
                if (!world.Adventurers.TryGetKnown(other, out Adventurer partner)) continue;
                RelationLabels labels = RelationService.LabelsOf(relation.Value, relation.JointQuests, balance);
                if ((labels & RelationLabels.Friends) != 0) Relations.Add(new RelationRow(other, partner.Name, UiStrings.Friends));
                if ((labels & RelationLabels.Dislike) != 0) Relations.Add(new RelationRow(other, partner.Name, UiStrings.Dislike));
            }

            foreach (TraitInstance trait in person.Traits)
            {
                if (trait.PartnerId == 0 || !(trait.Revealed || RevealAll)) continue;
                if (!data.TryGet(trait.TraitId, out SpecialTraitDefinition definition)) continue;
                string label = definition.HasHook(TraitHook.RivalPartner) ? UiStrings.Rival
                    : definition.HasHook(TraitHook.LoverPartner) ? UiStrings.Lover
                    : null;
                if (label == null || !world.Adventurers.TryGetKnown(trait.PartnerId, out Adventurer partner)) continue;
                Relations.Add(new RelationRow(partner.Id, partner.Name, label));
            }
        }

        private string NowText(Adventurer person, ISimulationClient client)
        {
            if (IsCandidate) return UiStrings.Candidate;
            if (IsActive) return PersonText.Activity(person, client);

            string state;
            switch (person.LeaveReason)
            {
                case LeaveReason.Died: state = UiStrings.DiedState; break;
                case LeaveReason.Disappeared: state = UiStrings.Disappeared; break;
                case LeaveReason.Expelled: state = UiStrings.Expelled; break;
                default: state = UiStrings.LeftGuild; break;
            }
            string when = person.LeftAtHours > 0 ? $" ({UiFormat.Date(client.Calendar.At(person.LeftAtHours))})" : string.Empty;
            return string.IsNullOrEmpty(person.LeaveReasonText) ? state + when : $"{state}{when}: {person.LeaveReasonText}";
        }

        private static bool Mentions(FeedEntry entry, int id)
        {
            foreach (TextSpan span in entry.Spans)
            {
                if (span.Link.Kind == TextLinkKind.Adventurer && span.Link.Id == id) return true;
            }
            return false;
        }

        private static string TraitName(SpecialTraitDefinition trait, bool female) =>
            trait == null ? string.Empty : female ? trait.DisplayNameFemale : trait.DisplayName;

        private static IEnumerable<string> NonEmpty(params string[] parts)
        {
            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part)) yield return part;
            }
        }
    }
}
