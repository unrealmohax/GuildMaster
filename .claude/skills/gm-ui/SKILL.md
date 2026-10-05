---
name: gm-ui
description: Интерфейс GuildMaster (uGUI + TMP, сборка GuildMaster.UI) — UiRoot (слои Canvas, частота перерисовки, F1/Esc, Bind(клиент, часы, сессия)), UiTheme (цвета, шрифт, размеры, частоты), UiFactory и виджеты (TableView, FeedView, BarView, LinkClickHandler, CycleSelector), навигация UiNavigator/Destination/LinkRouter, модели экранов (TopBarModel, PeopleModel, BoardModel, RegistrarModel, QuestsModel, CardModel, CalendarModel, NotificationsModel, AutopauseModel, TreasuryModel, DefeatModel) и действия игрока (BoardActions, BuildingActions, StaffModel.Offer/Dismiss, PeopleActions), экраны Гильдия/Доска/Задания/Казна, окна (карточка, время, автопауза, уведомления, отчёт, кандидат, кандидат в персонал, поражение) и очередь окон решений PopupQueue, отладочная панель и отладочные команды Core, ссылки в строках ленты (TextLink, TextSpan, FeedEntry.Spans), строки ui.*, IGameSession в GameRunner, зонд UiProbe. Использовать при правке или добавлении экрана, окна, столбца, уведомления, вида календаря, отладочной функции, при проблемах раскладки и ссылок.
---

# Интерфейс

Код — `game/Assets/_Project/Scripts/UI/` (сборка `GuildMaster.UI`: Core, Data, UnityEngine.UI, Unity.TextMeshPro, Unity.InputSystem).
Ассеты — `Data/UI/` (`UiTheme.asset`, `LiberationSans Cyrillic SDF.asset`). TMP Essential Resources — `Assets/TextMesh Pro/`.
Дизайн — `TechJob/14-ui-observation.md` (наблюдение), `TechJob/15-ui-decisions.md` (решения игрока); решения — `docs/decisions.md`
(2026-09-28 GM-14, 2026-09-29 GM-15). Проверяется только в 1920×1080 (прототип).

## Главное правило

Интерфейс читает мир через `ISimulationClient`, меняет — только командами (`Client.Send`); действие игрока — метод модели/действий,
который шлёт существующую команду (на паузе `GameRunner` применяет её в тот же кадр — `Simulation.ApplyCommandsNow`). Никаких `SimContext`,
`ctx.Rng`, internal-сеттеров. Для интерфейса в Core — только запросы без мутаций (`ObserverQueries`), скрытое — `DebugQueries` и только
при `Context.RevealAll`. Профиль заказа, шанс раунда, скрытые черты, число скрытых черт, число лояльности — нигде, кроме «Раскрыть всё».

## Карта

