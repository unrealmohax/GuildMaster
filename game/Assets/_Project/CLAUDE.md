# Код игры: архитектура

Правила работы — в корневом `CLAUDE.md`. Здесь — устройство кода игры. Задания — `TechJob/01-architecture.md`
(GM-01, архитектура), `TechJob/02-data.md` (GM-02, данные), `TechJob/03-time.md` (GM-03, время),
`TechJob/04-adventurers.md` (GM-04, авантюристы), `TechJob/05-state-health.md` (GM-05, состояние и здоровье),
`TechJob/06-debug-logging.md` (GM-06, лог и прогон без интерфейса), `TechJob/07-event-feed.md` (GM-07, лента событий),
`TechJob/08-decision-model.md` (GM-08, модель решений), `TechJob/09-economy.md` (GM-09, экономика гильдии),
`TechJob/10-orders.md` (GM-10, заказы, доска и репутация); этот файл описывает, как они реализованы.

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
- Связь кода с ТЗ — в этом файле и в самих ТЗ, не в коде.

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
│   │   ├── Time/        GameTime, Calendar, TimeSystem, DayPhase, DayRhythm (фазы, ночлег, выход), GameClock (пауза, скорости)
│   │   ├── Autopause/   AutopauseSystem, AutopauseRules (событие → вид), AutopauseKind, AutopauseState, SetAutopauseCommand
│   │   ├── Random/      Rng (PCG32), RngService (потоки), StableHash
│   │   ├── Events/      SimEvent, SimEventType, EventImportance, EventBus
│   │   ├── Commands/    ICommand, CommandQueue, CommandSystem
│   │   ├── Logging/     SimLogger, SimLogLevel, EventLogLevels (событие → уровень), AdventurerLog (строки о людях)
│   │   ├── Adventurers/ модель (Adventurer, TraitInstance, AdventurerRoster + Candidate, RelationBook), правила
│   │   │                (AdventurerStats, ArchetypeCalculator/Service, AxisMath, Growth, GuildRanks, RelationService,
│   │   │                TraitRules/TraitService, RevealService, AdventurerLifecycle), генерация (AdventurerGenerator,
│   │   │                StartScenario), системы AdventurerSystem, RecruitSystem и команды кандидатам
│   │   ├── State/       AdventurerState, Activity (+ BreakdownKind, PartyContext), правила StateRules, StateRates,
│   │   │                службы StateService, StressEvents, WalletService, системы ActivitySystem, StateSystem
│   │   ├── Health/      Condition, HealthService, HealthSystem, IInfirmary (+ заглушка NoInfirmary)
│   │   ├── Decisions/   DecisionSystem, DecisionPoints (+ DecisionBans), DecisionActions (+ DecisionScope), Motives
│   │   │                (+ MotiveWeights, StateFactor), LeaveReasons (+ LeaveCause, PersonTextSource), TavernEvening
│   │   ├── Economy/     Treasury (+ Bankruptcy, LedgerEntry), LedgerCategory (+ LedgerCategories), TreasuryService,
│   │   │                EconomySystem, HardTimes, SetCommissionCommand, MonthReport (+ история, разделы, строки),
│   │   │                MonthReportSections (+ ReportPeriod), MonthReportSystem, MonthReportText
│   │   ├── Feed/        FeedSystem, FeedState + FeedEntry (лента в мире), FeedKeys (событие → ключ), FeedConditions,
│   │   │                TextRenderer + TextValue + ITextSource (движок подстановки), EventTextSource (метки из события)
│   │   ├── Guild/       GuildState (репутация в мире), ReputationService
│   │   ├── Orders/      Order (+ статусы), OrderBoard (+ RegistrarRules, OrderTotals), OrderGenerator, OrderSystem,
│   │   │                OrderCommands (правила Регистратора, ответ на важный заказ, доплата), OrderTextSource
│   │   └── World/       WorldState, IdGenerator
│   ├── UI/          GuildMaster.UI        — UiRoot (экраны — ТЗ 14)
│   ├── Bootstrap/   GuildMaster.Bootstrap — GameRunner
│   └── Debugging/   GuildMaster.Debugging — Headless/ (HeadlessRun, окно GuildMaster → Run Headless…, боты,
│                    сценарий, сводка, LogFiles), Validation/ (DataValidator, меню GuildMaster → Validate Data),
│                    EditorAssets
├── Data/            GameConfig, BalanceSettings, StatCatalog + папки Axes, Traits, Archetypes, QuestTypes,
│                    Encounters, Buildings, Staff, Decrees, Dilemmas, Text (FeedTemplates, NameList, OrderTextTemplates)
├── ClaudeSandbox/   песочница Claude; Editor/GameDataGenerator — генератор ассетов данных, Editor/TimeProbe — время
│                    в Play Mode из меню, Editor/AdventurerProbe — люди гильдии в консоль, Editor/StateProbe — раны,
│                    стресс, прогон 90 дней, Editor/HeadlessProbe — прогон в файлы без окна, Editor/FeedProbe — шаблоны
│                    и лента за год в файлы (см. её README)
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
новая система встаёт на своё место в `CreateDefault()`. Сейчас реализованы шаги 1–3, 5–8, 11, 13, 15 и 16: `CommandSystem`,
`TimeSystem`, `OrderSystem`, `ActivitySystem`, `StateSystem`, `HealthSystem`, `DecisionSystem`, `EconomySystem`, `AdventurerSystem` + `RecruitSystem`
(шаг 13 дополнен `AdventurerSystem`, решение 2026-09-26), `MonthReportSystem` (отчёт месяца — после всех систем, которые меняют мир,
решение 2026-09-27), `FeedSystem` (после всех систем, которые публикуют события), `AutopauseSystem` (всегда последняя). Тесты, которые добавляют системы в конец `CreateDefault()`,
ставят их после автопаузы — для проверки случайности это неважно. **Стартовое состояние** готовит конструктор
`Simulation` до первого такта: казна (`Guild.startMoney`, `Economy.defaultCommission`), репутация (`Guild.startReputation`),
`StartScenario` со своим потоком `StartScenario` (стартовые люди) и `OrderSystem.ApplyStart` со своим потоком `OrderStart`
(стартовые заказы на доске); событий не пишет. **Гильдия закрыта** (`Simulation.IsFinished`, он же
`ISimulationClient.IsFinished`) — `Tick()` и `ApplyCommandsNow()` ничего не делают.

- **Время.** `GameTime.TotalHours` — часы от 00:00 дня 1 месяца 1 года 1 (не от старта игры). Старт —
  `TotalHours = StartHour` (06:00). Календарь — `Calendar` по `BalanceSettings.Time`. `TimeSystem` сначала
  двигает час, потом публикует события, поэтому первое событие игры — 07:00, а 06:00 первого дня
  событий не даёт (стартовое состояние готовит сценарий старта).
