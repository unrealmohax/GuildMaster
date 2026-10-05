---
name: gm-simulation-core
description: Ядро симуляции GuildMaster — такт Simulation.Tick, порядок систем (SimulationSystems), SimContext, шина событий SimEvent/EventBus, команды ICommand, потоки случайности Rng/RngService, время GameTime/Calendar/DayRhythm/GameClock, автопауза, WorldState и правило «мир меняется только командами». Использовать при добавлении системы, события, команды, части мира, при вопросах о детерминизме, порядке шагов такта, времени и фазах дня.
---

# Ядро симуляции

Код: `game/Assets/_Project/Scripts/Core/` (далее `Core/`). Сборка `GuildMaster.Core` — обычный C#, без Unity-объектов.

## Карта файлов

| Файл | Что там |
|---|---|
| `Core/Simulation/Simulation.cs` | точка входа: конструктор готовит старт, `Tick()`, `ApplyCommandsNow()`, `TickCompleted`, `StateChanged` |
| `Core/Simulation/SimulationSystems.cs` | `CreateDefault()` — порядок систем + комментарий со всеми 16 шагами |
| `Core/Simulation/SimContext.cs` | что видит система: `World`, `Data`, `Calendar`, `Rhythm`, `Events`, `Log`, `Rng`, `RollChance`, `RequestPause`; internal `Commands`, `Streams` |
| `Core/Simulation/ISimSystem.cs`, `ISimulationClient.cs` | интерфейс системы; то, что видит UI |
| `Core/Simulation/DataRegistry.cs` | доступ к данным (см. скилл `gm-data`) |
| `Core/World/WorldState.cs` | все части мира; `World.Ids` (`IdGenerator`) — id людей; у заказов (`OrderBoard.NextId`), заданий (`QuestBook.NextId`) и групп (`PartyBook`) — свои счётчики, id разных сущностей могут совпадать |
| `Core/Events/` | `SimEvent` (`.With(ключ, значение)`, `TryGet<T>`), `SimEventType` (enum, новые — в конец), `EventImportance` (Normal/Notable/Important = [О]/[З]/[В]), `EventBus` |
| `Core/Commands/` | `ICommand.Apply(ctx)`, `CommandQueue`, `CommandSystem` (шаг 1) |
| `Core/Random/` | `Rng` (PCG32), `RngService.Stream(имя)`, `StableHash` |
| `Core/Time/` | `GameTime`, `Calendar`, `DayRhythm`, `DayPhase`, `TimeSystem`, `GameClock` |
| `Core/Autopause/` | `AutopauseRules.Default` (событие → вид), `AutopauseSystem`, `AutopauseState`, `SetAutopauseCommand` |
| `Core/Guild/` | `GuildState` (репутация), `ReputationService.Change` |
| `Scripts/Bootstrap/GameRunner.cs` | MonoBehaviour: владеет `GameClock`, крутит такты, горячие клавиши, после такта `ConsumePauseRequest()` |

## Такт (один игровой час)

`Simulation.Tick()`: если `IsFinished` (казна закрыта) — ничего. Иначе по каждой системе `Run(имя, Tick)`:
перед шагом ставит `ctx.CurrentSystem`, `ctx.Rng = Rng.Stream(имя)`, источник событий и подпись лога, после шага пишет
в лог новые события (уровень — `EventLogLevels`). Потом `TicksDone++` → `TickCompleted(события)` → `Events.Clear()` → `StateChanged`.

Порядок сейчас (`CreateDefault`): `CommandSystem` → `TimeSystem` → `OrderSystem` → `QuestSystem` → `ActivitySystem` →
`StateSystem` → `HealthSystem` → `DecisionSystem` → `DilemmaSystem` → `EconomySystem` → `StaffSystem` → `BuildingSystem` →
`SalarySystem` → `AdventurerSystem` → `RecruitSystem` → `DecreeSystem` → `MonthReportSystem` → `FeedSystem` → `AutopauseSystem`.
Место 9 (`PartySystem`) пусто; 10 — `DilemmaSystem` (скилл `gm-dilemmas`); 12 — `StaffSystem`, `BuildingSystem`, `SalarySystem`;
14 — `DecreeSystem` (скилл `gm-decrees`) — см. комментарий в `SimulationSystems`. Группы живут внутри `DecisionSystem`
и `QuestSystem`, отдельной системы у них нет.

Стартовое состояние — в конструкторе `Simulation`, до первого такта, **без событий**: казна и комиссия, репутация, затем
`Run(StartScenario.StreamName, StartScenario.Apply)` и `Run(OrderSystem.StartStreamName, OrderSystem.ApplyStart)` — у каждого свой поток.

## Время

- `GameTime.TotalHours` — часы от 00:00 дня 1 месяца 1 года 1. Все сроки в мире хранятся в нём (`…AtHours`).
- Старт — `TotalHours = StartHour` (06:00). `TimeSystem` сначала сдвигает час, потом публикует события,
  поэтому первое событие игры — 07:00.
- События времени: `HourStarted` (каждый час), `DayStarted` (00:00), `MonthStarted` (00:00 дня 1), `MorningStarted`,
  `DaytimeStarted`, `EveningStarted`, `NightStarted`. Системы обычно проверяют `ctx.World.Time.Hour == …` напрямую,
  а не слушают эти события.
