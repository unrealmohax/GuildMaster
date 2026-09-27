# Код игры: архитектура

Правила работы — в корневом `CLAUDE.md`. Здесь — устройство кода игры. Задания — `TechJob/01-architecture.md`
(GM-01, архитектура), `TechJob/02-data.md` (GM-02, данные), `TechJob/03-time.md` (GM-03, время),
`TechJob/04-adventurers.md` (GM-04, авантюристы), `TechJob/05-state-health.md` (GM-05, состояние и здоровье),
`TechJob/06-debug-logging.md` (GM-06, лог и прогон без интерфейса), `TechJob/07-event-feed.md` (GM-07, лента событий);
этот файл описывает, как они реализованы.

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
│   │   ├── Feed/        FeedSystem, FeedState + FeedEntry (лента в мире), FeedKeys (событие → ключ), FeedConditions,
│   │   │                TextRenderer + TextValue + ITextSource (движок подстановки), EventTextSource (метки из события)
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
новая система встаёт на своё место в `CreateDefault()`. Сейчас реализованы шаги 1–2, 5–7, 13, 15 и 16: `CommandSystem`,
`TimeSystem`, `ActivitySystem`, `StateSystem`, `HealthSystem`, `AdventurerSystem` + `RecruitSystem` (шаг 13 дополнен
`AdventurerSystem`, решение 2026-09-26), `FeedSystem` (после всех систем, которые публикуют события), `AutopauseSystem` (всегда последняя). Тесты, которые добавляют системы в конец `CreateDefault()`,
ставят их после автопаузы — для проверки случайности это неважно. **Стартовое состояние** готовит конструктор
`Simulation` до первого такта: `StartScenario` со своим потоком `StartScenario` (стартовые люди), событий не пишет.

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
  → `TraitRevealed`) и уход из гильдии (`AdventurerLeft` → `MemberLeftGuild`); остальные события таблицы ТЗ 03
  появятся в ТЗ 09, 11, 13, 17.

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
- **Кандидаты** — `RecruitSystem`: утром (`morningHour`) раз в сутки шанс `CandidateChance` (заглушки: репутация
  стартовая до ТЗ 10, распоряжений нет до ТЗ 16), ожидание `candidateWaitDays`, `CandidateArrived` / `CandidateLeft`;
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
  До ТЗ 08 его ставит **`ActivitySystem`** (шаг 5) по расписанию-заглушке, первое подходящее: на задании — не трогает;
  запой — `Binge`, «сел и не смог подняться» — `Resting` весь срок; ночь — `Sleeping`; койка — `Infirmary`; тяжёлая рана —
  `Resting`; Пьяница, пропускающий день, — `Tavern` с утра; вечер — `Tavern`, утро и день — `Resting`. Там же расходы
  занятия раз в сутки (выпивка 2–5 при стрессе > 30 или Пьянице, еда в таверне — если кошелёк ≥ расходов на неделю;
  запой — 5; Лазарет — 5) и утренний пропуск дня Пьяницы (10% / 20% при стрессе > 50, раскрывает черту).
- **`StateSystem`** (шаг 6): каждый час — усталость и стресс по таблице занятий (`StateRules.FatiguePerHour` /
  `StressPerHour`) × эффекты черт; в 00:00 по каждому — расходы на жизнь (`WalletService.PayDaily`: еда 2 / 3 в таверне,
  жильё), довольство к цели (`StateRules.ContentmentTarget`) на 1, лояльность к довольству на 0,1, сброс учёта суток,
  срыв (стресс > 80, 10%), конец спада Потерявшего товарища (30 дней → раскрытие, «сломался» / «ожесточился», черта
  снимается); в начале месяца — проверка ухода (лояльность < 25, 20%, Семейный × 1,5 → `AdventurerLifecycle.Retire`,
  `AdventurerLeft` [В] с автопаузой, причина `LeaveCause.LowLoyalty`). Люди на задании не срываются и не уходят (ТЗ 11).
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
- **Заглушки** (решение 2026-09-26): платы гильдии не зачисляются в казну (таверна — ТЗ 09, Общежитие, двор, Лазарет — ТЗ 13; места в коде — с комментарием «казны пока нет»), комиссия —
  `Economy.defaultCommission`, Лазарета нет, причина ухода — без мотивов. Без заданий (ТЗ 11) доходов нет: за 2–3 недели
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
  у имён `NameList` заполнены все шесть форм (валидатор требует), у названий заказов — пока только именительный. Падежи в шаблонах расставлены. Скобка рода у названий — `[м|ж|ср|мн]@постройка` (решение 2026-09-26). Ключи ленты (`quest.departed`, `reveal.risk.negative`…)
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
зерно = одна лента, лента не сдвигает чужие броски, сводка). Всего 253.
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
