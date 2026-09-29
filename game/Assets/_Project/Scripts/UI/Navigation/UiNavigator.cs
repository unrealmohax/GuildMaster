using System;
using System.Collections.Generic;
using GuildMaster.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>Управление игрой, которое не часть мира: перезапуск с зерном и перемотка (для отладки). Реализует запуск игры.</summary>
    public interface IGameSession
    {
        uint Seed { get; }

        /// <summary>Начать заново с этим зерном: новая симуляция, интерфейс привязывается к ней заново.</summary>
        void Restart(uint seed);

        /// <summary>Прокрутить столько часов подряд, сейчас; автопаузу пропускает, останавливается, если гильдия закрылась.</summary>
        void Advance(int hours);
    }

    /// <summary>Всё, что нужно экранам: мир, часы, сессия, фабрика элементов, навигация, режим «Раскрыть всё».</summary>
    public sealed class UiContext
    {
        public UiContext(ISimulationClient client, GameClock clock, IGameSession session, UiFactory factory)
        {
            Client = client;
            Clock = clock;
            Session = session;
            Factory = factory;
        }

        public ISimulationClient Client { get; }
        public GameClock Clock { get; }
        public IGameSession Session { get; }
        public UiFactory Factory { get; }
        public UiTheme Theme => Factory.Theme;
        public UiNavigator Navigator { get; internal set; }

        /// <summary>Отладка: экраны показывают скрытое.</summary>
        public bool RevealAll { get; set; }

        /// <summary>Отладочная сборка: редактор или Development Build.</summary>
        public bool IsDebugBuild => Debug.isDebugBuild;

        public void OnLink(TextLink link) => Navigator.Go(LinkRouter.Resolve(link, Client));

        /// <summary>Уведомления и то, что игрок решил только в интерфейсе (отказ кандидату в персонал).</summary>
        public NotificationsModel Notifications { get; internal set; }

        /// <summary>Открывает окна решений (кандидаты, отчёт, поражение); задаёт корень интерфейса.</summary>
        public Action<Popup> PopupOpener { get; set; }

        public void OpenPopup(Popup popup)
        {
            if (popup.IsValid) PopupOpener?.Invoke(popup);
        }
    }

    /// <summary>Часть интерфейса: корень, показ и скрытие, перерисовка из мира.</summary>
    public abstract class UiView
    {
        protected UiView(UiContext context)
        {
            Context = context;
        }

        protected UiContext Context { get; }
        protected UiFactory Factory => Context.Factory;
        protected UiTheme Theme => Context.Theme;
        protected ISimulationClient Client => Context.Client;

        public RectTransform Root { get; protected set; }

        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        public virtual void Show() => Root.gameObject.SetActive(true);

        public virtual void Hide() => Root.gameObject.SetActive(false);

        /// <summary>Перечитать мир и обновить то, что показано.</summary>
        public abstract void Refresh();
    }

    /// <summary>Экран левой навигации: занимает рабочую область, выбирает объект по переходу.</summary>
    public abstract class ScreenView : UiView
    {
        protected ScreenView(UiContext context) : base(context) { }

        public abstract ScreenId Id { get; }
        public abstract string Title { get; }

        public virtual void Select(Destination destination) { }
    }

    /// <summary>Окно поверх экранов (карточка, время, автопауза…): закрывается Esc или кнопкой.</summary>
    public abstract class WindowView : UiView
    {
        protected WindowView(UiContext context) : base(context) { }

        public virtual void OnClosed() { }
    }

    /// <summary>
    /// Навигация: экраны по id (кнопки слева), окна стопкой поверх (Esc закрывает верхнее), переходы по ссылкам.
    /// Новый экран — <see cref="Register"/>; окна открывает <see cref="Open"/>.
    /// </summary>
    public sealed class UiNavigator
    {
        private readonly UiContext context;
        private readonly Dictionary<ScreenId, ScreenView> screens = new Dictionary<ScreenId, ScreenView>();
        private readonly Dictionary<ScreenId, Button> navButtons = new Dictionary<ScreenId, Button>();
        private readonly List<WindowView> windows = new List<WindowView>();
        private readonly Transform navBar;

        public UiNavigator(UiContext context, Transform navBar)
        {
            this.context = context;
            this.navBar = navBar;
            context.Navigator = this;
        }

        public ScreenView Current { get; private set; }
        public IReadOnlyList<WindowView> Windows => windows;

        /// <summary>Открыть карточку этого человека (задаётся экранами при открытии карточки).</summary>
        public Func<int, WindowView> CardOpener { get; set; }

        /// <summary>Последний открытый в карточке человек и последнее выбранное задание — для отладочных действий.</summary>
        public int LastCardId { get; set; }

        public int LastQuestId { get; set; }

        public event Action Changed;

        public void Register(ScreenView screen)
        {
            screens[screen.Id] = screen;
            screen.Hide();
            Button button = context.Factory.Button(navBar, screen.Title, () => ShowScreen(screen.Id), height: context.Theme.RowHeight + 12,
                fontSize: context.Theme.FontSizeLarge);
            navButtons[screen.Id] = button;
        }

        public void ShowScreen(ScreenId id)
        {
            if (!screens.TryGetValue(id, out ScreenView screen)) return;
            if (Current != screen)
            {
                Current?.Hide();
                Current = screen;
                screen.Show();
            }
            foreach (KeyValuePair<ScreenId, Button> pair in navButtons) context.Factory.SetButtonColors(pair.Value, pair.Key == id);
            screen.Refresh();
            Changed?.Invoke();
        }

        /// <summary>Переход: карточка поверх или экран с выбранным объектом. Недействительный — ничего.</summary>
        public void Go(Destination destination)
        {
            if (!destination.IsValid) return;
            if (destination.IsCard)
            {
                LastCardId = destination.SelectId;
                WindowView card = CardOpener?.Invoke(destination.SelectId);
                if (card != null) Open(card);
                return;
            }

            // Переход на экран закрывает окна, кроме отладочной панели.
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (!(windows[i] is DebugPanel)) Close(windows[i]);
            }
            ShowScreen(destination.Screen);
            Current?.Select(destination);
            Current?.Refresh();
        }

        public void Open(WindowView window)
        {
            windows.Remove(window);
            windows.Add(window);
            window.Root.SetAsLastSibling();
            window.Show();
            window.Refresh();
            Changed?.Invoke();
        }

        public void Close(WindowView window)
        {
            if (!windows.Remove(window)) return;
            window.Hide();
            window.OnClosed();
            Changed?.Invoke();
        }

        public bool IsOpen(WindowView window) => windows.Contains(window);

        public void Toggle(WindowView window)
        {
            if (IsOpen(window)) Close(window);
            else Open(window);
        }

        /// <summary>Esc: закрыть верхнее окно. Закрывать нечего — false.</summary>
        public bool CloseTop()
        {
            if (windows.Count == 0) return false;
            Close(windows[windows.Count - 1]);
            return true;
        }

        /// <summary>Перечитать мир на открытом экране и в открытых окнах.</summary>
        public void RefreshVisible()
        {
            if (Current != null && Current.IsVisible) Current.Refresh();
            for (int i = 0; i < windows.Count; i++) windows[i].Refresh();
        }
    }
}
