---
name: gm-crises
description: Траты и кризисы GuildMaster — кризисы по календарю CrisisBook/Crisis/CrisisTotals, CrisisSystem (после поселения), CrisisService (расписание года, объявление, набег, налог, зима, мор), CrisisRules (идёт ли зима или мор, спад Безопасности, поток заказов, цены, налог, время года), раздел BalanceSettings.Crises, Арсенал (уровни, улучшение UpgradeBuildingCommand, снаряжение на задании GearLevel), проекты поселения (Частокол, Дозорная вышка, Колодец, Дорога), содержание за людей, бот «Умный», модели интерфейса CrisisModel (верхняя панель, календарь, вкладка «Поселение»), отчёт «Кризисы», столбцы сводки. Использовать при правке кризисов, проектов, Арсенала, налога, зимних цен и бота «Умный».
---

# Траты и кризисы

Код — `game/Assets/_Project/Scripts/Core/Crises/`, правила построек — `Core/Buildings/`, интерфейс — `UI/Models/CrisisModel.cs`. Дизайн —
`docs/mechanics/stakes.md` → «C. Деньги», «D. Кризисы»; ТЗ — `TechJob/24-spending-crises.md` (допущения реализации — в его конце).
Числа — `BalanceSettings.Crises` (`CrisesBalance`), цены, сроки, содержание, `maxLevel`, множители уровня — в определениях построек
(генератор). Таблица — `docs/content/numbers.md` → «Траты и кризисы».

## Карта

| Файл | Что |
|---|---|
| `Crises/CrisisBook.cs` | `CrisisKind` (Raid, Tax, Winter, Plague), `CrisisStatus` (Planned → Warned → Active → Over), `CrisisOutcome`, `Crisis` (год, `WarnAtHours`, `StartsAtHours`, `EndsAtHours`, `Amount` налога, `OrderId` набега, `WeeksApplied` мора, `IsKnown`), `CrisisTotals`, `CrisisBook` (`World.Crises`: `All` по началу, `PlannedYear`, `IsActive`, `TryGetActive`, `TryGetByOrder`, `GetNextWarned`) |
| `Crises/CrisisRules.cs` | без побочных эффектов: `IsWinter`, `WasWinterAt` (за прошедшие сутки), `IsPlague`, `DailyDecay`, `OrdersPerDay`, `LocalsTavernMultiplier`, `ExtraCalmGrowth`, `AttackPossible`, `AttackLoss`, `GearMultiplier`, `Tax`, `IncomeSince`, `SeasonOf` (`Season`) |
| `Crises/CrisisService.cs` | `PlanYear`, `MonthStart`, `Warn`, `Start` (набег — заказ, налог — списание, зима и мор — начало), `End`, `RaidOutcome`, `PlagueDay` |
| `Crises/CrisisSystem.cs` | после `SettlementSystem`, перед `EconomySystem`, свой поток: итоги набега по событиям такта → расписание нового года → объявить / начать / кончить → сутки мора |
| `Buildings/*` | `Building.UpgradingToLevel`/`IsUpgrading`, `BuildingService.EnqueueUpgrade`, `CostFor`/`DaysFor`, `PayUpkeep` (× уровень и «за людей» — `PeopleUpkeepComment`), `BuildingRules.PeopleUpkeep`/`LevelOf`, `UpgradeBuildingCommand` |
| `Settlement/*` | `SettlementService.ProjectReady` (Колодец, Дорога), `Attack` × Частокол, `MonthlyPopulationChange(…, extraCalmGrowth)`, причины `RaidWon/RaidLost/TaxUnpaid/Project/RaidFled/Plague` |
| `UI/Models/CrisisModel.cs` | время года, `TopLine` (верхняя панель), `Lines` (вкладка «Поселение»), `Projects`/`ProjectRow`, `ProjectEffect`, `ArmoryEffect`, `CalendarSource` |
| `Debugging/Headless/PlayerBot.cs` | бот «Умный»: `BuildWithTaxReserveRule`, `SurchargeThreatsRule`, предел людей `SmartPeopleLimit` |

## Ход

- **Расписание** — в первом такте года: набег (утро 1-го числа `raidMonth`), налоги (00:00 первого числа месяца после каждого из `taxMonths`),
  зима (`winterMonths`), мор — броски `plague` (шанс) и месяца. Всё — в `CrisisBook` сразу на год; запланированный мор не виден (`IsKnown`).
