using GuildMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Корень интерфейса на Canvas. Получает от запуска игры симуляцию как <see cref="ISimulationClient"/>, часы и сессию:
    /// читает мир и отправляет команды, но не меняет мир сам. Экраны собирает кодом (<see cref="UiFactory"/>) по слоям —
    /// вложенные Canvas: экраны, верхняя панель, окна, отладка.
    /// <para>
    /// Обновление: <see cref="ISimulationClient.StateChanged"/> и смена хода часов только помечают интерфейс устаревшим;
    /// верхняя панель и открытый экран с окнами перерисовываются не чаще, чем задано в <see cref="UiTheme"/>. Скрытые экраны не
    /// обновляются. Клавиши: F1 — отладочная панель (только в отладочной сборке), Esc — закрыть верхнее окно.
    /// </para>
    /// <para>
    /// Окна решений (<see cref="PopupQueue"/>): закрытие гильдии — сразу; новый кандидат и новый отчёт месяца — сами, по одному,
    /// когда поверх экрана нет других окон. Время они не останавливают.
    /// </para>
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        [SerializeField] private UiTheme theme;

        private UiTheme activeTheme;
        private RectTransform built;
        private UiContext context;
        private UiNavigator navigator;
        private TopBarView topBar;
        private CardWindow card;
        private TimeWindow time;
        private AutopauseWindow autopause;
        private NotificationsWindow notificationsWindow;
        private ReportWindow report;
        private CandidateWindow candidate;
        private StaffCandidateWindow staffCandidate;
        private DefeatWindow defeat;
        private DebugPanel debug;
        private NotificationsModel notifications;
        private PopupQueue popups;

        private bool topDirty;
        private bool screenDirty;
        private float nextTopRefresh;
        private float nextScreenRefresh;

        public ISimulationClient Client { get; private set; }
        public GameClock Clock { get; private set; }
        public UiNavigator Navigator => navigator;
        public UiContext Context => context;

        /// <summary>Фокус в поле ввода: горячие клавиши игры не должны срабатывать.</summary>
        public bool WantsKeyboard
        {
            get
            {
                GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                return selected != null && selected.TryGetComponent(out TMP_InputField input) && input.isFocused;
            }
        }

        /// <summary>Привязать интерфейс к симуляции: собрать экраны заново (в том числе после перезапуска игры).</summary>
        public void Bind(ISimulationClient client, GameClock clock = null, IGameSession session = null)
        {
            Unbind();
            Client = client;
            Clock = clock;
            if (client == null) return;

            client.StateChanged += MarkDirty;
            if (clock != null) clock.Changed += MarkTopDirty;
            Build(session);
            navigator.ShowScreen(ScreenId.Guild);
            MarkDirty();
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (Client != null) Client.StateChanged -= MarkDirty;
            if (Clock != null) Clock.Changed -= MarkTopDirty;
            Client = null;
            Clock = null;
            if (built != null) UiFactory.DestroyObject(built.gameObject);
            built = null;
            context = null;
            navigator = null;
        }

        private void MarkDirty()
        {
            topDirty = true;
            screenDirty = true;
        }

        private void MarkTopDirty() => topDirty = true;

        // ---------- Сборка ----------

        private void Build(IGameSession session)
        {
            activeTheme = theme != null ? theme : UiTheme.CreateDefault();
            var factory = new UiFactory(activeTheme);
            context = new UiContext(Client, Clock, session, factory);

            if (TryGetComponent(out CanvasScaler scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            built = UiFactory.Stretch(UiFactory.Node(transform, "Built"));
            RectTransform screensLayer = Layer("Screens", 0);
            RectTransform topLayer = Layer("TopBar", 10);
            RectTransform windowLayer = Layer("Windows", 20);
            RectTransform debugLayer = Layer("Debug", 30);

            Image background = factory.Panel(screensLayer, "Background", activeTheme.Background);
            UiFactory.Stretch(background.rectTransform);

            Image nav = factory.Panel(screensLayer, "Navigation", activeTheme.Panel);
            RectTransform navRect = nav.rectTransform;
            navRect.anchorMin = new Vector2(0, 0);
            navRect.anchorMax = new Vector2(0, 1);
            navRect.pivot = new Vector2(0, 0.5f);
            navRect.offsetMin = new Vector2(0, 0);
            navRect.offsetMax = new Vector2(activeTheme.NavWidth, -activeTheme.TopBarHeight);
            factory.Vertical(nav, 6, activeTheme.Padding);

            RectTransform work = UiFactory.Node(screensLayer, "Work");
            UiFactory.Stretch(work, activeTheme.NavWidth + activeTheme.Padding, activeTheme.Padding, activeTheme.Padding,
                activeTheme.TopBarHeight + activeTheme.Padding);

            navigator = new UiNavigator(context, nav.transform);
            navigator.Register(new GuildScreen(context, work));
            navigator.Register(new BoardScreen(context, work));
            navigator.Register(new QuestsScreen(context, work));
            navigator.Register(new TreasuryScreen(context, work));
            navigator.Changed += MarkDirty;

            notifications = new NotificationsModel();
            popups = new PopupQueue();
            context.Notifications = notifications;
            context.PopupOpener = OpenPopup;
            card = new CardWindow(context, windowLayer);
            time = new TimeWindow(context, windowLayer);
            autopause = new AutopauseWindow(context, windowLayer);
            report = new ReportWindow(context, windowLayer);
            candidate = new CandidateWindow(context, windowLayer);
            staffCandidate = new StaffCandidateWindow(context, windowLayer);
            defeat = new DefeatWindow(context, windowLayer);
            notificationsWindow = new NotificationsWindow(context, windowLayer, notifications);

            // Ссылка на кандидата в авантюристы ведёт в окно решения по нему, на остальных людей — в карточку.
            navigator.CardOpener = id =>
            {
                if (Client.World.Adventurers.TryGetCandidate(id, out _))
                {
                    popups.MarkShown(new Popup(PopupKind.AdventurerCandidate, id), notifications);
                    candidate.CandidateId = id;
                    return candidate;
                }
                card.SetPerson(id);
                return card;
            };

            topBar = new TopBarView(context, topLayer, notifications, () => navigator.Toggle(time), () => navigator.Toggle(notificationsWindow));

            if (context.IsDebugBuild) debug = new DebugPanel(context, debugLayer);
        }

        /// <summary>Отладочная панель собрана (только в отладочной сборке).</summary>
        public bool HasDebugPanel => debug != null;

        /// <summary>Открыть или закрыть окно «Время».</summary>
        public void ToggleTime() => navigator?.Toggle(time);

        /// <summary>Открыть или закрыть список уведомлений.</summary>
        public void ToggleNotifications() => navigator?.Toggle(notificationsWindow);

        /// <summary>Открыть или закрыть отладочную панель; вне отладочной сборки её нет — ничего.</summary>
        public void ToggleDebug()
        {
            if (debug != null) navigator?.Toggle(debug);
        }

        /// <summary>Перерисовать всё сейчас, не дожидаясь следующего кадра (для проверки и снимков).</summary>
        public void RefreshNow()
        {
            if (navigator == null) return;
            topBar.Refresh();
            navigator.RefreshVisible();
            topDirty = screenDirty = false;
        }

        private RectTransform Layer(string name, int order)
        {
            RectTransform layer = UiFactory.Stretch(UiFactory.Node(built, name));
            var canvas = layer.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            layer.gameObject.AddComponent<GraphicRaycaster>();
            return layer;
        }

        /// <summary>Открыть окно решения: кандидат, кандидат в персонал, отчёт месяца, поражение.</summary>
        public void OpenPopup(Popup popup)
        {
            if (navigator == null || !popup.IsValid) return;
            popups.MarkShown(popup, notifications);
            switch (popup.Kind)
            {
                case PopupKind.AdventurerCandidate:
                    candidate.CandidateId = popup.Id;
                    navigator.Open(candidate);
                    break;
                case PopupKind.StaffCandidate:
                    staffCandidate.CandidateId = popup.Id;
                    navigator.Open(staffCandidate);
                    break;
                case PopupKind.Report:
                    report.ReportIndex = popup.Id;
                    navigator.Open(report);
                    break;
                case PopupKind.Defeat:
                    navigator.Open(defeat);
                    break;
            }
        }

        /// <summary>
        /// Открыть окно решения, которое ждёт своей очереди: закрытие гильдии — поверх всего; остальное — только если поверх
        /// экрана нет окон (отладочная панель не мешает). Открыто — true.
        /// </summary>
        public bool ShowPendingPopup()
        {
            if (Client == null || navigator == null) return false;
            if (popups.TryUrgent(Client, out Popup urgent))
            {
                OpenPopup(urgent);
                return true;
            }
            foreach (WindowView window in navigator.Windows)
            {
                if (!(window is DebugPanel)) return false;
            }
            if (!popups.TryNext(Client, notifications, out Popup popup)) return false;
            OpenPopup(popup);
            return true;
        }

        // ---------- Кадр ----------

        private void Update()
        {
            if (Client == null || navigator == null) return;

            ReadKeys();
            CheckAutopause();
            ShowPendingPopup();

            float now = Time.unscaledTime;
            if (topDirty && now >= nextTopRefresh)
            {
                topDirty = false;
                nextTopRefresh = now + 1f / activeTheme.TopBarRefreshPerSecond;
                topBar.Refresh();
            }
            if (screenDirty && now >= nextScreenRefresh)
            {
                screenDirty = false;
                nextScreenRefresh = now + 1f / activeTheme.ScreenRefreshPerSecond;
                navigator.RefreshVisible();
            }
        }

        /// <summary>Встали часы из-за автопаузы (новые причины в мире) — открыть окно автопаузы.</summary>
        private void CheckAutopause()
        {
            if (Clock == null || !Clock.Paused) return;
            var triggers = Client.World.Autopause.Triggers;
            if (triggers.Count == 0 || triggers[0].Event == autopause.ShownFor) return;
            autopause.ShowFor(triggers[0].Event);
            navigator.Open(autopause);
        }

        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || WantsKeyboard) return;

            if (keyboard.f1Key.wasPressedThisFrame) ToggleDebug();
            if (keyboard.escapeKey.wasPressedThisFrame) navigator.CloseTop();
        }
    }
}
