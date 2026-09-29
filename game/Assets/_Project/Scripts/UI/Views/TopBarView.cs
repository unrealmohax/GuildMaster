using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Верхняя панель (видна всегда): дата и фаза дня (клик — окно «Время»), скорость ⏸ ×1 ×2 ×4, казна (красным при минусе),
    /// репутация, люди, таймер банкротства, уведомления со счётчиком.
    /// </summary>
    public sealed class TopBarView : UiView
    {
        private readonly TextMeshProUGUI date;
        private readonly Button pause;
        private readonly List<Button> speeds = new List<Button>();
        private readonly TextMeshProUGUI money;
        private readonly TextMeshProUGUI reputation;
        private readonly TextMeshProUGUI headcount;
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
            date = Factory.Label(dateButton.transform, string.Empty, Theme.FontSizeLarge);
            UiFactory.Stretch(date.rectTransform, 6, 0, 6, 0);
            UiFactory.Size(dateButton, 470, Theme.TopBarHeight - 16);

            RectTransform speedGroup = UiFactory.Node(bar.transform, "Speed");
            Factory.Horizontal(speedGroup, 4);
            UiFactory.Size(speedGroup, 250);
            pause = Factory.Button(speedGroup, UiStrings.Pause, () => Context.Clock?.TogglePause(), 52);
            if (Context.Clock != null)
            {
                for (int i = 0; i < Context.Clock.Speeds.Count; i++)
                {
                    int index = i;
                    speeds.Add(Factory.Button(speedGroup, "×" + Context.Clock.Speeds[i], () => Context.Clock.SetSpeed(index), 58));
                }
            }

            money = Stat(bar.transform, 230);
            reputation = Stat(bar.transform, 190);
            headcount = Stat(bar.transform, 150);
            bankruptcy = Factory.Label(bar.transform, string.Empty, Theme.FontSize, Theme.Danger);
            UiFactory.Size(bankruptcy, flexWidth: 1);

            notificationsButton = Factory.Button(bar.transform, UiStrings.Notifications, () => openNotifications(), 240);
            notificationsLabel = notificationsButton.GetComponentInChildren<TextMeshProUGUI>();
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
            date.text = $"{model.Date} · <color={UiTheme.ToHex(Theme.Accent)}>{model.Phase}</color>";

            Factory.SetButtonColors(pause, model.Paused);
            for (int i = 0; i < speeds.Count; i++) Factory.SetButtonColors(speeds[i], !model.Paused && model.SpeedIndex == i);

            string dim = UiTheme.ToHex(Theme.TextDim);
            money.text = $"<color={dim}>{UiStrings.Treasury}</color> {UiFormat.Money(model.Money)}";
            money.color = model.MoneyNegative ? Theme.Danger : Theme.Text;
            reputation.text = $"<color={dim}>{UiStrings.Reputation}</color> {model.Reputation}";
            headcount.text = $"<color={dim}>{UiStrings.Headcount}</color> {model.Headcount}";

            string extra = model.DebugSpeed ? $"<color={UiTheme.ToHex(Theme.Accent)}>{UiStrings.DebugSpeed}</color>  " : string.Empty;
            bankruptcy.text = extra + model.Bankruptcy;

            notifications.Refresh(Client);
            int count = notifications.Items.Count;
            notificationsLabel.text = count > 0 ? $"{UiStrings.Notifications} · {count}" : UiStrings.Notifications;
            Factory.SetButtonColors(notificationsButton, count > 0);
        }
    }
}
