# Код игры: архитектура

Правила работы — в корневом `CLAUDE.md`. Здесь — общее устройство кода игры; подробный разбор каждой подсистемы
(карта файлов, ход выполнения, рецепты «как добавить», ловушки) — в скиллах `.claude/skills/gm-*` (таблица ниже).

Задания: `TechJob/01-architecture.md` (GM-01, архитектура), `02-data.md` (GM-02, данные), `03-time.md` (GM-03, время),
`04-adventurers.md` (GM-04, авантюристы), `05-state-health.md` (GM-05, состояние и здоровье), `06-debug-logging.md` (GM-06, лог
и прогон без интерфейса), `07-event-feed.md` (GM-07, лента событий), `08-decision-model.md` (GM-08, модель решений),
`09-economy.md` (GM-09, экономика гильдии), `10-orders.md` (GM-10, заказы, доска и репутация), `11-quests.md` (GM-11, задания),
`12-groups.md` (GM-12, группы), `13-buildings-staff.md` (GM-13, постройки и персонал; им закончился этап 1),
`14-ui-observation.md` (GM-14, интерфейс наблюдения; им начался этап 2), `15-ui-decisions.md` (GM-15, интерфейс решений; этап 3,
первая часть), `16-decrees.md` (GM-16, распоряжения).

## Комментарии в коде: без ссылок на документацию

Код (и комментарии, и `[Tooltip]`/`[Header]`, и сообщения тестов) понятен сам, без `docs/` и `TechJob/`
(решение 2026-09-27):

- **Нет номеров и ссылок**: ни `ТЗ NN`, ни `GM-NN`, ни `TechSpec`, ни имён документов (`numbers.md`, `quests.md`…),
  ни разделов («→ «Генерация»»), ни дат решений, ни пометок ❔ 💡 ✅.
- **Комментарий говорит, что делает код и почему**, своими словами: «Общежития пока нет — все живут в городе»,
  а не «Общежитие — ТЗ 13».
- **Будущее не упоминается.** Не «до ТЗ 13 — заглушка», а что есть сейчас: «Лазарет не готов — коек 0».
  Кто вызывает — «вызывающая система», а не номер задачи.
- Исключение — генератор данных в песочнице: он переносит данные из документов, имя документа-источника там — часть
  описания того, что он делает.
- Связь кода с ТЗ — в этом файле, в скиллах и в самих ТЗ, не в коде.

## Где что лежит

