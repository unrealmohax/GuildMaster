# Код игры: архитектура

Правила работы — в корневом `CLAUDE.md`. Здесь — устройство кода игры. Задания — `TechJob/01-architecture.md`
(GM-01, архитектура), `TechJob/02-data.md` (GM-02, данные), `TechJob/03-time.md` (GM-03, время),
`TechJob/04-adventurers.md` (GM-04, авантюристы); этот файл описывает, как они реализованы.

## Где что лежит

```
_Project/
├── Scripts/
│   ├── Data/        GuildMaster.Data      — определения и числа (ScriptableObject)
│   │   ├── GameConfig.cs    корневой ассет: ссылки на всё остальное
│   │   ├── Balance/         BalanceSettings + 18 разделов (TimeBalance, OrdersBalance, …)
│   │   ├── Definitions/     Axis, SpecialTrait (+ TraitEffect), Archetype, QuestType, RandomEvent, Discovery,
│   │   │                    Building, StaffRole, Decree, Dilemma, StatCatalog
│   │   ├── Text/            FeedTemplateSet, NameList, OrderTextTemplates, TextPlaceholders (словарь меток)
│   │   ├── Vocabulary/      перечисления словаря TechJob/README.md и кодовые id правил
│   │   └── Common/          Definition (id + displayName), NounForms (6 падежей), IntRange, FloatRange
│   ├── Core/        GuildMaster.Core      — симуляция на обычном C#
│   │   ├── Simulation/  Simulation, SimContext, ISimSystem, DataRegistry, ISimulationClient, SimulationSystems
│   │   ├── Time/        GameTime, Calendar, TimeSystem, DayPhase, DayRhythm (фазы, ночлег, выход), GameClock (пауза, скорости)
│   │   ├── Autopause/   AutopauseSystem, AutopauseRules (событие → вид), AutopauseKind, AutopauseState, SetAutopauseCommand
│   │   ├── Random/      Rng (PCG32), RngService (потоки), StableHash
│   │   ├── Events/      SimEvent, SimEventType, EventImportance, EventBus
│   │   ├── Commands/    ICommand, CommandQueue, CommandSystem
│   │   ├── Adventurers/ модель (Adventurer, TraitInstance, AdventurerRoster + Candidate, RelationBook), правила
│   │   │                (AdventurerStats, ArchetypeCalculator/Service, AxisMath, Growth, GuildRanks, RelationService,
│   │   │                TraitRules/TraitService, RevealService, AdventurerLifecycle), генерация (AdventurerGenerator,
│   │   │                StartScenario), системы AdventurerSystem, RecruitSystem и команды кандидатам
│   │   └── World/       WorldState, IdGenerator
│   ├── UI/          GuildMaster.UI        — UiRoot (экраны — ТЗ 15)
│   ├── Bootstrap/   GuildMaster.Bootstrap — GameRunner
│   └── Debugging/   GuildMaster.Debugging — HeadlessRun, окно GuildMaster → Run Headless…,
│                    Validation/ (DataValidator, меню GuildMaster → Validate Data), EditorAssets
├── Data/            GameConfig, BalanceSettings, StatCatalog + папки Axes, Traits, Archetypes, QuestTypes,
│                    Encounters, Buildings, Staff, Decrees, Dilemmas, Text (FeedTemplates, NameList, OrderTextTemplates)
├── ClaudeSandbox/   песочница Claude; Editor/GameDataGenerator — генератор ассетов данных, Editor/TimeProbe — время
│                    в Play Mode из меню, Editor/AdventurerProbe — люди гильдии в консоль (см. её README)
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

## Такт

`Simulation.Tick()` — один игровой час: системы из списка по порядку, затем `TickCompleted(события)` и очистка шины,
затем `StateChanged`. Порядок систем — в `SimulationSystems` (комментарий со всеми 16 шагами из ТЗ 01);
новая система встаёт на своё место в `CreateDefault()`. Сейчас реализованы шаги 1–2, 13 и 16: `CommandSystem`,
`TimeSystem`, `AdventurerSystem` + `RecruitSystem` (шаг 13 дополнен `AdventurerSystem`, решение 2026-09-26),
`AutopauseSystem` (всегда последняя). Тесты, которые добавляют системы в конец `CreateDefault()`,
ставят их после автопаузы — для проверки случайности это неважно. **Стартовое состояние** готовит конструктор
`Simulation` до первого такта: `StartScenario` со своим потоком `StartScenario` (стартовые люди), событий не пишет.

- **Время.** `GameTime.TotalHours` — часы от 00:00 дня 1 месяца 1 года 1 (не от старта игры). Старт —
  `TotalHours = StartHour` (06:00). Календарь — `Calendar` по `BalanceSettings.Time`. `TimeSystem` сначала
  двигает час, потом публикует события, поэтому первое событие игры — 07:00, а 06:00 первого дня
  событий не даёт (стартовое состояние готовит сценарий старта).
- **Ритм дня.** `TimeSystem` публикует `DayStarted` (сутки, 00:00), `MorningStarted`, `DaytimeStarted` (дневная фаза,
  09:00), `EveningStarted`, `NightStarted`, `MonthStarted`. `ctx.Rhythm` (`DayRhythm`, он же `Simulation.Rhythm`
  и `ISimulationClient.Rhythm`): `PhaseAt(час)`; для GM-06/09 — `IsCampHour` (группа в пути стоит с `campHour`
  до утра, раунды тоже), `MarchEnd(старт, часов)` — конец пути с ночлегами, `CampEnd`, `CanStartQuest`
  (с начала утра до `latestDepartureHour` **включительно**), `NextQuestStart`. Часы читаются из `BalanceSettings.Time`
  при каждом вызове. Час H — такт, в котором мир стоит на H:00 и тратит час H:00–H+1:00.
- **Пауза и скорость.** `GameClock` (Core, не часть мира и на результат не влияет): пауза, скорость — номер
  в `speedMultipliers`, отладочная ×`debugSpeedMultiplier` — только если создан с `debugSpeedAllowed`
  (`GameRunner` передаёт `Debug.isDebugBuild`: редактор или Development Build). `TakeTicks(секунды кадра)` —
  сколько тактов сделать, не больше `maxTicksPerFrame`. Выбор скорости снимает паузу. `GameRunner` владеет
  часами (`GameRunner.Clock`), читает горячие клавиши (Input System: Пробел, 1/2/3) и пишет смену скорости в консоль.
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
- **Автопауза.** `AutopauseSystem` смотрит события такта: тип есть в `AutopauseRules.Default` и его вид
  (`AutopauseKind`) включён в `World.Autopause` — запоминает событие в `World.Autopause.Triggers` (для окна
  «Перейти», GM-15; чистится в начале следующего такта) и вызывает `ctx.RequestPause()`; `GameRunner` после такта
  забирает флаг `ConsumePauseRequest()` и ставит паузу. **Новое событие с автопаузой — одна строка
  `{ SimEventType.Тип, AutopauseKind.Вид }` в `AutopauseRules`.** Переключатели — команда `SetAutopauseCommand`
  (пишет `AutopauseChanged`), по умолчанию все включены. Сейчас в правилах — раскрытия (`AxisRevealed`, `TraitRevealed`
  → `TraitRevealed`); остальные события таблицы ТЗ 03 появятся в ТЗ 05, 09, 10, 13.

## Авантюристы (GM-04)

- **Мир.** `World.Adventurers` (`AdventurerRoster`): `Active`, `Archive` (ушедшие и погибшие, с `LeftAtHours` и
  `LeaveReason`), `Candidates` (`Candidate`: человек + срок ожидания), `HeadCount` = активные + кандидаты (лимит
  `Guild.maxAdventurers`). `World.Relations` (`RelationBook`): симметричные пары `Relation` (значение −100..+100,
  совместные задания); нет записи — 0. Всё только на чтение снаружи Core; мутации — службы Core, которые требуют
  `SimContext` (его получают только системы и команды), поэтому UI мир изменить не может.
- **`Adventurer`**: базовые параметры по `StatId` (`Stats`, `GetStat`), оси (`Axes`, `RevealedAxes`), черты
  (`TraitInstance`: `TraitId`, `Revealed`, `PartnerId`, `AcquiredAtHours`, `AffectedStat` — параметр Калеки), ранг гильдии
  и `RankPoints` (дробные: частичный успех × 0,5), `PromotionReadyAtHours`, `ArchetypeId` + `PowerScore`, `State`
  (`AdventurerState`: пока только `Wallet`, остальное — GM-05), `Housing` (пока всегда `City`, Общежитие — GM-11),
  `JoinedAtHours`, счётчики заданий. Занятие (GM-06), группа (GM-07), память (GM-13) — не заведены.
- **Эффективные параметры** — `AdventurerStats`: `Permanent` = база × постоянные модификаторы (Калека), не ниже
  естественного минимума; `Effective` = `Permanent` × временные (пусто — рана и усталость в GM-05). Новый модификатор —
  строка в `PermanentModifiers` / `TemporaryModifiers`.
- **Архетип** — `ArchetypeCalculator.Evaluate` (чистая функция, по `Permanent`; роли — архетипы вида `Role` в порядке
  GameConfig), `Profile` — оценки всех ролей для карточки. `ArchetypeService.Recalculate(ctx, …)` — при изменении
  параметров (рост, черты вызывают сами) и в `AdventurerSystem` раз в сутки (00:00); смена — `ArchetypeChanged` без автопаузы.
- **Оси** — `AxisMath`: полюс, `Strength`, `Multiplier`/`ArrowMultiplier` (сила влияния эффекта полюса),
  `IsExtreme` (≥ 70), `IsNeutral` (< 30).
- **Рост** — `Growth`: `Train`, `PickTrainingStat`, `ApplyQuestExperience` (оси передаёт вызывающий, GM-09),
  `ApplyHardQuestComposure`, `ApplyGroupQuestCohesion`, `AddBonus`; `TrainingGain`/`QuestExperienceGain` — чистые.
  Хладнокровие и Слаженность не тренируются и опыта задания не получают.
- **Ранг гильдии** — `GuildRanks`: `AddPoints`, `IsReadyForPromotion`, `Promote` (очки → 0, `RankPromoted`),
  `FailPromotion` (+`promotionRetryDays`), `CanTakeOrder`.
- **Черты** — `TraitRules` (чистые: `CanAdd`, `IsPermanent` — Калека по эффекту `ProfileMultiplier`, `FindReplaced`),
  `TraitService.TryAcquire` / `TryRemove` (замена приобретённой, партнёр, параметр Калеки из потока вызывающего,
  раскрытие `OnAcquire`, Слаженность Проверенного). Эффекты черт в чужих системах — там. Черты находятся по `TraitHook`,
  а не по id.
- **Раскрытие** — `RevealService.TryRevealAxis` / `TryRevealTrait(ctx, человек, …, RevealTrigger)`: раскрывает, только если
  триггер совпадает с триггером полюса или черты в данных; событие [В] с автопаузой. Нейтральная ось — сама,
  `AdventurerSystem`, через `neutralRevealDays` в гильдии: `AxisBalanced` [З] без автопаузы.
- **Отношения** — `RelationService.Change` / `Set` / `AddJointQuest`, метки `LabelsOf` (друзья, неприязнь, давние напарники).
- **Генерация** — `AdventurerGenerator` (internal): пол, имя (по возможности без повторов), возраст → тип (веса
  `ArchetypeDefinition.generationWeight`) → уровень → параметры → оси → черты (с партнёром — только если есть подходящий
  в гильдии) → кошелёк, ранг G, город → архетип. Порядок бросков не менять без причины. Старт — `StartScenario`.
- **Кандидаты** — `RecruitSystem`: утром (`morningHour`) раз в сутки шанс `CandidateChance` (заглушки: репутация
  стартовая до ТЗ 10, распоряжений нет до ТЗ 12), ожидание `candidateWaitDays`, `CandidateArrived` / `CandidateLeft`;
  команды `AcceptCandidateCommand` (черта с партнёром появляется и у партнёра, если он ещё может её взять) и
  `RejectCandidateCommand`. Уход из гильдии — `AdventurerLifecycle.Retire` (событие публикует вызывающая система).
- **Реестр только из чисел** (`DataRegistry.HasDefinitions == false`, `TestData`) — симуляция без людей: старт и приток
  пропускаются. Поэтому тесты времени и случайности на `TestData` людей не видят.

## Мир меняется только командами

Состояние в `WorldState` и его частях — публичные геттеры, `internal`-сеттеры. Снаружи Core (UI, Bootstrap)
изменить мир невозможно на уровне компилятора; UI получает симуляцию только как `ISimulationClient`.
Новые части мира (казна, люди, заказы…) заводятся по тому же правилу, коллекции наружу — `IReadOnlyList`.

## Данные

- **ScriptableObject — только определения и числа**, не текущее состояние; во время игры не меняются.
  Поля — `[SerializeField] private` + геттеры; сеттеров нет. У определения (`Definition`) — неизменный `id`
  латиницей и `displayName`; `GameConfig` ссылается на всё, новое определение попадает в игру только через него.
- **`DataRegistry`** (Core) строится из `GameConfig` (`FromConfig`) или только из `BalanceSettings` (тесты).
  `Get<T>(id)` / `TryGet<T>(id)` — поиск среди определений своего вида; `All<T>()`, `Axis(AxisId)`,
  `FeedTemplates(ключ)`, `Stats`, `Names`, `OrderTexts`. Дубль id или пустая ссылка в списке — исключение при сборке.
- **Числа баланса — только в `BalanceSettings`**, в Core констант нет. Разделы повторяют numbers.md
  (плюс раздел `Traits` — числа особых правил черт). Значения по умолчанию в коде = стартовые числа из документов:
  ими заполняется новый ассет и `TestData`; дальше источник истины — ассет. Числа вариантов дилемм — в самих
  `DilemmaDefinition` (решение 2026-09-26); сила эффектов черт — стрелками или долями в `TraitEffect`.
- **Перечисления, записанные в ассетах** (`StatId`, `AxisId`, `TraitHook`, `RevealTrigger`, `DilemmaEffectKind`…),
  хранятся числами: новые значения — только в конец, существующие не переставлять.
- **Кодовые id правил**: особые правила черт — `TraitHook`, триггеры раскрытия — `RevealTrigger`, триггеры дилемм —
  `DilemmaTrigger`. Данные говорят «какое правило», код реализует его в своей системе (ТЗ 04, 09, 13).
- **Тексты**: подстановки `{имя}`, `{место:р}`, род `[м|ж]`, `[его|её]@имя` (ТЗ 14). Словарь меток —
  `TextPlaceholders`. У имён, мест, врагов, построек, распоряжений — `NounForms` (6 падежей); заполнен пока
  только именительный и род (`GrammaticalGender`), остальное и падежи в шаблонах — GM-14. Скобка рода у названий — `[м|ж|ср|мн]@постройка` (решение 2026-09-26). Ключи ленты (`quest.departed`, `reveal.risk.negative`…)
  — строки; определения ссылаются на них полями `…FeedKey`.
- **Валидатор** — GuildMaster → Validate Data (`DataValidator.Validate(config)`): обход сериализуемых полей
  отражением (пустые ссылки, кроме `[OptionalReference]`; `[Range]`/`[Min]`; `IntRange`/`FloatRange` с min > max),
  дубли id, связи (постройка ↔ должность, ключи ленты, шансы исходов в сумме 1…), разметка текстов.
  Ошибка — данные битые; предупреждение — подозрительно (сейчас 17: скобки рода без человека в предложении — GM-14).
  Консоль MCP видит итоговую строку только в `read_console` с `types: ["all"]`; полный отчёт — вывод теста
  `DataValidatorTests.RealConfig_HasNoErrors`.
- **Ассеты заполняет генератор в песочнице** (GuildMaster → Sandbox → Generate Game Data); он перезаписывает
  определения и тексты, баланс не трогает.

## Тесты

`Tests/EditMode`: `RngTests`, `TimeTests`, `DayRhythmTests`, `GameClockTests`, `AutopauseTests`, `SimulationTests`,
`DeterminismTests`, `ArchitectureTests`, `DataTests.cs` (`DataValidatorTests`, `DataRegistryTests`, `BalanceRuntimeTests`);
GM-04: `AdventurerTests.cs` (`AdventurerGenerationTests`, `ArchetypeTests`, `StartScenarioTests`), `TraitTests`,
`GrowthTests.cs` (`GrowthTests`, `GuildRankTests`, `RelationTests`), `RecruitTests.cs` (`RecruitTests`,
`PeopleDeterminismTests` — детерминизм, чужие потоки не сдвигаются, 360 дней с людьми).
Числа баланса в тесте меняются `GameData.Edit(data.Balance, "time.dayHour", p => p.intValue = 10)`.
Помощники — `TestSupport.cs`: `TestData` (BalanceSettings без ассета), `NoiseSystem`, `TraceCommand`,
`SimulationLog.Record` (лог событий прогона строкой), `ActionCommand` и `SimulationRun` (`Do` — вызвать службу Core
как команду на паузе, `Days`, `Collect` — события прогона); `DataTests.cs`: `GameData` — реальный `GameConfig`
и копии ассетов в памяти для порчи (`Copy`, `Edit` через `SerializedObject`); `PeopleTestData.cs`: `PeopleData` —
определения из реального `GameConfig` + `BalanceSettings` по умолчанию (`Set(путь, число)`); `PeopleDump` — состав строкой.
Запуск — Test Runner или MCP `run_tests`.
