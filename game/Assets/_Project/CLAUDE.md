# Код игры: архитектура

Правила работы — в корневом `CLAUDE.md`. Здесь — устройство кода игры. Задание на архитектуру —
`TechJob/01-architecture.md` (GM-01); этот файл описывает, как оно реализовано.

## Где что лежит

```
_Project/
├── Scripts/
│   ├── Data/        GuildMaster.Data      — ScriptableObject: GameConfig, BalanceSettings (+ разделы)
│   ├── Core/        GuildMaster.Core      — симуляция на обычном C#
│   │   ├── Simulation/  Simulation, SimContext, ISimSystem, DataRegistry, ISimulationClient, SimulationSystems
│   │   ├── Time/        GameTime, Calendar, TimeSystem
│   │   ├── Random/      Rng (PCG32), RngService (потоки), StableHash
│   │   ├── Events/      SimEvent, SimEventType, EventImportance, EventBus
│   │   ├── Commands/    ICommand, CommandQueue, CommandSystem
│   │   └── World/       WorldState, IdGenerator
│   ├── UI/          GuildMaster.UI        — UiRoot (экраны — ТЗ 15)
│   ├── Bootstrap/   GuildMaster.Bootstrap — GameRunner
│   └── Debugging/   GuildMaster.Debugging — HeadlessRun, окно GuildMaster → Run Headless…
├── Data/            GameConfig.asset, BalanceSettings.asset
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
| `GuildMaster.Bootstrap` | Core, Data, UI | Создаёт симуляцию, крутит такты |
| `GuildMaster.Debugging` | Core, Data, UI | Отладка; Editor-код — под `#if UNITY_EDITOR` |
| `GuildMaster.Tests` | Core, Data | EditMode; видит internal Core (`InternalsVisibleTo`) |

Правила проверяются тестами `ArchitectureTests`: ссылки asmdef, отсутствие Unity-объектов в Core,
поиск запрещённых API в исходниках Core, отсутствие публичных сеттеров у `WorldState`.

## Такт

`Simulation.Tick()` — один игровой час: системы из списка по порядку, затем `TickCompleted(события)` и очистка шины,
затем `StateChanged`. Порядок систем — в `SimulationSystems` (комментарий со всеми 16 шагами из ТЗ 01);
новая система встаёт на своё место в `CreateDefault()`. Сейчас реализованы шаги 1–2: `CommandSystem`, `TimeSystem`.

- **Время.** `GameTime.TotalHours` — часы от 00:00 дня 1 месяца 1 года 1 (не от старта игры). Старт —
  `TotalHours = StartHour` (06:00). Календарь — `Calendar` по `BalanceSettings.Time`. `TimeSystem` сначала
  двигает час, потом публикует события, поэтому первое событие игры — 07:00, а 06:00 первого дня
  событий не даёт (стартовое состояние готовит сценарий старта).
- **Команды.** UI вызывает `ISimulationClient.Send(команда)`; `CommandSystem` применяет очередь в начале такта,
  до сдвига времени. На паузе `GameRunner` вызывает `Simulation.ApplyCommandsNow()` — тот же поток случайных
  чисел и то же время, поэтому мир совпадает с применением в следующем такте. События таких команд уходят
  в лог вместе со следующим тактом.
- **События.** `ctx.Events.Publish(тип, важность, id…)` — шина сама ставит время и имя системы;
  полезные данные — `.With(ключ, значение)`, порядок сохраняется. `SimEvent.ToLogLine()` — строка
  без зависимости от культуры: `[1.1.1 07:00] [TimeSystem] [Normal] HourStarted`.
- **Случайность.** В системе — только `ctx.Rng`: это поток с зерном `хеш(мастер-зерно, имя системы)`.
  Имя системы (`ISimSystem.Name`) поэтому менять нельзя без причины — сдвинутся все броски.
  `Chance()` всегда тратит одно число. Алгоритм закреплён тестом на эталонные числа PCG32.
- **Автопауза.** Система вызывает `ctx.RequestPause()`; `GameRunner` после такта забирает флаг
  `ConsumePauseRequest()` и ставит паузу.

## Мир меняется только командами

Состояние в `WorldState` и его частях — публичные геттеры, `internal`-сеттеры. Снаружи Core (UI, Bootstrap)
изменить мир невозможно на уровне компилятора; UI получает симуляцию только как `ISimulationClient`.
Новые части мира (казна, люди, заказы…) заводятся по тому же правилу, коллекции наружу — `IReadOnlyList`.

## Данные

`DataRegistry` строится из `GameConfig` (`DataRegistry.FromConfig`) или прямо из `BalanceSettings` (тесты).
Сейчас в `BalanceSettings` только раздел `Time` — нужный такту; остальное — ТЗ 02. Ассеты во время игры
не меняются.

## Тесты

`Tests/EditMode`: `RngTests`, `TimeTests`, `SimulationTests`, `DeterminismTests`, `ArchitectureTests`.
Помощники — `TestSupport.cs`: `TestData` (BalanceSettings без ассета), `NoiseSystem`, `TraceCommand`,
`SimulationLog.Record` (лог событий прогона строкой). Запуск — Test Runner или MCP `run_tests`.