```
_Project/
├── Scripts/
│   ├── Data/        GuildMaster.Data      — определения и числа (ScriptableObject)
│   │   ├── GameConfig.cs    корневой ассет: ссылки на всё остальное
│   │   ├── Balance/         BalanceSettings + 19 разделов (TimeBalance, OrdersBalance, …, FeedBalance)
│   │   ├── Definitions/     Axis, SpecialTrait (+ TraitEffect), Archetype, QuestType, RandomEvent, Discovery,
│   │   │                    Building, StaffRole, Decree, Dilemma, StatCatalog
│   │   ├── Text/            FeedTemplateSet, NameList, OrderTextTemplates, TextPlaceholders (словарь меток)
│   │   ├── Vocabulary/      перечисления словаря TechJob/README.md и кодовые id правил
│   │   └── Common/          Definition (id + displayName), NounForms (6 падежей), IntRange, FloatRange
│   ├── Core/        GuildMaster.Core      — симуляция на обычном C#
│   │   ├── Simulation/  Simulation, SimContext, ISimSystem, DataRegistry, ISimulationClient, SimulationSystems
│   │   ├── Time/        GameTime, Calendar, TimeSystem, DayPhase, DayRhythm, GameClock
│   │   ├── Autopause/   AutopauseSystem, AutopauseRules, AutopauseKind, AutopauseState, SetAutopauseCommand
│   │   ├── Random/      Rng (PCG32), RngService (потоки), StableHash
│   │   ├── Events/      SimEvent, SimEventType, EventImportance, EventBus
│   │   ├── Commands/    ICommand, CommandQueue, CommandSystem
│   │   ├── Logging/     SimLogger, SimLogLevel, EventLogLevels, AdventurerLog
│   │   ├── Adventurers/ люди: модель, генерация, архетип, черты, раскрытия, рост, ранги, отношения, кандидаты
│   │   ├── State/       состояние, занятия, срывы, кошелёк
│   │   ├── Health/      раны, лечение, IInfirmary (+ BuildingInfirmary — Лазарет мира)
│   │   ├── Decisions/   модель решений (+ DecisionSystem.Parties — сбор групп), причины ухода, таверна
│   │   ├── Economy/     казна, журнал, банкротство, трудные времена, отчёт месяца
│   │   ├── Feed/        лента, ключи, условия шаблонов, движок подстановки TextRenderer
│   │   ├── Guild/       GuildState (репутация), ReputationService
│   │   ├── Orders/      заказы, доска, генератор, Регистратор
│   │   ├── Quests/      задания: ход, раунды, напряжение, путь и находки, итог, экзамены, событийные задания
│   │   ├── Parties/     группы: оценка, жизнь постоянных групп, причины отказа
│   │   ├── Buildings/   постройки: очередь и стройка, Общежитие, двор, содержание, назначение построек
│   │   ├── Staff/       персонал: кандидаты, переговоры, найм и уход, зарплаты и долг, эффект уровня
│   │   ├── Decrees/     распоряжения: DecreeBook (включённые и отрезки действия), DecreeRules (эффекты для других систем),
│   │   │                DecreeService, ToggleDecreeCommand, DecreeSystem (снятие по сроку)
│   │   ├── DebugTools/  отладочные команды (Debug…Command), запросы для интерфейса ObserverQueries и отладки DebugQueries
│   │   └── World/       WorldState, IdGenerator
│   ├── UI/          GuildMaster.UI        — интерфейс наблюдения и решений (скилл gm-ui): UiRoot, Theme/UiTheme, Text/ (UiStrings,
│   │                шаблоны ui.*, форматы, ленты со ссылками), Navigation/ (UiNavigator, Destination, LinkRouter),
│   │                Models/ (модели экранов и действия игрока — обычный C#), Views/ (фабрика элементов, экраны Гильдия, Доска,
│   │                Задания, Казна, Распоряжения, окна, окна решений, отладочная панель)
│   ├── Bootstrap/   GuildMaster.Bootstrap — GameRunner (+ IGameSession: перезапуск с зерном, перемотка)
│   └── Debugging/   GuildMaster.Debugging — Headless/ (прогон без интерфейса, окно GuildMaster → Run Headless…, боты,
│                    сценарий, сводка, LogFiles), Validation/ (DataValidator, меню GuildMaster → Validate Data), EditorAssets
├── Data/            GameConfig, BalanceSettings, StatCatalog + папки Axes, Traits, Archetypes, QuestTypes,
│                    Encounters, Buildings, Staff, Decrees, Dilemmas, Text (FeedTemplates, NameList, OrderTextTemplates),
│                    UI (UiTheme, шрифт LiberationSans Cyrillic SDF)
├── ClaudeSandbox/   песочница Claude: генератор данных и меню-зонды (см. её README)
├── Prefabs/UI/
├── Scenes/Main.unity  — единственная сцена: Main Camera, Global Light 2D, UI (Canvas + UiRoot с UiTheme), EventSystem, GameRunner
└── Tests/EditMode/  GuildMaster.Tests
```

## Сборки

