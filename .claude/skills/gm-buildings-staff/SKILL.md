---
name: gm-buildings-staff
description: Постройки и персонал GuildMaster — Building/BuildingBook (стадии, очередь, одна стройка), BuildingService и BuildingSystem (стройка, Общежитие, тренировка на дворе, содержание), BuildingRules (готова ли постройка по назначению BuildingFunction, места и занятость), BuildingInfirmary (настоящий Лазарет), команды StartBuilding/ReorderBuildQueue/CancelBuilding; StaffMember/StaffCandidate/StaffRoster, StaffService, StaffSystem и SalarySystem (кандидаты на вакансии, переговоры, найм, увольнение, зарплаты и долг), StaffRules (эффект уровня, просимая зарплата, шанс согласия), команды OfferSalary/DismissStaff. Использовать при правке построек, мест, Лазарета, двора, Общежития, персонала, зарплат и их строк ленты и отчёта.
---

# Постройки и персонал

Код — `game/Assets/_Project/Scripts/Core/Buildings/` и `Core/Staff/`. Дизайн — `docs/mechanics/buildings.md`, `staff.md`,
`economy.md`. Числа — `BalanceSettings.Staff`, `Expenses` (платы), `Growth.trainingHoursPerDay`; цены, сроки, содержание,
вместимость — в определениях `BuildingDefinition`, базовые зарплаты — `StaffRoleDefinition` (генератор песочницы).

## Карта

| Файл | Что |
|---|---|
| `Buildings/Building.cs` | `BuildingState` (Planned, UnderConstruction, Ready), `Building` (стадия, сроки, `PaidCost`, `Level` = 1), `BuildingBook` (`All`, `Queue`, `Current`, `TryGetByDefinition`, `TryGetById`, `IsReady`); свой счётчик id |
| `Buildings/BuildingRules.cs` | чистые вопросы: `IsReady(world, data, BuildingFunction)`, `Capacity`, `DormitoryResidents`, `Patients`, `Trainees(world, except)`, `MonthlyUpkeep` |
| `Buildings/BuildingService.cs` | `ApplyStart` (стартовые постройки без событий), `Enqueue`, `Cancel`, `Reorder`, `TryStartNext`, `CompleteIfDue`, `SettleDormitory`, `PayUpkeep` |
| `Buildings/BuildingSystem.cs` | шаг 12: стройка, Общежитие (00:00 и час готовности), час тренировки (`Growth.Train`, выбор параметра — свой поток), содержание 1-го числа |
| `Buildings/BuildingCommands.cs` | `StartBuildingCommand`, `ReorderBuildQueueCommand`, `CancelBuildingCommand` |
| `Health/Infirmary.cs` | `IInfirmary`, `BuildingInfirmary` — койки = вместимость готовой постройки с назначением Infirmary, Лекарь = сотрудник с эффектом `HealingSpeed` |
| `Staff/StaffMember.cs` | `StaffLeaveReason`, `StaffMember` (уровень, зарплата, `UnpaidSalary`, `UnpaidMonths`), `StaffCandidate` (просимая, срок ожидания), `StaffRoster` (`Members`, `Former`, `Candidates`, когда следующие кандидаты на должность); свой счётчик id |
| `Staff/StaffRules.cs` | `LevelEffect`, `EffectMultiplier` (нет человека — уровень 0), `TryGetWithEffect`, `AskedSalary`, `AcceptChance`, `IsVacant`, `MonthlySalaries` |
| `Staff/StaffService.cs` | `ApplyStart` (поток `StaffStart`), `AddCandidates`, `Offer` (бросок — поток команд), `Dismiss`, `ExpireCandidates`, `RepayDebts`, `QuitUnpaid`, `PaySalaries`; причины ухода кандидата `CandidateExpired/Refused/PlaceFilled` |
| `Staff/StaffSystem.cs` | `StaffSystem` (шаг 12, до построек: кандидаты, 00:00 — долги, 1-го — уход) и `SalarySystem` (после построек: зарплаты 1-го) |
| `Staff/StaffCommands.cs` | `OfferSalaryCommand`, `DismissStaffCommand` |

## Порядок в такте (шаг 12)

`EconomySystem` (банкротство) → **`StaffSystem`** → **`BuildingSystem`** → **`SalarySystem`** → `AdventurerSystem`…
1-го числа в 00:00 выходит: долги по зарплате (если хватает) → уход тех, чей долг висит `unpaidMonthsToQuit` сроков →
содержание (обязательный, даже в минус) → зарплаты по порядку найма (только если хватает, иначе долг и [В]).
Всё это попадает в отчёт **прошедшего** месяца (`MonthReportSystem` позже в такте).

## Правила

- **Стройка**: `StartBuilding` всегда ставит в очередь; голова начинается, когда нет стройки и денег ≥ цены (списание
  `Construction`, `IfAffordable`). Строгий порядок. Каждая постройка — один раз. Готово — `BuildingReady` [З].
