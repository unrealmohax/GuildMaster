using System.Collections.Generic;
using GuildMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Верхняя панель (видна всегда): дата и фаза дня, под ними время года и ближайший кризис (клик — окно «Время»), скорость
    /// ⏸ ×1 ×2 ×4, казна (красным при минусе; клик — экран «Казна»), репутация, люди, поселение (цвет по уровню, отсчёт покидания
    /// красным; клик — вкладка «Поселение»),
    /// таймер банкротства (клик — «Казна»; гильдия закрыта — окно итогов), уведомления
    /// со счётчиком.
    /// </summary>
    public sealed class TopBarView : UiView
    {
        private readonly TextMeshProUGUI date;
        private readonly Button pause;
        private readonly List<Button> speeds = new List<Button>();
        private readonly TextMeshProUGUI money;
        private readonly TextMeshProUGUI reputation;
        private readonly TextMeshProUGUI headcount;
        private readonly TextMeshProUGUI settlement;
        private readonly TextMeshProUGUI bankruptcy;
        private readonly Button notificationsButton;
        private readonly TextMeshProUGUI notificationsLabel;
        private readonly NotificationsModel notifications;

        public TopBarView(UiContext context, RectTransform parent, NotificationsModel notifications, System.Action openTime,
            System.Action openNotifications) : base(context)
        {
            this.notifications = notifications;
            Image bar = Factory.Panel(parent, "TopBar", Theme.Panel, blocksClicks: true);
            Root = UiFactory.TopStrip(bar.rectTransform, Theme.TopBarHeight);
            HorizontalLayoutGroup layout = Factory.Horizontal(bar, 18, 0);
            layout.padding = new RectOffset(16, 16, 8, 8);

            Button dateButton = Factory.RowButton(bar.transform, "Date", () => openTime());
            ColorBlock colors = dateButton.colors;
            colors.normalColor = Theme.Panel;
            dateButton.colors = colors;
            date = Factory.Label(dateButton.transform, string.Empty, Theme.FontSize);
            UiFactory.Stretch(date.rectTransform, 6, 0, 6, 0);
            UiFactory.Size(dateButton, 410, Theme.TopBarHeight - 16);

            RectTransform speedGroup = UiFactory.Node(bar.transform, "Speed");
            Factory.Horizontal(speedGroup, 4);
            UiFactory.Size(speedGroup, 240);
            pause = Factory.Button(speedGroup, UiStrings.Pause, () => Context.Clock?.TogglePause(), 52);
            if (Context.Clock != null)
            {
                for (int i = 0; i < Context.Clock.Speeds.Count; i++)
                {
                    int index = i;
                    speeds.Add(Factory.Button(speedGroup, "×" + Context.Clock.Speeds[i], () => Context.Clock.SetSpeed(index), 58));
                }
            }

            money = ClickableStat(bar.transform, "Treasury", 200, () => Context.Navigator.ShowScreen(ScreenId.Treasury));
            reputation = ClickableStat(bar.transform, "Reputation", 160, () => Context.Navigator.ShowScreen(ScreenId.Treasury));
            headcount = Stat(bar.transform, 140);
            settlement = ClickableStat(bar.transform, "Settlement", -1, () => Context.Navigator.Go(Destination.ToGuild(GuildTab.Settlement)));
            bankruptcy = ClickableStat(bar.transform, "Bankruptcy", 260, OnBankruptcy);
            bankruptcy.color = Theme.Danger;

            notificationsButton = Factory.Button(bar.transform, UiStrings.Notifications, () => openNotifications(), 190);
            notificationsLabel = notificationsButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>Надпись-кнопка без фона; ширина −1 — занять остаток панели.</summary>
        private TextMeshProUGUI ClickableStat(Transform parent, string name, float width, System.Action onClick)
        {
            Button button = Factory.RowButton(parent, name, () => onClick());
            ColorBlock colors = button.colors;
            colors.normalColor = Theme.Panel;
            button.colors = colors;
            TextMeshProUGUI label = Factory.Label(button.transform, string.Empty, Theme.FontSize);
            UiFactory.Stretch(label.rectTransform, 4, 0, 4, 0);
            if (width >= 0) UiFactory.Size(button, width, Theme.TopBarHeight - 16);
            else UiFactory.Size(button, 60, Theme.TopBarHeight - 16, flexWidth: 1);
            return label;
        }

        private void OnBankruptcy()
        {
            if (Client.IsFinished) Context.OpenPopup(new Popup(PopupKind.Defeat));
            else Context.Navigator.ShowScreen(ScreenId.Treasury);
        }

        /// <summary>Цвет уровня поселения: спокойно — зелёный, норма — обычный, тревога — охра, осада — красный.</summary>
        public static Color LevelColor(UiTheme theme, SettlementLevel level)
        {
            switch (level)
            {
                case SettlementLevel.Calm: return theme.Good;
                case SettlementLevel.Alarm: return theme.Accent;
                case SettlementLevel.Siege: return theme.Danger;
                default: return theme.Text;
            }
        }

        private TextMeshProUGUI Stat(Transform parent, float width)
        {
            TextMeshProUGUI label = Factory.Label(parent, string.Empty, Theme.FontSize);
            UiFactory.Size(label, width);
            return label;
        }

        public override void Refresh()
        {
            TopBarModel model = TopBarModel.Build(Client, Context.Clock);
            // Вторая строка — время года и ближайший кризис (клик по дате открывает «Время» — там они же в календаре).
            string crisis = string.IsNullOrEmpty(model.Crisis) ? string.Empty : $"  <color={UiTheme.ToHex(Theme.Accent)}>{model.Crisis}</color>";
            date.text = $"{model.Date} · <color={UiTheme.ToHex(Theme.Accent)}>{model.Phase}</color>\n<size={Theme.FontSizeSmall}>{model.Season}{crisis}</size>";

            Factory.SetButtonColors(pause, model.Paused);
            for (int i = 0; i < speeds.Count; i++) Factory.SetButtonColors(speeds[i], !model.Paused && model.SpeedIndex == i);

            string dim = UiTheme.ToHex(Theme.TextDim);
            money.text = $"<color={dim}>{UiStrings.Treasury}</color> {UiFormat.Money(model.Money)}";
            money.color = model.MoneyNegative ? Theme.Danger : Theme.Text;
            reputation.text = $"<color={dim}>{UiStrings.Reputation}</color> {model.Reputation}";
            headcount.text = $"<color={dim}>{UiStrings.Headcount}</color> {model.Headcount}";
            settlement.text = string.IsNullOrEmpty(model.SettlementLeaving)
                ? model.Settlement
                : $"{model.Settlement}  <color={UiTheme.ToHex(Theme.Danger)}>{model.SettlementLeaving}</color>";
            settlement.color = LevelColor(Theme, model.SettlementLevel);

            string extra = model.DebugSpeed ? $"<color={UiTheme.ToHex(Theme.Accent)}>{UiStrings.DebugSpeed}</color>  " : string.Empty;
            bankruptcy.text = extra + model.Bankruptcy;
            bankruptcy.transform.parent.gameObject.SetActive(bankruptcy.text.Length > 0); // пустое место отдаётся поселению

            notifications.Refresh(Client);
            int count = notifications.Items.Count;
            notificationsLabel.text = count > 0 ? $"{UiStrings.Notifications} · {count}" : UiStrings.Notifications;
            Factory.SetButtonColors(notificationsButton, count > 0);
        }
    }
}
