---
name: gm-testing
description: Тесты GuildMaster (EditMode, сборка GuildMaster.Tests) — тестовые миры и помощники TestData, PeopleData, StateWorld, QuestWorld, PartyTestSupport, SimulationRun, LambdaSystem, FakeInfirmary, Frequency, GameData; шаблоны тестов детерминизма («одно зерно — один лог», «не сдвигает чужие броски»), архитектурные тесты, запуск через Test Runner или MCP run_tests. Использовать при написании или починке тестов и при выборе, какой тестовый мир взять.
---

# Тесты

Код — `game/Assets/_Project/Tests/EditMode/`. Сборка видит internal Core (`InternalsVisibleTo`) — тест может создать `Adventurer`,
поставить сеттер, вызвать internal-службу. Запуск — Test Runner или MCP `run_tests` (EditMode; `include_details` для вывода).

## Какой мир взять

| Помощник (файл) | Когда |
|---|---|
| `TestData` (`TestSupport.cs`) | только `BalanceSettings` без ассета — время, случайность, ядро; людей и заказов нет |
| `PeopleData` (`PeopleTestData.cs`) | реальные определения из `GameConfig` + баланс **по умолчанию** в памяти; `Set("раздел.поле", число)`; `NoQuests()` — люди не берут заказов; `NoBankruptcy()` — огромная стартовая казна: без заданий содержание и зарплаты разоряют гильдию примерно за 10 месяцев, и долгий прогон встаёт (тесты частот за годы) |
| `StateWorld` (`StateTestSupport.cs`) | мир без стартовой шестёрки и без `OrderSystem`, стартовые заказы сняты; `Add(черты…)` — ровный человек (параметры 30, оси 0, денег много); `AddMany`, `Do(ctx => …)`, `TickToHour`, `Days`, `Collect`; параметры `infirmary` (подменяет Лазарет и в `HealthSystem`, и в `DecisionSystem`), `beforeState` (система перед `StateSystem`), `log`. Стартовые постройки (Зал, Таверна) и персонал (Регистратор, Трактирщик) остаются, персоналу — уровень `NeutralStaffLevel` 50 (эффект × 1); 1-го числа — содержание 90 и зарплаты |
| `QuestWorld` (`QuestTests.cs`) | поверх `StateWorld`: `AddOrder(тип, ранг, требование, награда, расстояние)`, `Start(заказ, люди…)`, `StartParty`, `MakePermanent`, `AtSite(run, провалов)`, `RunToEnd`, `DoEvents` |
| `PartyTestSupport.Person` (`PartyTests.cs`) | человек с заданными параметрами, Слаженностью и рангом |
| `SimulationRun` (`TestSupport.cs`) | `Do` — вызвать службу Core как команду на паузе (`ActionCommand` + `ApplyCommandsNow`), `Days`, `Collect` — события прогона |
| `SimulationLog.Record` | лог событий прогона строкой — для сравнения двух прогонов |
| `LambdaSystem` | код в своём месте такта (держать стресс, подменить состояние) |
| `FakeInfirmary` | Лазарет с койками, Лекарем и скоростью, заданными прямо, — для тестов здоровья без построек и персонала. С постройками — помощники `Ready`/`Hire` в `BuildingStaffTests` |
| `Frequency.Tolerance(испытаний, шанс, σ)` | допуск частоты случайного события (3σ биномиального) |
| `GameData` (`DataTests.cs`) | реальный `GameConfig`, копии ассетов в памяти для порчи: `Copy`, `Edit(объект, путь, p => …)` |
| `NoiseSystem`, `TraceCommand` | системы и команды, которые тратят броски — для тестов детерминизма |
| `DictionarySource`, `FeedTestTemplates` | метки из словаря и шаблоны в памяти — тесты текстов |

## Шаблоны тестов

- **Мутация мира в тесте** — только через `world.Do(ctx => Служба.Метод(ctx, …))` (нужен `SimContext`); прямые internal-сеттеры
  допустимы для подготовки состояния. `Do` идёт как команда на паузе — `ctx.Rng` там поток `CommandSystem`, а не системы,
  которая вызывает службу в игре.
- **Решение действует со следующего часа** — после действия, которое ставит `PlannedActivity`/`PlannedOrderId`, тикнуть ещё раз.
- **Событие на паузе** лежит в шине до следующего такта — `QuestWorld.DoEvents` или `Collect`.
- **Детерминизм**: два мира с одним зерном и одинаковыми командами → одинаковый `SimulationLog.Record`/лента/казна.
- **«Не сдвигает чужие броски»**: прогон с новой системой/механикой и без неё — логи чужих систем совпадают (отбросить строки
  своей системы и `MonthReportSystem`, который пишет и в пустом мире). Примеры: `OrderSystem_DoesNotShiftOtherSystems`,
  `EconomySystems_DoNotShiftOtherSystems`, `QuestSystem_WithoutQuests_DoesNotShiftOtherSystems`, `NoPartyPossible_NoRolls_SameLogAsWithoutParties`.
- Тесты, которые добавляют свои системы в конец `CreateDefault()`, ставят их после `AutopauseSystem` — для проверки
  случайности это неважно, но события такой системы автопаузу и ленту не пройдут.
- **Частоты** — много испытаний, `Assert` в пределах `Frequency.Tolerance`.
- **Число баланса в тесте** — `data.Set("decisions.bestChoiceChance", 1f)` или `GameData.Edit(data.Balance, "time.dayHour", p => p.intValue = 10)`.
- Сообщения `Assert` — по-русски, без ссылок на документы и ТЗ.

## Где что проверяется

`ArchitectureTests` — ссылки asmdef, нет Unity-объектов и запрещённых API в Core, у частей мира нет публичных сеттеров
и мутирующих публичных методов (**новую часть мира — в список теста**). `DataTests.cs` — валидатор на реальном конфиге
(`RealConfig_HasNoErrors`), реестр, баланс. По подсистемам: `AdventurerTests`, `TraitTests`, `GrowthTests`, `RecruitTests`,
`StateTests`, `WalletTests`, `HealthTests`, `StateTraitTests`, `LoggingTests`, `HeadlessRunTests`, `FeedTests`, `DecisionTests`,
`EconomyTests`, `OrderTests`, `QuestTests`, `PartyTests`, `UiTests` (модели экранов, ссылки, отладочные команды, сборка `UiRoot` в EditMode); ядро — `RngTests` (эталонные числа PCG32), `TimeTests`, `DayRhythmTests`,
`GameClockTests`, `AutopauseTests`, `SimulationTests`, `DeterminismTests`.

## Когда тест упал после новой механики

Новая механика с бросками законно сдвигает мир. Прежде чем «чинить» старый тест, понять, что он проверяет:
- проверяет механику, которой новые люди/заказы мешают, — изолировать (`PeopleData.NoQuests()`, `StateWorld` без `OrderSystem`,
  выключить автопаузу, смотреть события только своего человека);
- проверяет конкретный бросок/число — пересчитать ожидаемое и объяснить в отчёте, почему сдвинулось;
- проверяет, что чужое **не** сдвинулось, — это настоящая ошибка новой механики.
Список изменённых старых тестов и причин — в отчёт пользователю и в сообщение коммита.