| Сборка | Ссылается на | Правило |
|---|---|---|
| `GuildMaster.Data` | — | Только определения и числа, без логики |
| `GuildMaster.Core` | Data | Без MonoBehaviour, сцен, `UnityEngine.Random`, `Time`, `DateTime.Now`, `Stopwatch` |
| `GuildMaster.UI` | Core, Data, UnityEngine.UI, Unity.TextMeshPro, Unity.InputSystem | Читает мир, отправляет команды; F1 и Esc |
| `GuildMaster.Bootstrap` | Core, Data, UI, Unity.InputSystem | Создаёт симуляцию, крутит такты, горячие клавиши |
| `GuildMaster.Debugging` | Core, Data, UI | Отладка; Editor-код — под `#if UNITY_EDITOR` |
| `GuildMaster.Tests` | Core, Data, Debugging, UI, UnityEngine.UI, Unity.TextMeshPro | EditMode; видит internal Core (`InternalsVisibleTo`) |

Правила проверяются тестами `ArchitectureTests`: ссылки asmdef, отсутствие Unity-объектов в Core,
поиск запрещённых API в исходниках Core, отсутствие публичных сеттеров у `WorldState` и его частей (публичные
методы частей мира — только вопросы `Is…`/`Has…`/`Get…`/`TryGet…`; новую часть мира — добавить в список теста).

## Сквозные правила

- **Такт — один игровой час**: системы по порядку `SimulationSystems.CreateDefault()` — `CommandSystem`, `TimeSystem`,
  `OrderSystem`, `QuestSystem`, `ActivitySystem`, `StateSystem`, `HealthSystem`, `DecisionSystem`, `EconomySystem`,
  `StaffSystem`, `BuildingSystem`, `SalarySystem`, `AdventurerSystem`, `RecruitSystem`, `DecreeSystem`, `MonthReportSystem`, `FeedSystem`,
  `AutopauseSystem` (всегда последняя). Пустые места 16-шагового порядка — в комментарии `SimulationSystems`. Стартовое состояние
  (люди, заказы, постройки, персонал) готовит конструктор `Simulation`, без событий.
- **Детерминизм**: одно зерно + те же команды в те же такты = тот же мир. В системе — только `ctx.Rng` (поток по имени системы);
  имена систем и порядок бросков не менять без причины; «пустой» случай новой механики бросков не тратит.
- **Мир меняется только командами**: части мира — публичные геттеры, `internal`-сеттеры, коллекции наружу — `IReadOnlyList`;
  мутации — службы Core с `SimContext`. UI получает симуляцию только как `ISimulationClient`.
- **Данные**: ScriptableObject — только определения и числа; числа баланса — только в `BalanceSettings`; перечисления,
  записанные в ассетах, — новые значения только в конец; ассеты заполняет генератор в песочнице, проверяет Validate Data.
- **Решение человека действует со следующего часа** (`PlannedActivity`, `PlannedOrderId`).
- **Интерфейс не меняет мир сам**: читает мир через `ISimulationClient`, меняет — существующими командами; в Core для него — только запросы
  (`ObserverQueries`) и данные без логики интерфейса. Профиль заказа, шанс и скрытые черты — только в отладке («Раскрыть всё»,
  `DebugQueries`). Числа интерфейса — `UiTheme`, не баланс; подписи — `UiStrings`, строки с игровым смыслом — шаблоны `ui.*`.
  TMP Essential Resources лежат в `Assets/TextMesh Pro/` (вне `_Project`).

## Подсистемы — в скиллах