- **Ритм дня.** `TimeSystem` публикует `DayStarted` (сутки, 00:00), `MorningStarted`, `DaytimeStarted` (дневная фаза,
  09:00), `EveningStarted`, `NightStarted`, `MonthStarted`. `ctx.Rhythm` (`DayRhythm`, он же `Simulation.Rhythm`
  и `ISimulationClient.Rhythm`): `PhaseAt(час)`; для GM-08/11 — `IsCampHour` (группа в пути стоит с `campHour`
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
  в `TickCompleted` вместе со следующим тактом, а в `SimLogger` — сразу, со временем паузы.
- **События.** `ctx.Events.Publish(тип, важность, id…)` — шина сама ставит время и имя системы;
  полезные данные — `.With(ключ, значение)`, порядок сохраняется. `SimEvent.ToLogLine()` — строка
  без зависимости от культуры: `[1.1.1 07:00] [TimeSystem] [Normal] HourStarted`.
- **Случайность.** В системе — только `ctx.Rng`: это поток с зерном `хеш(мастер-зерно, имя системы)`.
  Имя системы (`ISimSystem.Name`) поэтому менять нельзя без причины — сдвинутся все броски.
  `Chance()` всегда тратит одно число. Алгоритм закреплён тестом на эталонные числа PCG32. Бросок шанса в системе —
  `ctx.RollChance(шанс, "что", человек, "показатель", значение)`: то же одно число, что `Rng.Chance`, и строка в лог.
- **Автопауза.** `AutopauseSystem` смотрит события такта: тип есть в `AutopauseRules.Default` и его вид
  (`AutopauseKind`) включён в `World.Autopause` — запоминает событие в `World.Autopause.Triggers` (для окна
  «Перейти», GM-14; чистится в начале следующего такта) и вызывает `ctx.RequestPause()`; `GameRunner` после такта
  забирает флаг `ConsumePauseRequest()` и ставит паузу. **Новое событие с автопаузой — одна строка
  `{ SimEventType.Тип, AutopauseKind.Вид }` в `AutopauseRules`.** Переключатели — команда `SetAutopauseCommand`
  (пишет `AutopauseChanged`), по умолчанию все включены. Сейчас в правилах — раскрытия (`AxisRevealed`, `TraitRevealed`
  → `TraitRevealed`), уход из гильдии (`AdventurerLeft` → `MemberLeftGuild`), начало банкротства (`BankruptcyStarted`) и важный
  заказ (`OrderAwaitingPlayer` → `ImportantOrder`); остальные события таблицы ТЗ 03 появятся в ТЗ 11, 13, 17.

## Авантюристы (GM-04)

- **Мир.** `World.Adventurers` (`AdventurerRoster`): `Active`, `Archive` (ушедшие и погибшие, с `LeftAtHours` и
  `LeaveReason`), `Candidates` (`Candidate`: человек + срок ожидания), `HeadCount` = активные + кандидаты (лимит
  `Guild.maxAdventurers`). `World.Relations` (`RelationBook`): симметричные пары `Relation` (значение −100..+100,
  совместные задания); нет записи — 0. Всё только на чтение снаружи Core; мутации — службы Core, которые требуют
  `SimContext` (его получают только системы и команды), поэтому UI мир изменить не может.
- **`Adventurer`**: базовые параметры по `StatId` (`Stats`, `GetStat`), оси (`Axes`, `RevealedAxes`), черты
  (`TraitInstance`: `TraitId`, `Revealed`, `PartnerId`, `AcquiredAtHours`, `AffectedStat` — параметр Калеки), ранг гильдии
  и `RankPoints` (дробные: частичный успех × 0,5), `PromotionReadyAtHours`, `ArchetypeId` + `PowerScore`, `State`
  (`AdventurerState`, GM-05), `Housing` (пока всегда `City`, Общежитие — GM-13),
  `JoinedAtHours`, счётчики заданий. Группа (GM-12), память (GM-17) — не заведены.
- **Эффективные параметры** — `AdventurerStats`: `Permanent` = база × постоянные модификаторы (Калека), не ниже
  естественного минимума; `Effective` = `Permanent` × временные (лёгкая рана, усталость выше 70 — GM-05). Новый модификатор —
  строка в `PermanentModifiers` / `TemporaryModifiers`.
- **Архетип** — `ArchetypeCalculator.Evaluate` (чистая функция, по `Permanent`; роли — архетипы вида `Role` в порядке
  GameConfig), `Profile` — оценки всех ролей для карточки. `ArchetypeService.Recalculate(ctx, …)` — при изменении
  параметров (рост, черты вызывают сами) и в `AdventurerSystem` раз в сутки (00:00); смена — `ArchetypeChanged` без автопаузы.
- **Оси** — `AxisMath`: полюс, `Strength`, `Multiplier`/`ArrowMultiplier` (сила влияния эффекта полюса),
  `IsExtreme` (≥ 70), `IsNeutral` (< 30).
- **Рост** — `Growth`: `Train`, `PickTrainingStat`, `ApplyQuestExperience` (оси передаёт вызывающий, GM-11),
  `ApplyHardQuestComposure`, `ApplyGroupQuestCohesion`, `AddBonus`; `TrainingGain`/`QuestExperienceGain` — чистые.
  Хладнокровие и Слаженность не тренируются и опыта задания не получают.
- **Ранг гильдии** — `GuildRanks`: `AddPoints`, `IsReadyForPromotion`, `Promote` (очки → 0, `RankPromoted`),
  `FailPromotion` (+`promotionRetryDays`), `CanTakeOrder`.
- **Черты** — `TraitRules` (чистые: `CanAdd`, `IsPermanent` — Калека по эффекту `ProfileMultiplier`, `FindReplaced`),
  `TraitService.TryAcquire` / `TryRemove` (замена приобретённой, партнёр, параметр Калеки из потока вызывающего,
  раскрытие `OnAcquire`, лояльность и Слаженность Проверенного). Эффекты черт в чужих системах — там. Черты находятся
  по `TraitHook`, а не по id: в данных — `TraitRules.FindByHook`, у человека — `TraitRules.FindWithHook`.
- **Раскрытие** — `RevealService.TryRevealAxis` / `TryRevealTrait(ctx, человек, …, RevealTrigger)`: раскрывает, только если
  триггер совпадает с триггером полюса или черты в данных; событие [В] с автопаузой. Нейтральная ось — сама,
  `AdventurerSystem`, через `neutralRevealDays` в гильдии: `AxisBalanced` [З] без автопаузы.
- **Отношения** — `RelationService.Change` / `Set` / `AddJointQuest`, метки `LabelsOf` (друзья, неприязнь, давние напарники).
- **Генерация** — `AdventurerGenerator` (internal): пол, имя (по возможности без повторов), возраст → тип (веса
  `ArchetypeDefinition.generationWeight`) → уровень → параметры → оси → черты (с партнёром — только если есть подходящий
  в гильдии) → кошелёк, ранг G, город → архетип → стартовое состояние (GM-05, последние броски). Порядок бросков не менять без причины. Старт — `StartScenario`.
- **Кандидаты** — `RecruitSystem`: утром (`morningHour`) раз в сутки шанс `CandidateChance(данные, репутация)` — от текущей
  `World.Guild.Reputation` (GM-10; распоряжений нет до ТЗ 16), ожидание `candidateWaitDays`, `CandidateArrived` / `CandidateLeft`;
  команды `AcceptCandidateCommand` (черта с партнёром появляется и у партнёра, если он ещё может её взять) и
  `RejectCandidateCommand`. Уход из гильдии — `AdventurerLifecycle.Retire` (событие публикует вызывающая система).
- **Реестр только из чисел** (`DataRegistry.HasDefinitions == false`, `TestData`) — симуляция без людей: старт и приток
  пропускаются. Поэтому тесты времени и случайности на `TestData` людей не видят.

## Состояние и здоровье (GM-05)

Рамки — ТЗ 05 и его раздел «Заглушки до следующих ТЗ» (решение 2026-09-26). Числа — `BalanceSettings`: `State`,
`Health`, `Expenses`, `Traits`, `Economy`, `Rounds` (лестница провалов); новых чисел в коде нет.

- **`AdventurerState`** (часть мира, `Adventurer.State`): `Fatigue`, `Stress`, `Contentment`, `Loyalty` (0..100, float),
  `Wallet`, `DebtToGuild` (int), `Conditions` (раны), `Activity`, флаги `IsWalletEmpty`, `Breakdown` + `BreakdownEndsAtHours`
  («в запое до …»), `InInfirmary`, `SkipsDayUntilHours` (Пьяница), учёт суток `AteInTavernToday` / `DrankToday` /
  `PaidInfirmaryToday` (сбрасывается в 00:00). Вопросы: `HasHeavyWound`, `HasLightWound`, `IsOnQuest`, `TryGetCondition`.
  Старт — `StateService.InitializeNew` в конце генератора: усталость 10, стресс 10–30, довольство 50, лояльность 40–60.
- **Занятие** — `Activity`: `Resting`, `Sleeping`, `Tavern`, `Training`, `Infirmary`, `Binge`, `OnQuestTravel/Round/Camp`.
  Ставит **`ActivitySystem`** (шаг 5), первое подходящее: на задании — не трогает; запой — `Binge`, «сел и не смог
  подняться» — `Resting` весь срок; ночь — `Sleeping`; койка — `Infirmary`; тяжёлая рана — `Resting`; Пьяница, пропускающий
  день, — `Tavern` с утра; иначе — занятие, выбранное решением в прошлом часу (`PlannedActivity`, см. «Модель решений»), иначе
  прежнее свободное (`IsFreeTime`: отдых, таверна, тренировка), после сна / срыва / Лазарета — `Resting`. Расписания
  «день — отдых, вечер — таверна» больше нет (GM-08). Там же расходы
  занятия раз в сутки (выпивка 2–5 при стрессе > 30 или Пьянице, еда в таверне — если кошелёк ≥ расходов на неделю;
  запой — 5; Лазарет — 5) и утренний пропуск дня Пьяницы (10% / 20% при стрессе > 50, раскрывает черту).
- **`StateSystem`** (шаг 6): каждый час — усталость и стресс по таблице занятий (`StateRules.FatiguePerHour` /
  `StressPerHour`) × эффекты черт; в 00:00 по каждому — расходы на жизнь (`WalletService.PayDaily`: еда 2 / 3 в таверне,
  жильё), довольство к цели (`StateRules.ContentmentTarget`) на 1, лояльность к довольству на 0,1, сброс учёта суток,
  срыв (стресс > 80, 10%), конец спада Потерявшего товарища (30 дней → раскрытие, «сломался» / «ожесточился», черта
  снимается); в начале месяца — проверка ухода (лояльность < 25, 20%, Семейный × 1,5 → `AdventurerLifecycle.Retire`,
  `AdventurerLeft` [В] с автопаузой; причины — `LeaveReasons` из мотивов и состояния, Наёмник раскрывается, если главная —
  деньги, см. «Модель решений»). Люди на задании не срываются и не уходят (ТЗ 11).
- **`HealthSystem`** (шаг 7): в 00:00 — койки Лазарета (тяжёлые раны первыми, затем кто раньше ранен), затем у каждой
  раны срок −1 × множитель (Лазарет — 1 / 0,7 × скорость Лекаря, без него — 1 / 1,5), зажила — `WoundHealed`. Тяжёлая
  рана без койки — один бросок на осложнение в первые сутки лечения (+7 дней, стресс +10, `WoundComplicated`).
  Лазарет — **`IInfirmary`** в конструкторе системы; по умолчанию `NoInfirmary` (коек нет, решение 2026-09-26),
  тесты подменяют (`FakeInfirmary`), ТЗ 13 даст настоящий.
- **Черты на состояние** — `StateRates.Multiplier(человек, показатель, рост/падение, данные, PartyContext)` читает эффекты
  `StateRate` из данных: у осей — по формуле оси, у особых черт — полностью. Трус — стресс быстрее, Кошмары — усталость
  × 1,3, Потерявший товарища — снятие стресса × 0,5, Преданный / Наёмник — лояльность, Одиночка / Командный — только
  с `PartyContext.InGroup` / `Solo` (его передаст ТЗ 12/11; вне задания `None`). `PayContentmentSensitivity` (Жадный × 2)
  — к вкладу комиссии. Остальное по `TraitHook` — в своих местах: Железные нервы и партнёр Влюблённого — `StressEvents`,
  Семейный — `WalletService` и проверка ухода, Пьяница — `ActivitySystem`, Ветеран — вид срыва и раскрытие, Проверенный —
  `TraitService`.
- **Службы для следующих ТЗ** (все требуют `SimContext`): `StateService.AddStress` / `AddFatigue` (× черты),
  `ChangeContentment` / `ChangeLoyalty` (разово, без множителей — дилеммы ТЗ 17), `StartBreakdown` (срыв Ветерана после
  тяжёлого задания — ТЗ 11); `StressEvents.RoundFailed` (лестница провалов), `ComradeWounded`, `AdventurerDied`
  (стресс +25 / друг +40 / Влюблённый +60, Железные нервы × 0,5, другу и партнёру — «Потерявший товарища»);
  `HealthService.Wound` (своя рана — стресс; лёгкие не складываются; вторая тяжёлая — `Maim`, черта «Калека»);
  `WalletService.Pay`, `ReceiveIncome(IncomeKind.Reward/Loot)` (Семейный 30% домой, долг — 20% доли награды, доли вниз),
  `TakeLoan`. Долг погибшего или пропавшего списывает `AdventurerLifecycle.Retire`.
- **Запреты и вопросы для ТЗ 08, 11**: `StateRules.CanTakeQuests` (тяжёлая рана, усталость > 90, срыв),
  `IsTooTiredForQuests`, `WalletService.IsBelowWeeklyExpenses` / `MoneyMotiveMultiplier` (мотив «Деньги» × 2),
  `AdventurerStats.Effective` (лёгкая рана × 0,85, усталость > 70 × 0,8).
- **Лояльность словами** — `StateRules.LoyaltyWordIndex` (границы `loyaltyWordThresholds`, на границе — верхний интервал)
  и `LoyaltyWord(лояльность, пол, …)`; ❔ тексты пока в коде (`LoyaltyWordsMale` / `Female`).
- **Заглушки** (решение 2026-09-26): платы Общежития, двора и Лазарета не зачисляются в казну (ТЗ 13; построек нет), Лазарета нет.
  Таверна и комиссия доделаны в GM-09: еда и выпивка — порции в доход таверны (`TreasuryService.CountTavernFood/Drink`
  в `StateSystem` и `ActivitySystem`, если заплачено хоть что-то), цель довольства — текущая `Treasury.Commission`. Без заданий (ТЗ 11) доходов нет: за 2–3 недели
  кошельки пустеют, довольство падает к 25, лояльность — к 25 (ровно на пороге ухода «ниже 25» — никто не уходит).
- **События** (`SimEventType`, новые — в конец): `AdventurerWounded`, `WoundComplicated`, `WoundHealed`, `Breakdown`,
  `DrunkardSkippedDay`, `WalletEmptied`, `AdventurerLeft`, `GrievingEnded`. Строки ленты — см. «Лента событий (GM-07)».

## Лог и прогон без интерфейса (GM-06)

- **`SimLogger`** (Core, `Logging/`): строка `[Год.Месяц.День ЧЧ:00] [Система] [Уровень] текст`, конец строки `\n`, числа
  без культуры. Уровни `SimLogLevel`: `Error`, `Info`, `Debug`, `Trace`; логгер пишет свой уровень и всё выше. Пишет
  в `TextWriter` (не закрывает его) и, если задан, в обработчик консоли. Даётся в конструктор `Simulation`
  (`CreateDefault(data, seed, log)`); без него — `SimLogger.Disabled`. Логгер привязывается к одной симуляции (время
  подписи — время мира), имя системы ставит `Simulation.Run` перед каждым шагом. Случайных чисел не тратит.
- **Выключенный уровень не строит строку**: простые строки — `ctx.Log.Write<T0…T3>(уровень, формат, аргументы)` (формат
  только при включённом уровне, дженерики — без упаковки), сложные — под `if (log.IsOn(уровень))` через
  `Begin(уровень)` → дописать в построитель → `Commit()`. Строку со склейкой (`"a" + x`) в `Write` не передавать.
- **События** пишет сама `Simulation` — сразу после шага системы, которая их опубликовала (после её бросков), текстом
  `SimEvent.AppendLogText`: `Breakdown (Important) ids=3,5 kind=Binge`. Уровень — `EventLogLevels`: по умолчанию `Info`;
  `DayStarted` — `Debug`, часы и фазы дня — `Trace` (иначе на `Info` ~10 тыс. строк за год). Новое частое событие — строка там.
- **Что пишется** (`AdventurerLog`): `Debug` — броски `ctx.RollChance` (`roll breakdown #3 Имя stress=85.2 chance=0.1
  rolled=0.0532 => yes`): срыв, уход (`loyalty`), осложнение, приход кандидата, пропуск дня Пьяницы, спад Потерявшего
  товарища, рана в драке; генерация человека (`start` — стартовая шестёрка после раздачи черт, `candidate` — кандидат):
  тип, уровень, архетип, параметры, оси, черты со скрытыми, кошелёк и стартовое состояние; стартовые «старые друзья».
  `Trace` — раз в сутки на каждого (`StateSystem`, после суточных сдвигов): занятие, усталость, стресс, довольство и цель,
  лояльность, кошелёк, флаги, раны. `Error` — исключение прогона.
- **Прогон** — `HeadlessRun.Run(data, зерно, дней, бот, лог)`: перед каждым тактом ходит бот, затем такт; итог — `Result`
  (симуляция, такты, события, время, строк лога, сводка, сценарий). Первая строка лога — параметры прогона
  (`[-] [HeadlessRun] [Info] run seed=… bot=…`), команды бота — `[Bot] [Info] send accept 7`.
  `RunToFiles(config, Options, прогресс)` — серия в `Logs/` проекта (`LogFiles`): прогон `i` — зерно `Seed + i`;
  на каждый `sim_{зерно}_{дата}.log`, `summary_{зерно}.csv`, `scenario_{зерно}.txt`; при нескольких — `summary_{зерно}_x{N}.csv`.
- **Бот** (`PlayerBot.cs`): `PlayerBot` = имя + список `IBotRule`; правило получает `BotTurn` (мир на чтение, `Send`,
  номер такта). Готовые — `PlayerBots.Presets`: «Пассивный» (без правил), «Простой» (`AcceptAllCandidatesRule`).
  Новое поведение — правило в списке бота, новый бот — строка в `Presets`.
- **Сценарий** (`ScenarioScript.cs`): текст, строка на команду `такт слово аргументы` (`672 accept 7`), такт — сколько
  тактов сделано к отправке, `#` — комментарий, `# seed=N` — зерно записи. Команды — `ScenarioCommands.Formats`
  (`accept`, `reject`, `autopause Вид on|off`); новая команда игрока — строка там. `ScenarioRecorder` пишет команды
  бота в сценарий (неизвестную — комментарием, `Unrecorded`), `ScenarioRule` — бот «Сценарий».
- **Сводка** (`RunSummary.cs`, `SummaryColumns.cs`): столбцы — `MonthColumn` (имя, функция от `MonthRecord` — мир на конец
  месяца и счётчики событий месяца, свёртка в итог `Sum`/`Last`/`Mean`, формат) и `PersonColumn` (функция от
  `PersonRecord` — человек и события, где он первый участник). Новый показатель — строка в `SummaryColumns.Monthly` /
  `People`. Строка — календарный месяц, закрывается после его последнего часа; последний неполный — если в нём прошли
  целые сутки (360 дней с 06:00 — ровно 12 строк). Люди — все, кто был в гильдии (активные и архив). Несколько
  прогонов — `SummaryAggregate` (среднее, стандартное отклонение выборки, мин, макс по месяцам и итогу).
  `SummaryCsv`: запятая, точка, UTF-8 с BOM (Excel), таблицы через пустую строку.
- **Окно** GuildMaster → Run Headless…: Game Config, зерно (случайное по кнопке), дней (360), уровень лога (Info),
  лог в консоль, бот («Простой»; «Сценарий» — с файлом, зерно берётся из `# seed=`), прогонов (1–100). Итог — отчёт,
  таблицы сводки (при серии — среднее ± разброс), «Открыть папку Logs».
- **Доработка GM-10**: «Простой» ещё держит правила Регистратора «все типы, до высшего ранга людей, без порога»
  (`RegistrarUpToTopRankRule` — шлёт команду, когда высший ранг сменился) и отвечает на важные заказы
  (`AnswerImportantOrdersByRankRule`: как только есть человек ранга заказа или выше — принять без доплаты; до тех пор не отвечать —
  заказ ждёт, без ответа заказчик уходит сам). Команды
  сценария — `registrar all|none|Hunt,Escort Ранг Награда`, `answer id accept Доплата` / `answer id decline`, `surcharge id сумма`.
  В сводке — «Заказов пришло», «На доску», «Отклонено Регистратором», «Отклонено игроком», «Снято по сроку», «Репутация».
  Лог `Info` — ещё ~2 тыс. строк событий заказов в год; `Debug` — заказ целиком при появлении (`order new #4 …`).
- **Доработка GM-09**: «Простой» первым ходом ставит комиссию 20% (`SetCommissionOnceRule`); команда сценария
  `commission 0.2`; прогон кончается раньше срока, если гильдия закрылась (`Result.GuildClosed`, строка
  `guild closed at …`, в окне — «Гильдия закрыта»); в сводке — «Казна» (на конец месяца), «Доходы», «Расходы»,
  «Банкротство» (0/1) и по статье журнала «Доход: {статья}» / «Расход: {статья}» (`SummaryColumns.MonthlyFor(data)` —
  названия из шаблонов `ledger.*`; `MonthRecord.Ledger(направление, статья)` — записи тактов месяца).
- **Скорость** (редактор): год с ботом «Простой» — ~0,18 с без лога и на `Info` (62 строки), ~0,21 с на `Trace`
  (~14 тыс. строк); серия из 10 лет на `Info` — ~3 с.

## Лента событий (GM-07)

- **Мир.** `World.Feed` (`FeedState`): `Guild` — строки `FeedEntry` (`TimeHours` события, `Feed`, `Importance`, `Text`, `Links` —
  участники события, `TemplateKey`), не больше `Feed.guildFeedLimit` (500), старые выбрасываются; `GetAddedCount(важность)` —
  сколько строк добавлено за игру (для сводки); `TryGetLastVariant(ключ)` — последний вариант строки ключа.
- **`FeedSystem`** (шаг 15, перед автопаузой): по каждому событию такта — ключ (`FeedKeys.Of`; нет ключа — нет строки) →
  шаблоны ключа из `DataRegistry.FeedTemplates` → `FeedConditions.Select` (все условия выполнены, больше условий — конкретнее,
  при равенстве — первый в данных) → случайный вариант, кроме последнего у ключа (поток `FeedSystem`; один вариант — без броска)
  → `TextRenderer.Render` со значениями `EventTextSource` → лента и лог `Info`: `feed [В] guild.adventurer.left: Ян ушёл из гильдии`.
  Ошибки шаблона (нет шаблона, метка без источника, скобка без владельца) — `Error` в лог, строка всё равно пишется с меткой как есть.
  Реестр только из чисел строк не даёт. Важность строки — из шаблона.
- **Новое событие со строкой** — строка в `FeedKeys.Keys` (+ константа в `Fixed`, если ключ не из данных — его проверит
  валидатор) и шаблон в генераторе. Ключи раскрытия — из данных: `revealFeedKey` полюса и черты, `balancedFeedKey` оси.
  Срыв — свой ключ на вид (`guild.breakdown.*`, драка без противника — `brawlAlone`); «поправился» — только когда ран не осталось.
- **Условия шаблонов** — `FeedConditions.Checks` (вид → проверка): `Archetype`, `RevealedAxisPole` / `RevealedTrait` (скрытая
  черта не называется), `MaimedStat`. Новое условие — значение `FeedConditionKind` в конец и строка в `Checks`; условие без
  проверки не выполняется.
- **Движок подстановки** `TextRenderer` (общий, не только лента): `{метка}` / `{метка:р|д|в|т|п}`; `[м|ж]` — род ближайшего
  предыдущего человека в предложении, иначе ближайшего следующего; `[…]@метка` — явная привязка; у названий `[м|ж|ср|мн]@метка`,
  нет формы — мужская; значение в начале предложения — с заглавной. Значения — `ITextSource` → `TextValue` (`Person`, `Noun`,
  `Word`, `Number`; нет формы падежа — именительный). Метки из события — `EventTextSource.Sources` (имя/напарник — участники,
  остальное — ключи данных события `medic`, `place`, `count`…). Падежи имён — `DataRegistry.NameForms(имя)`.
- **Сводка**: столбцы «Лента [О]», «Лента [З]», «Лента [В]» — строк за месяц (`MonthRecord.FeedLines`).
- **Строки GM-09**: `guild.bankruptcy.started` [В], `guild.bankruptcy.lifted` [З], `guild.closed` [В] (💡), `guild.month.summary` [З]
  (событие `MonthReportReady`: `income`, `expense`, `count` — погибших).
- **Строки GM-10**: за утро — `guild.board.newOrders` и `guild.board.declined` (`count`), `guild.board.awaitingPlayer` [В],
  `guild.board.expired` (вариант по типу — условие `QuestType`: id в данных события `questType`), `guild.board.noAnswer`
  (только `OrderDeclinedByPlayer` с `noAnswer`; отказ игрока строки не даёт). Названия — из данных события заказа (`client`,
  `place`, `enemy`, `cargo`).

## Модель решений (GM-08)

Числа — `BalanceSettings.Decisions` (выбор, стрелки, пороги состояния, оценки отдыха и таверны, `daytimeTavernStress`,
`maxReasons`, `recentBreakdownDays`) и `Adventurers` (таверна и ссоры). Решение 2026-09-27 в `docs/decisions.md`.

- **`DecisionSystem`** (шаг 8, свой поток): в час начала ночи — итоги вечера (`TavernEvening.Settle`); по каждому свободному
  (`DecisionPoints.CanDecide`: не на задании, не в запое и не «сел и не смог подняться», не в Лазарете, без тяжёлой раны, не
  пропускает день; отказ от заданий не мешает) — первая наступившая точка из `DecisionPoints.Default` (ночь → вечер → утро →
  освободился); вечером отмечает `InTavernThisEvening`; в конце — `WasFreeLastHour`.
- **Точка** — `DecisionPoint` (вид, `IsDue`, варианты, `OnDecided`). Утро — первый утренний час, когда свободен, раз в сутки
  (`MorningDecisionDay`); освободился — час назад не был свободен, сейчас свободен, не ночь (и только что вступивший); ночь —
  `Sleep` без выбора (сон ставит `ActivitySystem`, в лог `Debug`). **Новая точка — строка в `DecisionPoints.Default`.**
- **Решение**: запреты (`DecisionBans.Default`: правило + причина в лог `Debug`; сейчас — таверна утром и днём, кроме Пьяницы
  и стресса выше `daytimeTavernStress`) → веса (`Motives.Weigh(человек, данные, вТаверне)`: эффекты `MotiveWeight` осей плавно,
  особых черт полностью; утешение Пьяницы — только для таверны, `DrunkardComfortOnlyInTavern`; состояние — кошелёк, усталость,
  стресс, лёгкая рана, факторы — `MotiveWeights.Factors`) → оценки (`DecisionActions`, −1..1) → ценность Σ вес × оценка → лучший
  с `bestChoiceChance` (бросок `decision-best`; один вариант — без броска), иначе второй → `PlannedActivity`.
  **Новое действие — `DecisionAction` (вид, занятие, «в таверне», функция оценок) в `DecisionActions` и в варианты точки.**
  `DecisionScope` — контекст оценки на час: друзья (`FriendsHeadingTo`) собираются один раз за час.
- **Решение действует со следующего часа**: `ActivitySystem` ставит `PlannedActivity` в следующем такте, если ничто не мешает.
- **Лог**: `Info` — `decide #3 Имя evening: Tavern 2.21 (best; Rest 1.43) because Comfort 0.95 (stress 72), …`;
  `Debug` — запреты (`ban …`), сон (`… night: Sleep (no choice)`), броски `decision-best` и `quarrel`, `tavern evening: N people`;
  `Trace` — `scores #3 Имя evening Tavern=2.21: Money 1x-0.01 …` по каждому варианту. Год с 6 людьми — ≈ 6 тыс. строк `Info`.
- **Вечер в таверне** (`TavernEvening`): кто был в таверне хотя бы час вечера (не запой) — пары по порядку: + `tavernEveningRelation`,
  затем при отношениях ниже `quarrelRelationBelow` или у Соперников — шанс `quarrelChance`: + `quarrelRelation`, событие
  `Quarrel` [З] (строка `guild.relation.quarrel`), раскрытие Соперника у обоих (`RivalFirstClash`).
- **Причины ухода** (`LeaveReasons`): `Of` — до `maxReasons` мотивов с весом выше 1 → `LeaveCause` (по состоянию: пустой кошелёк,
  рана, срыв за `recentBreakdownDays`); ничего — `LowLoyalty`. `Text` — шаблоны `reason.*` из набора шаблонов ленты
  (самый конкретный — условие «черта раскрыта» называет черту; первый вариант, без бросков), `PersonTextSource` даёт `{имя}`.
  Событие `AdventurerLeft`: `cause`, `causes`, `loyalty`, `reason` (текст); строка «{имя} ушёл из гильдии: {причина}».
  Валидатор требует шаблон на каждый `LeaveCause` (`LeaveReasons.Keys`).
- **Движок текстов** (доработка GM-07): `[…]@метка`, которой нет в строке, берёт род из источника (`TextRenderer`); валидатор
  разрешает это только текстам `reason.*`. `{причина}` — общая метка ленты и дилемм.
- **`RelationBook`**: ключ пары хешируется с перемешиванием (`PairKeyComparer`) — при сотнях людей в таверне (сотни тысяч пар)
  стандартный хеш `long` вырождал словарь.
- **Сводка**: «Отдых, %» и «Таверна, %» — доля часов людей в гильдии за месяц (`MonthRecord.ActivityShare`).
- **Что поменялось в поведении GM-05**: вечером в таверну идут ~80% людей (лучший вариант), а не все; таверна — с 19:00
  (решение в 18:00 действует со следующего часа); Пьяница ходит в таверну днём и сам; сдвинулись броски `ActivitySystem`
  (выпивка) и `StateSystem` (срывы и уход зависят от стресса и порядка бросков). Тесты GM-05, которые проверяют расходы
  и расписание, ставят `decisions.bestChoiceChance = 1` (вечером всегда таверна); пропуск дня Пьяницы считается
  по `SkipsDayUntilHours`.

## Экономика гильдии (GM-09)

Числа — `BalanceSettings.Economy` (комиссия и пределы, `tavernIncomePerServing`, `bankruptcyStartDays`, `bankruptcyMonths`,
`closedBestPeople`), `Guild.startMoney`; пороги черт — `Traits.testedMinDays/testedMinLoyalty`, `Adventurers.loyalStayLoyaltyBelow`.
Решение 2026-09-27 в `docs/decisions.md`.

- **Мир.** `World.Treasury` (`Treasury`): `Money` (может быть < 0), `StartMoney` (начальный остаток, не запись журнала),
  `Ledger` (`LedgerEntry`: время, статья, сумма со знаком, комментарий, `RelatedId`, `BalanceAfter`), `Commission`,
  `Bankruptcy` (`NegativeSinceHours`, `Active`, `StartedAtHours`, `EndsAtHours`), `IsClosed` + `ClosedAtHours`.
  Инвариант: `StartMoney` + Σ журнала = `Money`. `World.Reports` (`MonthReportHistory`) — отчёты месяца, `GetLast()`.
- **Статьи журнала** — `LedgerCategory` (код, `LedgerFlow` доход / расход, `ExpenseKind`: `Mandatory` — в минус,
  `IfAffordable` — не списывается без денег). **Новая статья — поле и строка в `LedgerCategories.All` + шаблон `ledger.{код}`
  в генераторе** (валидатор требует). Сейчас — `Tavern`. Расходов в игре пока нет; тесты заводят свои статьи (`TestLedger`).
- **`TreasuryService`** — единственный путь изменить деньги: `Credit(ctx, статья, сумма, комментарий, связанный)`,
  `Debit(…)` → bool; каждая запись — лог `Debug` (`ledger tavern +3 money=2014 food=4 drinks=2`); отмечает, с какого часа
  казна в минусе (стартовый минус — с начала игры). Таверна: `CountTavernFood/Drink` копят порции, `SettleTavern` в 00:00 —
  одна запись за сутки, целые монеты, дробный остаток переносится.
- **`EconomySystem`** (шаг 11, свой поток, бросков нет): в 00:00 — доход таверны; каждый час — банкротство (30 × 24 часа в минусе
  → `BankruptcyStarted` [В], автопауза, срок `bankruptcyMonths` × `HoursPerMonth`; казна ≥ 0 или минус начался заново →
  `BankruptcyLifted` [З]; срок истёк в минусе → `GuildClosed` [В]: участники — лучшие люди, `days`, `died`, `left`, `money`);
  в начале месяца — `HardTimes`.
- **`HardTimes`**: `IsNow` — банкротство или уход (`LeaveReason.Left`) за месяц до этого часа включительно (не раньше начала игры);
  `Apply` — «Проверенный» (`TraitHook.TestedOnAcquire`) у тех, кто в гильдии ≥ 180 суток и с лояльностью ≥ 50; раскрытие
  Преданного (`RevealTrigger.LoyalStayedInHardTimes`) у тех, у кого лояльность ниже 40; лог `Info` `hard times: …`.
- **Комиссия** — `SetCommissionCommand` (обрезка до `commissionLimits`, то же значение — без события, смена — `CommissionChanged` [О]).
- **Отчёт месяца** — `MonthReportSystem` (перед `FeedSystem`): в 00:00 первого числа `Build` → `ReportPeriod` (от прошлого отчёта до
  текущего часа включительно; журнал — по индексам; уже попавшее в прошлый отчёт не повторяется; стартовый состав — не новость)
  → разделы `MonthReportSections.Default` (заголовок, ключи подписей, сборка строк). **Новый раздел — строка в `Default`**,
  его ключи попадают в `TextKeys` (валидатор требует шаблоны). «Деньги» — из журнала; «Люди» — пришли, ушли, погибли, пропали,
  изгнаны, раскрылись (время раскрытия — `TraitInstance.RevealedAtHours`, `Adventurer.GetAxisRevealedAtHours`, ставит
  `RevealService`). `ReportLine` — подпись и сумма (`Signed`) или люди (`ReportItem`: id и подробность `trait:{id}` /
  `axis:{ось}:{полюс|Balanced}`). Текстом — `MonthReportText.Lines` (подписи — первый вариант шаблона); в лог `Info`
  (`report 1.1: Таверна: +14`); событие `MonthReportReady` [З] без автопаузы.
- **Что поменялось в GM-05–08**: доход таверны идёт в казну; цель довольства — текущая комиссия; бот «Простой» шлёт команду комиссии
  (в сценарии и логе — строка `send commission 0.2`); лог `Info` — +8 строк отчёта в месяц. Броски чужих систем не сдвинуты
  (тест `EconomySystems_DoNotShiftOtherSystems`); тест `AutopauseSystem_DoesNotShiftOtherSystems` отбрасывает строки
  `MonthReportSystem` — отчёт пишется и в пустом мире.
- **Прогон года** (10 зёрен, «Простой»): казна 2000 → ~2040; таверна +14 в первый месяц, дальше 0–7 — без заданий кошельки пустеют
  и в таверне почти не едят и не пьют. Трудных времён нет (никто не уходит, банкротства нет).

## Заказы, доска и репутация (GM-10)

Числа — `BalanceSettings.Orders` (поток, смесь рангов, «сложный не по времени», доли дальних, порог требований, множитель осей,
награда, намёки, точность описания, порог важного, срок ответа, стартовые заказы, предел архива), `Ranks` (оси и награды по рангам),
`Guild` (репутация, приток). Доли типов — `QuestTypeDefinition.generationWeight`. Решение 2026-09-27 в `docs/decisions.md`.

- **Мир.** `World.Guild` (`GuildState`): `Reputation`, `StartReputation`; меняет только `ReputationService.Change(ctx, Δ, причина)` —
  обрезка 0..`maxReputation`, событие `ReputationChanged` (без строки). В GM-10 репутация не меняется. `World.Orders` (`OrderBoard`):
  `Open` (на доске и ждущие игрока), `Closed` (отклонённые и снятые, не больше `closedOrdersLimit`), `Rules` (`RegistrarRules`:
  типы или все, ранг до, минимум награды; по умолчанию — все, до C, 0), `Totals` (`OrderTotals` — счётчики за игру, без
  стартовых), `TryGetOpen`, `TryGetOrder`, `GetPromisedSurcharges` (доплаты на доске). Свой счётчик id.
- **`Order`**: тип, заказчик, место, враг, груз (`NounForms`), ранг, расстояние, описание, `HintStats`, награда, доплата, статус
  (`Incoming` → `OnBoard` / `AwaitingPlayer` → `Declined` (+ `DeclinedBy`: `Registrar`, `Player`, `NoAnswer`) / `Expired`),
  `IsImportant`, `IsHardEarly`, часы прихода, повешен, снимут, ответ до, закрыт. **Скрытое — `internal`**: профиль
  (`Requirement(StatId)`, по всем 14 параметрам, у Слаженности 0 — `Vocabulary.IsDiagramAxis`), потолки, `DescriptionAccuracy`.
- **`OrderGenerator`** (internal, чистые функции от потока): `Generate` (порядок бросков — в комментарии класса, не менять без
  причины), `RankMixFor`, `OrdersPerDay`, `BuildProfile` («тип + ранг → профиль»: главные / второстепенные из диапазона ранга,
  далеко без Выживания — Выживание второстепенной, × множитель, все оси диаграммы не ниже `requirementFloor`), `Reward`
  (× дальность, округление), `LargestAxes`, `IsImportant`. Описание — шаблон типа (`OrderTextSource` даёт метки) + намёки на
  1–2 самые большие оси + «Путь неблизкий», одинаковые предложения склеиваются.
- **`OrderSystem`** (шаг 3, свой поток): каждый такт — снять заказы с вышедшим сроком (`OrderExpired`) и отклонить важные без
  ответа (`OrderDeclinedByPlayer`, `noAnswer`); в начале утра — число по репутации (дробная часть — бросок `extra-order` всегда),
  каждый: `OrderArrived` → важный — `AwaitingPlayer` ([В], автопауза, ответ до `playerResponseDays`), иначе по правилам —
  `OrderPosted` или `OrderDeclinedByRegistrar`; в конце утра — `NewOrdersPosted` / `RegistrarDeclinedOrders` (`count`) для ленты.
  Id заказа — в данных события (`order`), не в участниках. `ApplyStart` — стартовые заказы (поток `OrderStart`, без событий).
- **Команды**: `SetRegistrarRulesCommand` (те же правила — без события, смена — `RegistrarRulesChanged`),
  `AnswerImportantOrderCommand` (принять — на доске с этого часа, срок от принятия, доплата; отклонить — `Declined` / `Player`),
  `SetSurchargeCommand` (только на доске, ≥ 0, из казны не списывается, `SurchargeSet`).
- **Отчёт месяца**: разделы «Заказы» (числами — разница счётчиков с прошлым отчётом; `ReportLine.Number`) и «Репутация»
  («За месяц: 5 → 5» — `ReportLine.Change`). `MonthReport` хранит `ReputationAtEnd` и `OrdersAtEnd`.
- **Что поменялось в GM-04–09**: шанс кандидата — от текущей репутации (пока равна стартовой — броски те же); `StateWorld` в тестах
  убирает `OrderSystem` (тесты состояния видят только своих людей); тест раскрытия нейтральных осей выключает автопаузу важного
  заказа; тест бота «Простой» считает только команды `accept` (в сценарии теперь и `registrar`, `answer`); отчёт месяца длиннее
  на 8 строк. Чужие броски не сдвинуты (тест `OrderSystem_DoesNotShiftOtherSystems`).
- **Прогон года** (10 зёрен, «Простой»): ~1,5 заказа в день (~45 в месяц), почти все снимаются по сроку — заданий ещё нет;
  важных ~27 в год, все уходят без ответа (высший ранг людей — F, бот ждёт человека нужного ранга).

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
  `DilemmaTrigger`. Данные говорят «какое правило», код реализует его в своей системе (ТЗ 04, 11, 17).
- **Тексты**: подстановки `{имя}`, `{место:р}`, род `[м|ж]`, `[его|её]@имя` (ТЗ 07). Словарь меток —
  `TextPlaceholders`. У имён, мест, врагов, построек, распоряжений — `NounForms` (6 падежей и род `GrammaticalGender`);
  у имён `NameList` и у названий заказов (заказчики, места, враги, грузы) заполнены все шесть форм (валидатор требует). Падежи в шаблонах расставлены. Скобка рода у названий — `[м|ж|ср|мн]@постройка` (решение 2026-09-26). Ключи ленты (`quest.departed`, `reveal.risk.negative`…)
  — строки; определения ссылаются на них полями `…FeedKey`.
- **Валидатор** — GuildMaster → Validate Data (`DataValidator.Validate(config)`): обход сериализуемых полей
  отражением (пустые ссылки, кроме `[OptionalReference]`; `[Range]`/`[Min]`; `IntRange`/`FloatRange` с min > max),
  дубли id, связи (постройка ↔ должность, ключи ленты, шансы исходов в сумме 1…), разметка текстов.
  Ошибка — данные битые; предупреждение — подозрительно (сейчас 0). Проверяет и то, что у каждого ключа `FeedKeys.Fixed`
  есть шаблон, у оси — `balancedFeedKey`, у имён — все шесть падежей.
  Консоль MCP видит итоговую строку только в `read_console` с `types: ["all"]`; полный отчёт — вывод теста
  `DataValidatorTests.RealConfig_HasNoErrors`.
- **Ассеты заполняет генератор в песочнице** (GuildMaster → Sandbox → Generate Game Data); он перезаписывает
  определения и тексты, баланс не трогает.

## Тесты

`Tests/EditMode`: `RngTests`, `TimeTests`, `DayRhythmTests`, `GameClockTests`, `AutopauseTests`, `SimulationTests`,
`DeterminismTests`, `ArchitectureTests`, `DataTests.cs` (`DataValidatorTests`, `DataRegistryTests`, `BalanceRuntimeTests`);
GM-04: `AdventurerTests.cs` (`AdventurerGenerationTests`, `ArchetypeTests`, `StartScenarioTests`), `TraitTests`,
`GrowthTests.cs` (`GrowthTests`, `GuildRankTests`, `RelationTests`), `RecruitTests.cs` (`RecruitTests`,
`PeopleDeterminismTests` — детерминизм, чужие потоки не сдвигаются, 360 дней с людьми);
GM-05: `StateTests` (старт, занятия, обрезка, расписание, усталость, срывы и их частота, довольство, лояльность, уход,
слова), `WalletTests`, `HealthTests` (сроки с Лазаретом и без, койки, осложнения, увечье), `StateTraitTests.cs`
(`StateTraitTests` — черты и стресс от событий, `StateDeterminismTests` — чужие потоки, детерминизм с ранами,
год с 30 людьми);
GM-06: `LoggingTests.cs` (`SimLoggerTests` — формат, уровни, выключенный уровень не форматирует, событие после бросков;
`SimulationLogTests` — лог не меняет мир, одно зерно + бот = один лог, сценарий повторяет игру, по логу видно,
почему срыв и уход), `HeadlessRunTests.cs` (`HeadlessRunTests` — боты, год меньше минуты, Info против Trace по цене лога,
сводка, свёртка, файлы; `ScenarioScriptTests` — разбор, запись, ошибки с номером строки);
GM-07: `FeedTests.cs` (`TextRendererTests` — падежи, правило рода, `@`, четыре рода названий, метка без источника;
`FeedTemplateTests` — все шаблоны × 2 пола × 4 рода названий без разметки и склеек, выборочные строки, шесть падежей у имён,
черта в строке только после раскрытия, архетип; `FeedSystemTests` — событие → строка нужной важности со ссылками, срывы,
раскрытия, варианты не повторяются, лог, лимит, год: каждое событие с ключом даёт строку; `FeedDeterminismTests` — одно
зерно = одна лента, лента не сдвигает чужие броски, сводка);
GM-08: `DecisionTests.cs` (`DecisionTests` — точки и лог, действует со следующего часа, срыв и «освободился», 80/20;
`DecisionMotiveTests` — усталые: Командный и стресс → таверна, днём — Пьяница и стресс > 60, веса, оценки таверны;
`LeaveReasonTests` — скрытая / раскрытая черта, род, состояние, событие и строка ухода, Наёмник; `TavernEveningTests` — +0,5,
ссоры 5% только у пар ниже −20, Соперники; `DecisionDeterminismTests` — одно зерно = один лог и лента, приток кандидатов
не сдвигается);
GM-09: `EconomyTests.cs` (`TreasuryTests` — старт, запись журнала, виды расхода; `CommissionTests` — обрезка, довольство выше 25%;
`TavernIncomeTests` — 0,5 с порции и перенос остатка, раз в сутки, пустой кошелёк; `BankruptcyTests` — начало через 30 суток
с автопаузой и строкой, сброс счётчика, снятие, закрытие по сроку и остановка, досрочный конец прогона; `MonthReportTests` —
отчёт совпадает с журналом, люди и раскрытия, строка ленты, следующий отчёт продолжает; `HardTimesTests` — Проверенный и Преданный
при уходе и банкротстве, без трудных времён ничего; `EconomyDeterminismTests` — одно зерно = та же казна, журнал, отчёты, лента;
чужие броски не сдвигаются; сводка года; `EconomyScenarioTests` — команда `commission`, бот «Простой»);
GM-10: `OrderTests.cs` (`OrderGenerationTests` — смесь рангов по репутации, 5% «сложных», доли типов и расстояний, профиль по шаблону
и порог, награда, намёки на самые большие оси, без повторов, важность, точность, скрытое не видно; `OrderBoardTests` — старт без
событий и без сдвига людей, число заказов по утрам, Регистратор по правилам и строки утра, важные — игроку, автопауза, отказ без
ответа, ответ и доплата, снятие по сроку, предел архива, репутация, шанс кандидата от репутации, отчёт; `OrderDeterminismTests` —
одно зерно = те же заказы и лента, чужие броски не сдвигаются; `OrderScenarioTests` — команды сценария, бот «Простой», сводка,
повтор сценария; бот ждёт человека нужного ранга). Всего 335.
Помощники ленты — `DictionarySource` (метки из словаря), `FeedTestTemplates` (шаблоны в памяти).
Числа баланса в тесте меняются `GameData.Edit(data.Balance, "time.dayHour", p => p.intValue = 10)`.
Помощники — `TestSupport.cs`: `TestData` (BalanceSettings без ассета), `NoiseSystem`, `TraceCommand`,
`SimulationLog.Record` (лог событий прогона строкой), `ActionCommand` и `SimulationRun` (`Do` — вызвать службу Core
как команду на паузе, `Days`, `Collect` — события прогона); `StateTestSupport.cs`: `StateWorld` (мир без стартовой
шестёрки, по желанию со своим `SimLogger`; `Add(черты…)` — ровный человек, `TickToHour`, `Collect`), `LambdaSystem` (код в своём месте такта),
`FakeInfirmary`, `Frequency.Tolerance` (допуск частоты — 3σ биномиального числа успехов); `DataTests.cs`: `GameData` — реальный `GameConfig`
и копии ассетов в памяти для порчи (`Copy`, `Edit` через `SerializedObject`); `PeopleTestData.cs`: `PeopleData` —
определения из реального `GameConfig` + `BalanceSettings` по умолчанию (`Set(путь, число)`); `PeopleDump` — состав строкой
(с состоянием и ранами).
Запуск — Test Runner или MCP `run_tests`.