- **Назначение** (`BuildingFunction` в определении) — правила находят постройку по нему (`DataRegistry.BuildingWith`); валидатор
  требует не больше одной постройки на назначение.
- **Места — у людей**: жильё `Adventurer.Housing`, койка `AdventurerState.InInfirmary`, двор — `(PlannedActivity ?? Activity) == Training`.
  Считаются только активные.
- **Общежитие**: переселяет дольше служивших (затем меньший id); плата за жильё — первой из суточных, в казну (`Dormitory`,
  `WalletService.PayDaily`); довольство — цель по жилью (`StateRules.ContentmentTarget`).
- **Лазарет** (`HealthSystem`, каждый час): койка до выздоровления; тяжёлых кладут сами (очередь: раньше раненый, затем id);
  лёгкий ложится решением `Heal`; тяжёлый не вытесняет лёгкого; без Лекаря / при нехватке коек лишние выходят. Плата 5 — в первый
  час занятия «Лазарет» (`ActivitySystem`), в казну сколько заплачено (`Infirmary`), недостача — недополученный доход.
- **Двор**: `Train` в точках «утро»/«освободился»; занятие `trainingHoursPerDay` часов (`ActivitySystem.Train`: начало — плата 2
  в казну `TrainingYard`, `TrainedOnDay`, событие `TrainingStarted`; конец — `Resting` и `WasFreeLastHour = false` → точка
  «освободился» в том же часу). Прибавка — `BuildingSystem`, по часу.
- **Персонал**: один человек на должность; вакансия — должность без человека при готовой постройке; кандидаты — в первое утро вакансии
  и дальше каждые `candidateIntervalDays`; после найма остальные кандидаты на должность уходят (`cause=filled`, без строки).
  Трактирщик — `StateService.ApplyHour` (снятие стресса в таверне × эффект), Лекарь — `BuildingInfirmary.HealingSpeed`.
  Регистратор — без эффекта.
- **Лента**: у событий персонала участников нет — `{имя}` берётся из данных события `staff` (`EventTextSource`), `{постройка}` —
  `building`. Ключи — `FeedKeys.Building*`, `StaffCandidate*`, `Staff*`, `SalaryUnpaid`. `StaffQuit` — автопауза `MemberLeftGuild`.
- **Отчёт месяца**: разделы `report.buildings` и `report.staff`; строки-постройки и строки-сотрудники — `ReportItem` с подробностью
  `building` / `staff` (id в строке — id постройки / сотрудника), текст — `MonthReportText`.

## Рецепты

- **Новая постройка** — определение в генераторе (`FillBuildings`: назначение, цена, срок, содержание, вместимость, род названия),
  при своём правиле — значение `BuildingFunction` в конец и код правила через `BuildingRules.IsReady(…, function)`.
- **Новая должность** — `FillStaff` (базовая зарплата, постройка, `StaffLevelEffect`); эффект — значение `StaffLevelEffect` в конец
  и множитель через `StaffRules.EffectMultiplier`.
- **Улучшение уровня** (отложено) — `Building.Level` уже есть; цена/срок — `levelCostMultiplier`/`levelTimeMultiplier` определения.

## Детерминизм и ловушки

- Пустой случай бросков не тратит: без двора и вакансий `BuildingSystem`/`StaffSystem` не бросают (тест
  `NewSystems_WithoutBuildingsAndVacancies_DoNotShiftOtherRolls`).
- Стартовый персонал — поток `StaffStart`, свои id: люди и заказы не сдвигаются. Но **уровень Трактирщика меняет снятие стресса**
  в таверне (× 0,92–1,08) — поведение людей сдвигается против прогонов до построек. `StateWorld` ставит стартовому персоналу
  уровень 50 (× 1).
- Варианты `Train`/`Heal` добавляются, только когда двор готов / есть рана, Лекарь и свободная койка — иначе они сдвинули бы
  броски `decision-best`.
- `HealthSystem` и `DecisionSystem` берут один `IInfirmary`; тест со своим Лазаретом подменяет его в обеих (`StateWorld(infirmary:)`).

## Бот и сводка

Бот «Простой»: `BuildWithReserveRule` (Общежитие → Лазарет → двор, запас 3 месяца расходов), `HireBestCandidateRule`. Сценарий:
`build Id`, `buildqueue 1,2`, `unbuild Id`, `offer IdКандидата сумма`, `dismiss IdСотрудника`. Сводка: «Построек готово», «Построено»,
«В Общежитии», «В Лазарете, %», «Легли в Лазарет», «Вылечено в Лазарете», «На дворе, %», «Тренировок», «Персонал», «Нанято»,
«Персонал ушёл», «Невыплат жалованья», «Долг по зарплате» + статьи журнала.

## Тесты

`Tests/EditMode/BuildingStaffTests.cs` — помощники `Ready(world, id)` (постройка готова в обход стройки), `Hire(world, role, level)`.
