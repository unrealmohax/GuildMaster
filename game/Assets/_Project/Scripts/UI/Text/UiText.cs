using System;
using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>
    /// Ключи шаблонов строк интерфейса с игровым смыслом (занятие, срыв, фаза задания, рана, стадия постройки). Шаблоны —
    /// в наборе шаблонов ленты; нужен каждый ключ из <see cref="All"/>. Метки: <c>{название}</c> — тип задания, <c>{место}</c>,
    /// <c>{число}</c>.
    /// </summary>
    public static class UiTextKeys
    {
        public const string ActivityResting = "ui.activity.resting";
        public const string ActivitySleeping = "ui.activity.sleeping";
        public const string ActivityTavern = "ui.activity.tavern";
        public const string ActivityTraining = "ui.activity.training";
        public const string ActivityInfirmary = "ui.activity.infirmary";
        public const string ActivityBinge = "ui.activity.binge";
        public const string ActivityQuestTravel = "ui.activity.questTravel";
        public const string ActivityQuestRound = "ui.activity.questRound";
        public const string ActivityQuestCamp = "ui.activity.questCamp";

        public const string BreakdownBrawl = "ui.breakdown.brawl";
        public const string BreakdownRefuseQuests = "ui.breakdown.refuseQuests";
        public const string BreakdownCollapse = "ui.breakdown.collapse";

        public const string PhaseTravelOut = "ui.phase.travelOut";
        public const string PhaseAtSite = "ui.phase.atSite";
        public const string PhaseTravelBack = "ui.phase.travelBack";
        public const string PhaseReturned = "ui.phase.returned";

        public const string WoundLight = "ui.wound.light";
        public const string WoundHeavy = "ui.wound.heavy";

        public const string BuildingReady = "ui.building.ready";
        public const string BuildingConstruction = "ui.building.construction";
        public const string BuildingQueued = "ui.building.queued";
        public const string BuildingNotBuilt = "ui.building.notBuilt";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            ActivityResting, ActivitySleeping, ActivityTavern, ActivityTraining, ActivityInfirmary, ActivityBinge,
            ActivityQuestTravel, ActivityQuestRound, ActivityQuestCamp,
            BreakdownBrawl, BreakdownRefuseQuests, BreakdownCollapse,
            PhaseTravelOut, PhaseAtSite, PhaseTravelBack, PhaseReturned,
            WoundLight, WoundHeavy,
            BuildingReady, BuildingConstruction, BuildingQueued, BuildingNotBuilt,
        };
    }

    /// <summary>
    /// Строка интерфейса по шаблону: первый вариант первого шаблона ключа, подстановка — общим движком текстов.
    /// Случайности нет. Шаблона нет — сам ключ (видно, чего не хватает в данных).
    /// </summary>
    public static class UiText
    {
        public static string Render(DataRegistry data, string key, ITextSource source = null, List<TextSpan> spans = null)
        {
            IReadOnlyList<FeedTemplate> templates = data.HasDefinitions ? data.FeedTemplates(key) : Array.Empty<FeedTemplate>();
            if (templates.Count == 0 || templates[0].Variants.Count == 0) return key;
            return TextRenderer.Render(templates[0].Variants[0], source ?? UiTextSource.Empty, null, spans);
        }
    }

    /// <summary>Значения меток строки интерфейса, заданные словарём.</summary>
    public sealed class UiTextSource : ITextSource
    {
        public static readonly UiTextSource Empty = new UiTextSource();

        private readonly Dictionary<string, TextValue> values = new Dictionary<string, TextValue>(StringComparer.Ordinal);

        public UiTextSource Set(string label, TextValue value)
        {
            if (value != null) values[label] = value;
            return this;
        }

        public UiTextSource Set(string label, NounForms forms) => forms != null ? Set(label, TextValue.Noun(forms)) : this;

        public UiTextSource Set(string label, long number) => Set(label, TextValue.Number(number));

        public bool TryGet(string label, out TextValue value) => values.TryGetValue(label, out value);
    }
}