- `DayRhythm`: `PhaseAt(час)`, `IsCampHour` (ночлег в пути), `CanStartQuest` (утро … `latestDepartureHour` включительно),
  `NextQuestStart`, `CampEnd`, `MarchEnd(старт, ходовых часов)`. Часы читает из `BalanceSettings.Time` при каждом вызове.
- Час H — такт, в котором мир стоит на H:00 и тратит час H:00–H+1:00.
- `GameClock` — пауза и скорость, на результат не влияет; не часть мира.

## Случайность и детерминизм

- Одно зерно + те же команды в те же такты = тот же мир. На этом держатся тесты `*DeterminismTests` и повтор сценария.
- В системе — **только `ctx.Rng`** (поток по имени системы). `ISimSystem.Name` не переименовывать: сдвинутся все броски.
- Бросок шанса с записью в лог — `ctx.RollChance(шанс, "что", человек, "показатель", значение)`: одно число, как `Rng.Chance`.
- Бросок, который не должен сдвигать поток текущей системы, — свой поток `ctx.Streams.Stream("Имя")` (пример — название
  постоянной группы, `PartyService.NameStream`).
- Порядок бросков внутри системы — часть поведения. Генераторы (`AdventurerGenerator`, `OrderGenerator`) и `QuestRounds`
  описывают порядок в комментарии класса — не менять без причины.
- Новый код, который в «пустом» случае не тратит бросков, не сдвигает чужой мир. Это проверяют тесты вида
  `…_DoesNotShiftOtherSystems` / `NoPartyPossible_NoRolls_SameLogAsWithoutParties`.
- Запрещено в Core: `UnityEngine.Random`, `Time`, `DateTime.Now`, `Stopwatch`, MonoBehaviour (тест `ArchitectureTests`).

## Мир меняется только командами

- Части мира: публичные геттеры, `internal`-сеттеры; коллекции наружу — `IReadOnlyList`. Публичные методы частей мира —
  только вопросы `Is…`/`Has…`/`Get…`/`TryGet…` (проверяет `ArchitectureTests`; **новую часть мира добавить в список теста**).
- Мутации — службы Core (`…Service`, статические классы), которые принимают `SimContext`. `SimContext` получают только
  системы и команды, поэтому UI мир не изменит.
- UI видит `ISimulationClient`: `World`, `Data`, `Calendar`, `Rhythm`, `IsFinished`, `Send(команда)`, `StateChanged`.

## Команды

`ICommand.Apply(ctx)` применяется в `CommandSystem` в начале такта (до сдвига времени), в порядке отправки. На паузе
`ApplyCommandsNow()` применяет их сразу тем же потоком `CommandSystem` — мир совпадёт с применением в следующем такте.
Команда, отправленная во время применения, ждёт следующего раза. Команда, которая ничего не меняет (то же значение),
событий не пишет — так сделаны все существующие.

## События

`ctx.Events.Publish(тип, важность, id участников…)` — время и система ставятся сами; данные — `.With("ключ", значение)`
(ключ уникален, порядок сохраняется). Участники — **люди** (у задания ещё id группы); id заказа и задания — в данных
(`order`, `quest`). Строка в логе: `Breakdown (Important) ids=3,5 kind=Binge`.

## Рецепты

**Новая система.** Класс `sealed : ISimSystem`, `Name => nameof(Класс)`, встать на своё место в `CreateDefault()` (сверить с
комментарием 16 шагов). Если система пуста без определений — `if (!ctx.Data.HasDefinitions) return;`. Тест на то, что она
не сдвигает чужие броски.

**Новое событие.** Значение в **конец** `SimEventType`. Затем по надобности: автопауза — строка в `AutopauseRules.Default`;
частое — уровень в `EventLogLevels`; строка ленты — `FeedKeys.Keys` (скилл `gm-feed-text`); счётчик в сводке — `SummaryColumns`
(скилл `gm-debug-headless`).

**Новый вид автопаузы.** Значение в конец `AutopauseKind` + строки в `AutopauseRules.Default`.

**Новая команда игрока.** `sealed class …Command : ICommand`; для прогонов — формат в `ScenarioCommands.Formats`, при
надобности правило бота (скилл `gm-debug-headless`).

**Новая часть мира.** Свойство в `WorldState`, класс с `internal`-сеттерами, в список `ArchitectureTests`.

## Ловушки

- События такта видны только до `Events.Clear()`: система, стоящая раньше публикующей, их не увидит в этом такте.
  `FeedSystem` и `AutopauseSystem` стоят в конце именно поэтому; `MonthReportSystem` — после всех, кто меняет мир.
- Решение человека действует **со следующего часа** (`PlannedActivity`, `PlannedOrderId`) — тесты должны тикать ещё раз.
- `DataRegistry(BalanceSettings)` — реестр только из чисел: людей, заказов, заданий и строк ленты нет (тесты времени).
- Репутацию меняет только `ReputationService.Change(ctx, Δ, причина)` (обрезка 0..`maxReputation`, событие без строки).
