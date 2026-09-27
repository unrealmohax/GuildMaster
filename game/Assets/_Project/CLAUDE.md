# Код игры: архитектура

Правила работы — в корневом `CLAUDE.md`. Здесь — общее устройство кода игры; подробный разбор каждой подсистемы
(карта файлов, ход выполнения, рецепты «как добавить», ловушки) — в скиллах `.claude/skills/gm-*` (таблица ниже).

Задания: `TechJob/01-architecture.md` (GM-01, архитектура), `02-data.md` (GM-02, данные), `03-time.md` (GM-03, время),
`04-adventurers.md` (GM-04, авантюристы), `05-state-health.md` (GM-05, состояние и здоровье), `06-debug-logging.md` (GM-06, лог
и прогон без интерфейса), `07-event-feed.md` (GM-07, лента событий), `08-decision-model.md` (GM-08, модель решений),
`09-economy.md` (GM-09, экономика гильдии), `10-orders.md` (GM-10, заказы, доска и репутация), `11-quests.md` (GM-11, задания),
`12-groups.md` (GM-12, группы).

## Комментарии в коде: без ссылок на документацию

Код (и комментарии, и `[Tooltip]`/`[Header]`, и сообщения тестов) понятен сам, без `docs/` и `TechJob/`
(решение 2026-09-27):

- **Нет номеров и ссылок**: ни `ТЗ NN`, ни `GM-NN`, ни `TechSpec`, ни имён документов (`numbers.md`, `quests.md`…),
  ни разделов («→ «Генерация»»), ни дат решений, ни пометок ❔ 💡 ✅.
- **Комментарий говорит, что делает код и почему**, своими словами: «Общежития пока нет — все живут в городе»,
  а не «Общежитие — ТЗ 13».
- **Будущее не упоминается.** Не «до ТЗ 13 — заглушка», а что есть сейчас: «построек пока нет — `NoInfirmary`».
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
│   │   ├── Health/      раны, лечение, IInfirmary (+ NoInfirmary)
│   │   ├── Decisions/   модель решений (+ DecisionSystem.Parties — сбор групп), причины ухода, таверна
│   │   ├── Economy/     казна, журнал, банкротство, трудные времена, отчёт месяца
│   │   ├── Feed/        лента, ключи, условия шаблонов, движок подстановки TextRenderer
│   │   ├── Guild/       GuildState (репутация), ReputationService
│   │   ├── Orders/      заказы, доска, генератор, Регистратор
│   │   ├── Quests/      задания: ход, раунды, напряжение, путь и находки, итог, экзамены, событийные задания
│   │   ├── Parties/     группы: оценка, жизнь постоянных групп, причины отказа
│   │   └── World/       WorldState, IdGenerator
│   ├── UI/          GuildMaster.UI        — UiRoot (экранов пока нет)
│   ├── Bootstrap/   GuildMaster.Bootstrap — GameRunner
│   └── Debugging/   GuildMaster.Debugging — Headless/ (прогон без интерфейса, окно GuildMaster → Run Headless…, боты,
│                    сценарий, сводка, LogFiles), Validation/ (DataValidator, меню GuildMaster → Validate Data), EditorAssets
├── Data/            GameConfig, BalanceSettings, StatCatalog + папки Axes, Traits, Archetypes, QuestTypes,
│                    Encounters, Buildings, Staff, Decrees, Dilemmas, Text (FeedTemplates, NameList, OrderTextTemplates)
├── ClaudeSandbox/   песочница Claude: генератор данных и меню-зонды (см. её README)
├── Prefabs/UI/
├── Scenes/Main.unity  — единственная сцена: Main Camera, Global Light 2D, UI (Canvas + UiRoot), EventSystem, GameRunner
└── Tests/EditMode/  GuildMaster.Tests
```

## Сборки

| Сборка | Ссылается на | Правило |
|---|---|---|
| `GuildMaster.Data` | — | Только определения и числа, без логики |
| `GuildMaster.Core` | Data | Без MonoBehaviour, сцен, `UnityEngine.Random`, `Time`, `DateTime.Now`, `Stopwatch` |
| `GuildMaster.UI` | Core, Data, UnityEngine.UI, Unity.TextMeshPro | Читает мир, отправляет команды |
| `GuildMaster.Bootstrap` | Core, Data, UI, Unity.InputSystem | Создаёт симуляцию, крутит такты, горячие клавиши |
| `GuildMaster.Debugging` | Core, Data, UI | Отладка; Editor-код — под `#if UNITY_EDITOR` |
| `GuildMaster.Tests` | Core, Data, Debugging | EditMode; видит internal Core (`InternalsVisibleTo`) |

Правила проверяются тестами `ArchitectureTests`: ссылки asmdef, отсутствие Unity-объектов в Core,
поиск запрещённых API в исходниках Core, отсутствие публичных сеттеров у `WorldState` и его частей (публичные
методы частей мира — только вопросы `Is…`/`Has…`/`Get…`/`TryGet…`; новую часть мира — добавить в список теста).

## Сквозные правила

- **Такт — один игровой час**: системы по порядку `SimulationSystems.CreateDefault()` — `CommandSystem`, `TimeSystem`,
  `OrderSystem`, `QuestSystem`, `ActivitySystem`, `StateSystem`, `HealthSystem`, `DecisionSystem`, `EconomySystem`,
  `AdventurerSystem`, `RecruitSystem`, `MonthReportSystem`, `FeedSystem`, `AutopauseSystem` (всегда последняя). Пустые места
  16-шагового порядка — в комментарии `SimulationSystems`. Стартовое состояние готовит конструктор `Simulation`, без событий.
- **Детерминизм**: одно зерно + те же команды в те же такты = тот же мир. В системе — только `ctx.Rng` (поток по имени системы);
  имена систем и порядок бросков не менять без причины; «пустой» случай новой механики бросков не тратит.
- **Мир меняется только командами**: части мира — публичные геттеры, `internal`-сеттеры, коллекции наружу — `IReadOnlyList`;
  мутации — службы Core с `SimContext`. UI получает симуляцию только как `ISimulationClient`.
- **Данные**: ScriptableObject — только определения и числа; числа баланса — только в `BalanceSettings`; перечисления,
  записанные в ассетах, — новые значения только в конец; ассеты заполняет генератор в песочнице, проверяет Validate Data.
- **Решение человека действует со следующего часа** (`PlannedActivity`, `PlannedOrderId`).

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

## Тесты

`Tests/EditMode`, 408 тестов. Помощники, тестовые миры и шаблоны тестов — скилл `gm-testing`. Запуск — Test Runner или MCP
`run_tests`.
