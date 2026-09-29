using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Человек словами: архетип в его роде, занятие, раны, лояльность. Только видимое игроку.</summary>
    public static class PersonText
    {
        public static string Archetype(Adventurer adventurer, DataRegistry data)
        {
            if (!data.TryGet(adventurer.ArchetypeId, out ArchetypeDefinition archetype)) return adventurer.ArchetypeId;
            return adventurer.Gender == Gender.Female ? archetype.DisplayNameFemale : archetype.DisplayName;
        }

        public static string Loyalty(Adventurer adventurer, DataRegistry data) =>
            StateRules.LoyaltyWord(adventurer.State.Loyalty, adventurer.Gender, data.Balance.State);

        /// <summary>Срыв, который идёт сейчас (кроме запоя — он виден как занятие); нет — <c>null</c>.</summary>
        public static string Breakdown(Adventurer adventurer, ISimulationClient client)
        {
            AdventurerState state = adventurer.State;
            if (state.Breakdown == BreakdownKind.None || state.BreakdownEndsAtHours <= client.World.Time.TotalHours) return null;
            switch (state.Breakdown)
            {
                case BreakdownKind.Brawl: return UiText.Render(client.Data, UiTextKeys.BreakdownBrawl);
                case BreakdownKind.RefuseQuests: return UiText.Render(client.Data, UiTextKeys.BreakdownRefuseQuests);
                case BreakdownKind.Collapse: return UiText.Render(client.Data, UiTextKeys.BreakdownCollapse);
                default: return null;
            }
        }

        /// <summary>Занятие сейчас: «отдыхает», «в таверне», «на задании: охота, Старая мельница»; идёт срыв — он же через запятую.</summary>
        public static string Activity(Adventurer adventurer, ISimulationClient client)
        {
            string text = ActivityOnly(adventurer, client);
            string breakdown = Breakdown(adventurer, client);
            return breakdown != null ? $"{text}, {breakdown}" : text;
        }

        private static string ActivityOnly(Adventurer adventurer, ISimulationClient client)
        {
            DataRegistry data = client.Data;
            switch (adventurer.State.Activity)
            {
                case Core.Activity.Sleeping: return UiText.Render(data, UiTextKeys.ActivitySleeping);
                case Core.Activity.Tavern: return UiText.Render(data, UiTextKeys.ActivityTavern);
                case Core.Activity.Training: return UiText.Render(data, UiTextKeys.ActivityTraining);
                case Core.Activity.Infirmary: return UiText.Render(data, UiTextKeys.ActivityInfirmary);
                case Core.Activity.Binge: return UiText.Render(data, UiTextKeys.ActivityBinge);
                case Core.Activity.OnQuestTravel: return QuestActivity(adventurer, client, UiTextKeys.ActivityQuestTravel);
                case Core.Activity.OnQuestRound: return QuestActivity(adventurer, client, UiTextKeys.ActivityQuestRound);
                case Core.Activity.OnQuestCamp: return QuestActivity(adventurer, client, UiTextKeys.ActivityQuestCamp);
                default: return UiText.Render(data, UiTextKeys.ActivityResting);
            }
        }

        private static string QuestActivity(Adventurer adventurer, ISimulationClient client, string key)
        {
            var source = new UiTextSource();
            if (client.World.Quests.TryGetRun(adventurer.State.QuestRunId, out QuestRun run))
            {
                source.Set("место", run.Place);
                if (client.Data.TryGet(run.TypeId, out QuestTypeDefinition type)) source.Set("название", TextValue.Word(type.DisplayName.ToLowerInvariant()));
            }
            return UiText.Render(client.Data, key, source);
        }

        /// <summary>Группа занятия для фильтра списка людей.</summary>
        public static PeopleFilter FilterOf(Adventurer adventurer, long nowHours)
        {
            AdventurerState state = adventurer.State;
            if (state.IsOnQuest()) return PeopleFilter.OnQuest;
            if (state.Breakdown != BreakdownKind.None && state.BreakdownEndsAtHours > nowHours || state.Activity == Core.Activity.Binge)
                return PeopleFilter.Breakdown;
            switch (state.Activity)
            {
                case Core.Activity.Tavern: return PeopleFilter.Tavern;
                case Core.Activity.Training: return PeopleFilter.Training;
                case Core.Activity.Infirmary: return PeopleFilter.Infirmary;
                default: return PeopleFilter.Resting;
            }
        }

        /// <summary>Раны строкой: «тяжёлая рана — 9 дн.; лёгкая рана — 2 дн.»; пусто — ран нет.</summary>
        public static string Wounds(Adventurer adventurer, DataRegistry data)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (Condition condition in adventurer.State.Conditions)
            {
                int days = (int)System.Math.Ceiling(System.Math.Max(0f, condition.RemainingDays));
                string key = condition.Kind == ConditionKind.HeavyWound ? UiTextKeys.WoundHeavy : UiTextKeys.WoundLight;
                parts.Add(UiText.Render(data, key, new UiTextSource().Set("число", days)));
            }
            return string.Join("; ", parts);
        }

        /// <summary>Калека — постоянная черта увечья; видна, если раскрыта (увечье раскрывается сразу).</summary>
        public static bool IsMaimed(Adventurer adventurer, DataRegistry data)
        {
            SpecialTraitDefinition maimed = HealthService.FindMaimedTrait(data);
            return maimed != null && adventurer.TryGetTrait(maimed.Id, out TraitInstance trait) && trait.Revealed;
        }
    }
}
