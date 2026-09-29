using System.Collections.Generic;
using System.Globalization;
using GuildMaster.Core;
using GuildMaster.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Отладочная панель (F1, только в отладочной сборке). Всё, что меняет мир, — отладочные команды Core; зерно, перемотка
    /// и ×50 — через запуск игры; «Раскрыть всё» — режим экранов. Новая функция — ещё один блок в конструкторе.
    /// </summary>
    public sealed class DebugPanel : WindowView
    {
        private static readonly StateStat[] StateStats = { StateStat.Fatigue, StateStat.Stress, StateStat.Contentment, StateStat.Loyalty };
        private static readonly DebugEventKind[] QuestEvents = { DebugEventKind.Ambush, DebugEventKind.Discovery };

        private readonly RectTransform content;
        private readonly TextMeshProUGUI seedLabel;
        private readonly TMP_InputField seedInput;
        private readonly TMP_InputField rewindInput;
        private readonly Button revealButton;
        private readonly TMP_InputField moneyInput;
        private readonly TMP_InputField reputationInput;
        private readonly CycleSelector archetype;
        private readonly TMP_InputField traitsInput;
        private readonly TextMeshProUGUI selectedPerson;
        private readonly CycleSelector stat;
        private readonly TMP_InputField statInput;
        private readonly CycleSelector axis;
        private readonly TMP_InputField axisInput;
        private readonly CycleSelector state;
        private readonly TMP_InputField stateInput;
        private readonly CycleSelector questType;
        private readonly CycleSelector rank;
        private readonly TextMeshProUGUI selectedQuest;

        public DebugPanel(UiContext context, RectTransform parent) : base(context)
        {
            Image panel = Factory.Panel(parent, "Debug", Theme.Background, blocksClicks: true);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(500, -Theme.TopBarHeight);
            Root = rect;

            content = Factory.Scroll(panel.transform, "Content", out ScrollRect scroll, 6);
            UiFactory.Stretch((RectTransform)scroll.transform, 4, 4, 4, 4);
            scroll.GetComponent<Image>().color = Theme.Background;

            Factory.Heading(content, UiStrings.DebugTitle);

            // Зерно
            RectTransform seedRow = Row();
            seedLabel = Factory.Label(seedRow, string.Empty, Theme.FontSizeSmall);
            UiFactory.Size(seedLabel, 170);
            seedInput = Factory.Input(seedRow, UiStrings.Seed, type: TMP_InputField.ContentType.IntegerNumber);
            Factory.Button(seedRow, UiStrings.Restart, Restart, 160, fontSize: Theme.FontSizeSmall);

            // Скорость и перемотка
            RectTransform speedRow = Row();
            Factory.Button(speedRow, UiStrings.DebugSpeed, () => Context.Clock?.SetDebugSpeed(), 80);
            rewindInput = Factory.Input(speedRow, "N", 80, TMP_InputField.ContentType.IntegerNumber);
            rewindInput.text = "24";
            Factory.Button(speedRow, UiStrings.Hours, () => Rewind(1), 110, fontSize: Theme.FontSizeSmall);
            Factory.Button(speedRow, UiStrings.Days, () => Rewind(Client.Calendar.HoursPerDay), 110, fontSize: Theme.FontSizeSmall);

            revealButton = Factory.Button(content, UiStrings.RevealAll, () =>
            {
                Context.RevealAll = !Context.RevealAll;
                Context.Navigator.RefreshVisible();
            });

            // Деньги и репутация
            RectTransform moneyRow = Row(UiStrings.Treasury);
            moneyInput = Factory.Input(moneyRow, "N", 120, TMP_InputField.ContentType.IntegerNumber);
            moneyInput.text = "1000";
            Factory.Button(moneyRow, "+", () => Money(1), 60);
            Factory.Button(moneyRow, "−", () => Money(-1), 60);
            RectTransform reputationRow = Row(UiStrings.Reputation);
            reputationInput = Factory.Input(reputationRow, "0–100", 120, TMP_InputField.ContentType.DecimalNumber);
            Factory.Button(reputationRow, UiStrings.Apply, () =>
            {
                if (TryFloat(reputationInput, out float value)) Client.Send(new DebugSetReputationCommand(value));
            }, 120, fontSize: Theme.FontSizeSmall);

            // Человек
            Factory.Heading(content, UiStrings.Person);
            RectTransform spawnRow = Row();
            archetype = new CycleSelector(Factory, spawnRow, 260);
            archetype.SetValues(Ids<ArchetypeDefinition>(), id => Client.Data.TryGet(id, out ArchetypeDefinition a) ? a.DisplayName : id);
            Factory.Button(spawnRow, UiStrings.Create, SpawnAdventurer, 150, fontSize: Theme.FontSizeSmall);
            traitsInput = Factory.Input(content, UiStrings.TraitsHint);
            selectedPerson = Factory.Label(content, string.Empty, Theme.FontSizeSmall, Theme.TextDim);

            stat = ValueRow(out statInput, Ids(Vocabulary.StatCount, i => ((StatId)i).ToString()),
                id => Client.Data.Stats.Get(ParseEnum<StatId>(id)).DisplayName,
                () => WithPerson(id => new DebugSetStatCommand(id, ParseEnum<StatId>(stat.Value), Float(statInput))));
            axis = ValueRow(out axisInput, Ids(Vocabulary.AxisCount, i => ((AxisId)i).ToString()),
                id => Client.Data.Axis(ParseEnum<AxisId>(id)).DisplayName,
                () => WithPerson(id => new DebugSetAxisCommand(id, ParseEnum<AxisId>(axis.Value), Float(axisInput))));
            var stateNames = new List<string>();
            foreach (StateStat s in StateStats) stateNames.Add(s.ToString());
            state = ValueRow(out stateInput, stateNames, StateName,
                () => WithPerson(id => new DebugSetStateCommand(id, ParseEnum<StateStat>(state.Value), Float(stateInput))));
            Factory.Button(content, UiStrings.Breakdown, () => WithPerson(id => new DebugEventCommand(DebugEventKind.Breakdown, id)),
                fontSize: Theme.FontSizeSmall);

            // Заказ
            Factory.Heading(content, UiStrings.Order);
            RectTransform orderRow = Row();
            questType = new CycleSelector(Factory, orderRow, 210);
            questType.SetValues(Ids<QuestTypeDefinition>(), id => Client.Data.TryGet(id, out QuestTypeDefinition q) ? q.DisplayName : id);
            rank = new CycleSelector(Factory, orderRow, 110);
            var ranks = new List<string>();
            for (int i = 0; i < Vocabulary.RankCount; i++) ranks.Add(((GuildRank)i).ToString());
            rank.SetValues(ranks);
            Factory.Button(orderRow, UiStrings.Create, () =>
                Client.Send(new DebugSpawnOrderCommand(questType.Value, ParseEnum<GuildRank>(rank.Value))), 120, fontSize: Theme.FontSizeSmall);

            // События на задании
            Factory.Heading(content, UiStrings.Event);
            selectedQuest = Factory.Label(content, string.Empty, Theme.FontSizeSmall, Theme.TextDim);
            RectTransform eventRow = Row();
            foreach (DebugEventKind kind in QuestEvents)
            {
                DebugEventKind captured = kind;
                Factory.Button(eventRow, kind == DebugEventKind.Ambush ? UiStrings.Ambush : UiStrings.Discovery, () =>
                {
                    int quest = Context.Navigator.LastQuestId;
                    if (quest != 0) Client.Send(new DebugEventCommand(captured, quest));
                }, 200, fontSize: Theme.FontSizeSmall);
            }

            Root.gameObject.SetActive(false);
        }

        public override void Refresh()
        {
            seedLabel.text = Context.Session != null ? $"{UiStrings.Seed}: {Context.Session.Seed}" : UiStrings.Seed;
            Factory.SetButtonColors(revealButton, Context.RevealAll);

            int person = Context.Navigator.LastCardId;
            selectedPerson.text = person != 0 && Client.World.Adventurers.TryGetActive(person, out Adventurer adventurer)
                ? string.Format(UiStrings.SelectedPersonFormat, adventurer.Name)
                : UiStrings.NothingSelected;
            int quest = Context.Navigator.LastQuestId;
            selectedQuest.text = quest != 0 && Client.World.Quests.TryGetRun(quest, out QuestRun run)
                ? string.Format(UiStrings.SelectedQuestFormat, QuestsModel.PartyText(run, Client.World))
                : UiStrings.NothingSelected;
        }

        // ---------- Действия ----------

        private void Restart()
        {
            if (Context.Session == null) return;
            uint seed = uint.TryParse(seedInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed)
                ? parsed
                : Context.Session.Seed;
            Context.Session.Restart(seed);
        }

        private void Rewind(int hoursPerUnit)
        {
            if (Context.Session == null || !int.TryParse(rewindInput.text, out int count) || count <= 0) return;
            Context.Session.Advance(count * hoursPerUnit);
        }

        private void Money(int sign)
        {
            if (int.TryParse(moneyInput.text, out int amount) && amount > 0) Client.Send(new DebugMoneyCommand(sign * amount));
        }

        private void SpawnAdventurer()
        {
            var traits = new List<string>();
            foreach (string part in traitsInput.text.Split(','))
            {
                string id = part.Trim();
                if (id.Length > 0) traits.Add(id);
            }
            Client.Send(new DebugSpawnAdventurerCommand(archetype.Value, traits));
        }

        private void WithPerson(System.Func<int, ICommand> command)
        {
            int person = Context.Navigator.LastCardId;
            if (person != 0 && Client.World.Adventurers.IsActive(person)) Client.Send(command(person));
        }

        // ---------- Сборка ----------

        private RectTransform Row(string label = null)
        {
            RectTransform row = UiFactory.Node(content, "Row");
            Factory.Horizontal(row, 6);
            UiFactory.Size(row, height: Theme.RowHeight);
            if (label != null) UiFactory.Size(Factory.Label(row, label, Theme.FontSizeSmall, Theme.TextDim), 130);
            return row;
        }

        private CycleSelector ValueRow(out TMP_InputField input, IReadOnlyList<string> values, System.Func<string, string> caption,
            UnityEngine.Events.UnityAction apply)
        {
            RectTransform row = Row();
            var selector = new CycleSelector(Factory, row, 240);
            selector.SetValues(values, caption);
            input = Factory.Input(row, "N", 90, TMP_InputField.ContentType.DecimalNumber);
            Factory.Button(row, UiStrings.Apply, apply, 110, fontSize: Theme.FontSizeSmall);
            return selector;
        }

        private List<string> Ids<T>() where T : Definition
        {
            var ids = new List<string>();
            if (!Client.Data.HasDefinitions) return ids;
            foreach (T definition in Client.Data.All<T>()) ids.Add(definition.Id);
            return ids;
        }

        private static List<string> Ids(int count, System.Func<int, string> name)
        {
            var ids = new List<string>(count);
            for (int i = 0; i < count; i++) ids.Add(name(i));
            return ids;
        }

        private static string StateName(string id)
        {
            switch (ParseEnum<StateStat>(id))
            {
                case StateStat.Fatigue: return UiStrings.ColFatigue;
                case StateStat.Stress: return UiStrings.ColStress;
                case StateStat.Contentment: return UiStrings.ColContentment;
                default: return UiStrings.ColLoyalty;
            }
        }

        private static T ParseEnum<T>(string value) where T : struct => System.Enum.TryParse(value, out T parsed) ? parsed : default;

        private static bool TryFloat(TMP_InputField input, out float value) =>
            float.TryParse(input.text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static float Float(TMP_InputField input) => TryFloat(input, out float value) ? value : 0f;
    }
}