| Файл | Что |
|---|---|
| `UiRoot.cs` | MonoBehaviour на Canvas сцены. `Bind(клиент, часы, сессия)` — собирает всё заново (и после перезапуска). Слои — вложенные Canvas: Screens 0, TopBar 10, Windows 20, Debug 30 (у каждого `GraphicRaycaster`). `Update`: F1/Esc, `CheckAutopause`, перерисовка по флагам. `WantsKeyboard` — фокус в поле ввода (GameRunner глушит горячие клавиши). `RefreshNow`, `ToggleTime/Notifications/Debug`, `HasDebugPanel` — для тестов и зонда |
| `Theme/UiTheme.cs` | ScriptableObject: шрифт, палитра, размеры при 1920×1080, `topBarRefreshPerSecond` 10, `screenRefreshPerSecond` 4, `feedLinesShown` 150. Нет ассета — `CreateDefault()` |
| `Text/UiStrings.cs` | подписи (вкладки, столбцы, кнопки, форматы), названия видов автопаузы |
| `Text/UiText.cs` | `UiTextKeys` (ключи шаблонов `ui.*`, список `All` проверяет валидатор), `UiText.Render` (первый вариант, без случайности), `UiTextSource` |
| `Text/UiFormat.cs` | дата, метка времени, фаза дня, деньги с пробелом тысяч, срок словами по календарю, дни до срока |
| `Text/FeedFormatter.cs` | `LinkCodec` (`<link="Adventurer:12">`), `FeedFormatter.WithLinks` (разметка по `Spans`, экранирование `<>`), `Format` — важность [О]/[З]/[В] |
| `Navigation/Destination.cs` | `ScreenId`, `GuildTab`, `Destination` (карточка или экран+вкладка+выбор), `LinkRouter.Resolve(TextLink)` |
| `Navigation/UiNavigator.cs` | `IGameSession`, `UiContext` (клиент, часы, сессия, фабрика, навигатор, `RevealAll`), базовые `UiView`/`ScreenView`/`WindowView`, `UiNavigator` (Register, ShowScreen, Go, Open/Close/Toggle, CloseTop, RefreshVisible, LastCardId, LastQuestId) |
| `Models/*.cs` | модели экранов — обычный C#, их тестируют `UiTests` и `UiDecisionTests` |
| `Models/BoardModel.cs` | + «Ждут решения» (`Awaiting`), доплата выбранного; `RegistrarModel` (типы, ранг, награда, комиссия; неприменённая правка — `pending`, следующая строится от неё); `BoardActions` (доплата, ответы на важный заказ и событийное задание) |
| `Models/GuildTabsModel.cs` | + `BuildingRow` (цена, срок, `CanOrder`, `Queued`, `RowId` — отрицательный у не начатых), `BuildingActions` (заказ, `Move`/`MoveTo` очереди, снять), `StaffModel.Vacancies/Offer/Dismiss` |
| `Models/NotificationsModel.cs` | + `Popup`/`PopupKind` (окно решения в уведомлении), `RejectedStaffCandidates` (отказ кандидату в персонал — только в интерфейсе), `PopupQueue` (что открыть само) |
| `Models/TreasuryModel.cs` | `TreasuryModel` (казна, банкротство словами, журнал месяца с фильтром статей, `Describe` — к чему относится запись), `DefeatModel` (итоги, `NewSeed`) |
| `Views/UiFactory.cs` | детали: Node/Stretch/TopStrip/Centered/Size, Panel, Vertical/Horizontal, Label/Heading/LinkLabel, Button/RowButton + цвета, Scroll, Slider, Input, `DestroyObject` |
| `Views/Widgets.cs` | `LinkClickHandler`, `BarView`, `TableView` (заголовки с сортировкой, пул строк), `FeedView` (новые сверху, дописывает по последней строке), `CycleSelector` (`Changed`, `SetIndex`, `Root`), `SliderReleaseHandler` (команда — когда отпустили), `DragReorder` (перетаскивание строки среди соседей) |
| `Views/TopBarView.cs`, `GuildScreen.cs`, `BoardScreen.cs`, `QuestsScreen.cs`, `TreasuryScreen.cs`, `Windows.cs`, `DebugPanel.cs` | виды; в `Windows.cs` — `CandidateWindow`, `StaffCandidateWindow`, `DefeatWindow`, общий текст карточки `CardWindow.StatsText` и отчёта `ReportWindow.Render` |
| `Core/DebugTools/DebugCommands.cs` | `DebugMoneyCommand`, `DebugSetReputationCommand`, `DebugSpawnAdventurerCommand`, `DebugSetStat/Axis/StateCommand`, `DebugSpawnOrderCommand`, `DebugEventCommand` (засада, находка, срыв); событие `DebugAction` |
| `Core/DebugTools/ObserverQueries.cs` | `ObserverQueries` (возвращение на обратном пути, доля фазы), `DebugQueries` (профиль заказа, шанс раунда) |
| `Core/Feed/TextLink.cs` | `TextLinkKind`, `TextLink`, `TextSpan` |
| `Bootstrap/GameRunner.cs` | реализует `IGameSession`: `Restart(seed)` (на паузе, `ui.Bind` заново), `Advance(hours)` (такты подряд, автопауза — в лог) |

## Ход обновления

`StateChanged` (такт или команды на паузе) и `GameClock.Changed` → флаги «устарело». В `Update`: верхняя панель — не чаще 10 раз/с,
открытый экран и открытые окна — не чаще 4 раз/с (`navigator.RefreshVisible`). Скрытые экраны не трогаются. На ×50 за кадр идёт до
`MaxTicksPerFrame` тактов — перерисовка одна. Таблицы переиспользуют строки, TMP не перестраивает одинаковый текст.

Автопауза: `CheckAutopause` — часы на паузе и первая причина в `World.Autopause.Triggers` не та, что у открытого окна → открыть окно.
Окно помнит строки (следующий такт очистит причины в мире).

## Ссылки

`EventTextSource` даёт значениям меток `TextLink` (см. таблицу в его комментарии); `TextRenderer.Render(…, spans)` пишет места
подставленных значений со ссылкой; `FeedSystem` кладёт их в `FeedEntry.Spans`. Интерфейс: `FeedFormatter.WithLinks` → `<link>`,
`LinkClickHandler` → `LinkCodec.TryDecode` → `Context.OnLink` → `LinkRouter.Resolve` → `Navigator.Go`. Id людей, сотрудников, заказов,
заданий, построек, групп — разные счётчики, поэтому у ссылки всегда вид.

## Рецепты

**Новый экран.** Значение в `ScreenId`, класс `: ScreenView` (Root — `Stretch` в рабочей области, `Refresh` читает модель), модель в
`Models/` (+ тест), `navigator.Register(new …(context, work))` в `UiRoot.Build`. Переходы к нему — ветка в `LinkRouter`.

**Новое окно.** `: ModalWindow` (или `WindowView`), создать в `UiRoot.Build` на слое Windows, открыть `navigator.Open`/`Toggle`.