- **Объявление** за `warningDays` — `CrisisWarning` [В], автопауза `CrisisWarning`; у налога — сумма по доходам на этот час.
- **Набег**: `OrderGenerator.Generate(…, тип с isRaid, OrderDistance.Near)` → `Order.IsRaid`, награда `raidReward` × номер ранга, на доске `raidDays`,
  `OrderArrived` + `OrderPosted` (`by=raid`). Итог — `QuestReturned` / `OrderExpired` по заказу набега: отбит — Безопасность +; проигран —
  Безопасность −, `SettlementService.Attack` (без броска), население − доля. Обычный сдвиг за угрозу у заказа набега не применяется
  (`SettlementSystem` пропускает `IsRaid`).
- **Налог** — `CrisisStarted` с `amount` и `unpaid`; неуплата (казна < 0 после списания) — репутация и Безопасность −.
- **Зима и мор** действуют, пока их кризис `Active`: цены (`FoodRules.Price/MealPrice/RationPrice/HousingCost(…, winter)`), поток заказов, спад,
  таверна от жителей. Все места, где нужен признак зимы, берут `CrisisRules.IsWinter(world)`; оценки решений — `DecisionScope.Winter`.

## Рецепты

- **Новый кризис**: значение в конец `CrisisKind`, план в `PlanYear`, начало/конец в `Start`/`End`, ключи ленты в `FeedKeys.Crisis*Key`,
  строка календаря и `TopLine`/`Lines` в `CrisisModel`, раздел отчёта `BuildCrises`, числа в `CrisesBalance`, `PeopleData.NoCrises` — обнулить.
- **Новый проект поселения**: значение `BuildingFunction` в конец, определение в генераторе (`Project(…)`, `settlementProject`), эффект в
  `CrisisRules`, текст в `CrisisModel.ProjectEffect`, порядок стройки «Умного».
- **Уровни у другой постройки**: `maxLevel` в генераторе и снять проверку валидатора «уровни только у Арсенала»; эффект уровня — в своей системе.

## Ловушки

- **Тесты**: `StateWorld`/`QuestWorld` по умолчанию без `CrisisSystem` (`crises: true`). `NoSettlementPressure()` зовёт `NoCrises()` — кризисы
  приходят (заказ набега на доске на 100000 дней), но ничего не меняют. В `StateWorld` нет `OrderSystem` — заказ набега там не снимается по сроку:
  проигранный набег — через `Simulation.CreateDefault` (`CrisisTests.Raid_OrderOnBoard_NotTaken_Lost…`).
- Заказ набега берёт номер на общей доске — тесты «не сдвигает чужие броски» с заказами убирают `CrisisSystem` из обоих прогонов.
- Налог зависит от доходов — тест без экономики (`EconomySystems_DoNotShiftOtherSystems`) тоже без кризисов.
- Пустой случай: мор бросает только в расписании (раз в год, свой поток); заказ набега — поток `CrisisSystem`.
- Снаряжение: `GearLevel` ставится при выходе и действует, пока `QuestRunId != 0`; оценка заказа до выхода его не видит.
- Улучшение стоит в той же очереди, у него `State == Ready` и `IsUpgrading`; снимать из очереди — `CancelBuildingCommand` (тот же).
- `TreasuryService.SettleTavern` берёт зиму за прошедшие сутки (`WasWinterAt(now − 1)`).

## Сводка, отчёт, интерфейс

- Сводка: «Уровень Арсенала», «Готовых проектов», «Набегов отбито/проиграно», «Зима» (1 — зимний месяц), «Мор, дней»; статьи «Налог»,
  «Проекты поселения» — сами. Зонд Balance: `10 Years Seeds 1-5 Smart Bot`, в `yearly_*.csv` — набеги, месяцы в минусе, налог, проекты, Арсенал.
- Отчёт месяца: раздел «Кризисы» (`report.crises.*`), в «Деньгах» — «Из содержания — за людей».
- Интерфейс: верхняя панель — вторая строка блока даты (время года и ближайший кризис), вкладка «Поселение» — «Кризисы» и «Проекты»,
  «Постройки» — уровень и улучшение Арсенала, календарь — кризисы, журнал казны — «за людей: N чел.», «лорду за квартал».

## Замер (10 лет, стартовые числа, ✅ автор оставил)

«Простой» жив во всех 5 зёрнах (набег проигран хоть раз в 4, месяцы в минусе — в 1); «Умный» растёт (население ~616), но казна ~330 тыс. к 10-му
году (цель ~100 тыс. не достигнута); «Пассивный» и «Безрассудный» рушатся (частично в срок). Серия года «Простого» — 2 зерна из 10 теряют
поселение на 1-м году.