| Подсистема | Скилл | Задачи |
|---|---|---|
| Такт, события, команды, случайность, время, автопауза, репутация | `gm-simulation-core` | GM-01, GM-03 |
| Определения, баланс, `DataRegistry`, валидатор, генератор | `gm-data` | GM-02 |
| Авантюристы: генерация, архетип, черты, раскрытия, рост, ранги, отношения, кандидаты | `gm-adventurers` | GM-04 |
| Состояние, занятия, срывы, кошелёк, раны, Лазарет, уход | `gm-state-health` | GM-05 |
| Лог, прогон без интерфейса, боты, сценарии, сводка, зонды песочницы | `gm-debug-headless` | GM-06 |
| Лента событий, шаблоны, движок подстановки | `gm-feed-text` | GM-07 |
| Модель решений, таверна, причины ухода | `gm-decisions` | GM-08 |
| Казна, журнал, банкротство, трудные времена, отчёт месяца | `gm-economy` | GM-09 |
| Заказы, доска, Регистратор, репутация | `gm-orders` | GM-10 |
| Задания: путь, раунды, напряжение, находки, итог, экзамены | `gm-quests` | GM-11 |
| Группы | `gm-parties` | GM-12 |
| Постройки и персонал: стройка, Общежитие, Лазарет, двор, найм, зарплаты | `gm-buildings-staff` | GM-13 |
| Распоряжения: команда, срок и область, эффекты в других системах, отчёт, сводка, экран | `gm-decrees` | GM-16 |
| Интерфейс: экраны, навигация, ссылки лент, уведомления, автопауза, отладочная панель, действия игрока, окна решений, казна | `gm-ui` | GM-14, GM-15, GM-16 |
| Тестовые миры и шаблоны тестов | `gm-testing` | — |
| Как вести задачу по ТЗ | `gm-implement-techspec` | — |

Задача GM-XX, которая меняет подсистему, обновляет и её скилл.

## Прогоны года: базовые цифры

10 зёрен, бот «Простой», 360 дней. Новая задача сравнивает свою серию с последней строкой (GuildMaster → Sandbox → Headless →
`Year Seeds 1-10 Info`).

| После | Что видно |
|---|---|
| GM-09 | казна 2000 → ~2040; таверна +14 в первый месяц, дальше 0–7 (без заданий кошельки пустеют); трудных времён нет |
| GM-10 | ~1,5 заказа в день (~45 в месяц), почти все снимаются по сроку; важных ~27 в год, все уходят без ответа |
| GM-11 | ~1900 заданий (от ~58 в первый месяц до ~215 в двенадцатый), выполнено ~93%, гибелей 4–11, экзаменов 54–74, репутация 100 к 4-му месяцу, казна ~54 тыс.; лог `Info` — ~55 тыс. строк |
| GM-12 | ~2000 заданий (73% соло, средняя группа 2,35), выполнено 94%, гибелей ~7, шанс раунда 0,83; постоянных групп ~12 в год, распадается ~4; год без лога — ~1,65 с (без групп ~1,46 с) |
| GM-13 | Общежитие к ~13-му дню, Лазарет к ~66-му, двор к ~89-му; ~2060 заданий (72% соло, группа 2,35), выполнено 95%, гибелей ~6; ~500 тренировок и ~93 «лёг в Лазарет» за год, лечение в Лазарете — ~0,85 номинала против ~1,45 без; персонал — 3 (Лекарь нанят один раз), невыплат нет; расходы гильдии ~860 в месяц, казна ~60 тыс.; год с логом `Info` — ~2,0 с, лог ~66 тыс. строк |
| GM-14 | цифры те же, что после GM-13 (броски не сдвинуты: ~2060 заданий, выполнено ~95%, гибелей ~6, казна ~60 тыс.); год с логом `Info` — ~1,9 с. Play Mode: кадр на ×4 — ~2,1 мс, скрипты (такты + интерфейс) — ~0,04 мс в среднем |
| GM-15 | цифры не сдвинулись (~2046 заданий, выполнено ~95%, казна ~60 тыс.); год с логом `Info` — ~2,0 с. Интерфейс проверяется только в 1920×1080 |
| GM-16 | без распоряжений — не сдвинулись (~2046 заданий, выполнено ~95%, казна ~60,4 тыс.). Все четыре с первого такта (Сухой закон — 7 дней; `Year Seeds 1-10 Info With Decrees`): ~2120 заданий, выполнено ~93%, гибелей ~6, казна ~50,6 тыс., расходы на распоряжения ~13,5 тыс. в год, довольство ~43,7 против ~45 |

## Тесты

`Tests/EditMode`, 480 тестов. Помощники, тестовые миры и шаблоны тестов — скилл `gm-testing`. Запуск — Test Runner или MCP
`run_tests`.