**Новый столбец таблицы.** Поле в строке модели, `TableColumn` (ширина; `Bar` — полоска; `OnHeader` — сортировка), заполнить в `Refresh`.
Бюджет ширины «Людей» при 1920 — ~1200 px (навигация 200, лента 440).

**Новый вид уведомлений / календаря.** Класс `INotificationSource` / `ICalendarSource` и строка в `Sources`. Уведомление — по состоянию
мира, пропадает само.

**Новая строка с игровым смыслом.** Ключ в `UiTextKeys` (+ `All`), шаблон в `GameDataGenerator.Feed.cs`, генератор, Validate Data.

**Новая отладочная функция.** Меняет мир — команда в `Core/DebugTools` (службы Core, случайность — поток `CommandSystem`, событие
`DebugAction`) + тест; не меняет — через `IGameSession` или флаг `UiContext`. Кнопка — блок в конструкторе `DebugPanel`.

## Проверка

- `UiTests` (EditMode): ссылки и их места, маршруты, скрытое/«Раскрыть всё», обновление моделей, календарь, уведомления, причина ухода,
  отладочные команды и их детерминизм, сборка `UiRoot` в EditMode (все экраны, окна, панель только при `Debug.isDebugBuild`).
- `UiDecisionTests` (EditMode): действие → команда → на паузе видно в модели (доплата, «Ждут решения», Регистратор и комиссия, стройка
  и очередь, найм и увольнение), окна решений и уведомления, журнал и отчёты, поражение и перезапуск, детерминизм действий.
- Play Mode — зонд `ClaudeSandbox/Editor/UiProbe.cs` (меню GuildMaster → Sandbox → UI): `Advance 45 Days`, `Capture All 1920x1080`
  (с казной, окнами кандидатов, отчётом и итогами поражения — предпросмотр), `Capture Current …`, `Measure Frames 10s` (компонент `ClaudeSandbox/FrameMeter.cs`), `Log Screen State`, `Restore Game View Size`.
  Без фокуса окна включить Time → Toggle Run In Background и выключить до выхода из Play Mode. Снимки — `Logs/UiShots/`, после просмотра
  удалить. Время игры вперёд — Time → Debug Speed (снимает паузу после автопаузы).

## Окна решений

`PopupQueue` в `UiRoot.ShowPendingPopup` (каждый кадр): гильдия закрыта — окно поражения сразу; иначе, только если поверх экрана нет окон
(кроме отладочной панели), — первый не показанный кандидат, кандидат в персонал (не отказанный), непрочитанный отчёт. Открытое окно
(само, по уведомлению или по ссылке) отмечается и само больше не приходит. Ссылка на кандидата (`CardOpener`) ведёт в окно кандидата.
Открыть окно из экрана — `Context.OpenPopup(new Popup(…))`.

## Ловушки

- **Раскладка.** `HorizontalLayoutGroup` с `childForceExpandHeight = true` сообщает гибкую высоту 1 и забирает высоту у соседей —
  в фабрике по умолчанию `false`; кнопкам и ячейкам без текста-ребёнка в раскладке задавать высоту (`Size(…, height)`), иначе схлопнутся.
  `ContentSizeFitter` — только на корне содержимого прокрутки; у детей раскладок он ломает высоту (строки наезжают).
- **Кнопки** — `fadeDuration = 0`: иначе строки, созданные в кадре, видны светлыми, пока идёт переход цвета.
- `GetComponent<T>() ?? …` у Unity-объектов не работает — `TryGetComponent`.
- Удаление объектов интерфейса — `UiFactory.DestroyObject` (в EditMode тестах `Destroy` запрещён).
- `Calendar` конфликтует с `System.Globalization.Calendar` — алиас `using Calendar = GuildMaster.Core.Calendar;`.
- Новые глифы (значки) — только те, что есть в шрифтовом ассете: список символов в `ClaudeSandbox/Editor/UiSetup.cs`
  (`Build Font And Theme` пересобирает шрифт только если его нет — удалить ассет перед пересборкой).
- Правка скрипта в Play Mode — перекомпиляция посреди игры: выйти из Play Mode (сначала выключить Run In Background).
- Экзамен висит на доске без срока (`ExpiresAtHours = long.MaxValue`) — «без срока».
- **Две правки подряд, пока время идёт** (команды применятся в начале такта): новая правка должна строиться от отправленной, а не от
  мира — как `RegistrarModel.pending`; иначе вторая затрёт первую.
- Поле ввода не перезаписывать из мира, пока в нём фокус (`isFocused`), и заполнять заново только при смене объекта.
- Снимки зонда без фокуса окна Unity иногда теряются (записывается только последний) — повторить `Capture All`.
- Экран «Распоряжения» (`ScreenId.Decrees`, `DecreesModel` с черновиком, как `RegistrarModel.pending`) и календарь — скилл
  `gm-decrees`. Кнопки навигации — автоподбор размера шрифта (`FontSize`…`FontSizeLarge`): длинное название не обрезается.
