---
name: gm-state-health
description: Состояние и здоровье авантюристов GuildMaster — AdventurerState (усталость, стресс, довольство, лояльность, кошелёк, долг, флаги), занятия Activity и ActivitySystem, суточные сдвиги и срывы StateSystem/StateService, множители черт StateRates, чистые правила StateRules, стресс от событий StressEvents, личные деньги WalletService, раны Condition/HealthService, лечение и Лазарет HealthSystem/IInfirmary, проверка ухода. Использовать при правке показателей состояния, срывов, ран, расходов людей, ухода из гильдии.
---

# Состояние и здоровье

Код — `Core/State/` и `Core/Health/` (`game/Assets/_Project/Scripts/Core/`). Дизайн — `docs/mechanics/state.md`, `health.md`.
Числа — `BalanceSettings.State`, `Health`, `Expenses`, `Traits`.

## Карта

| Файл | Что |
|---|---|
| `State/AdventurerState.cs` | шкалы 0..100 (`Fatigue`, `Stress`, `Contentment`, `Loyalty`), `Wallet`, `DebtToGuild`, `Conditions`, `Activity`, `Breakdown` + `BreakdownEndsAtHours`, `InInfirmary`, `SkipsDayUntilHours`, тренировка (`TrainingEndsAtHours`, `TrainingStat`, `TrainedOnDay`), учёт суток (`AteInTavernToday`, `DrankToday`, `PaidInfirmaryToday`), `LastBreakdownAtHours`; для решений и заданий — `PlannedActivity`, `WasFreeLastHour`, `MorningDecisionDay`, `InTavernThisEvening`, `PlannedOrderId`, `QuestRunId`, `QuestParty` |
| `State/Activity.cs` | `Activity` (Resting, Sleeping, Tavern, Training, Infirmary, Binge, OnQuestTravel/Round/Camp), `BreakdownKind`, `PartyContext` (None/Solo/InGroup) |
| `State/ActivitySystem.cs` | шаг 5: занятие и расходы занятия |
| `State/StateSystem.cs` | шаг 6: часовые сдвиги, 00:00 — сутки, начало месяца — проверка ухода |
| `State/StateService.cs` | `AddStress`, `AddFatigue` (× черты), `ChangeContentment`, `ChangeLoyalty` (разово), `StartBreakdown`, `BreakdownKindOf` |
| `State/StateRules.cs` | чистые: `FatiguePerHour`, `StressPerHour`, `CanTakeQuests`, `IsTooTiredForQuests`, `ContentmentTarget`, `CommissionContentment`, `LoyaltyWord` |
| `State/StateRates.cs` | `Multiplier(человек, показатель, рост/падение, данные, PartyContext)`, `PayContentmentSensitivity` |
| `State/StressEvents.cs` | `RoundFailed`, `ComradeWounded`, `AdventurerDied` (+ «Потерявший товарища») |
| `State/WalletService.cs` | `Pay`, `ReceiveIncome(IncomeKind.Reward/Loot)`, `TakeLoan`, `WeeklyExpenses`, `IsBelowWeeklyExpenses`, `MoneyMotiveMultiplier`, `Share`, `Coins` |
| `Health/Condition.cs` | `LightWound`/`HeavyWound`, `RemainingDays`, `IsComplicated` |
| `Health/HealthService.cs` | `Wound(ctx, человек, вид, PartyContext)`, `Maim`, `FindMaimedTrait` |
| `Health/HealthSystem.cs` | шаг 7: каждый час — койки (тяжёлых кладут, лишних выводят), 00:00 — лечение, осложнения; `FreeBeds` |
| `Health/Infirmary.cs` | `IInfirmary` (`Beds`, `HasMedic`, `HealingSpeed`), `BuildingInfirmary` — Лазарет мира (скилл `gm-buildings-staff`) |

## Час человека (кто что ставит)

1. `QuestSystem` (шаг 4) ставит занятия заданий — дальше их никто не трогает.
2. `ActivitySystem` (шаг 5) — первое подходящее: на задании — пропуск; срыв (запой — `Binge`, «сел и не смог подняться» —
   `Resting`); ночь — `Sleeping`; койка — `Infirmary`; тяжёлая рана — `Resting`; Пьяница, пропускающий день, — `Tavern`;
   иначе `PlannedActivity` из прошлого часа, иначе прежнее свободное (`IsFreeTime`), после сна/срыва/Лазарета — `Resting`.
   Тренировка — занятие на `trainingHoursPerDay` часов (`ActivitySystem.Train`: плата 2 в казну при начале; конец — `Resting`
   и `WasFreeLastHour = false`, чтобы человек решил снова в этом же часу).
   Расходы занятия раз в сутки (выпивка, еда в таверне, запой, Лазарет — в казну сколько заплачено); выпивка/еда, за которую
   заплачено, — порция в доход таверны (`TreasuryService.CountTavern…`). Утром — бросок пропуска дня Пьяницы.
3. `StateSystem` (шаг 6): каждый час `StateService.ApplyHour` — усталость и стресс по таблице занятий × `StateRates`
   (снятие стресса в таверне ещё × эффект Трактирщика, `StaffRules.EffectMultiplier`).
   В 00:00 по каждому: `WalletService.PayDaily` (еда, жильё), довольство к `ContentmentTarget` (комиссия — текущая
   `Treasury.Commission`), лояльность к довольству, сброс учёта суток, бросок срыва (стресс выше порога), конец спада
   Потерявшего товарища. В начале месяца — проверка ухода (`LeaveReasons` — скилл `gm-decisions`). **Люди на задании
   не срываются и не уходят.**
4. `HealthSystem` (шаг 7): каждый час — койки: койка держится до выздоровления; нет Лекаря или коек меньше — лишние выходят;
   тяжело раненых кладут на свободные (раньше раненый, затем id); лёгкий ложится сам решением `Heal` (`HealthService.Admit`),
   тяжёлый его не вытесняет. В 00:00 у каждой раны срок −1 × множитель (Лазарет с Лекарем быстрее, без — медленнее), тяжёлая
   рана без Лазарета — один бросок осложнения.
5. `DecisionSystem` (шаг 8) выбирает занятие на следующий час (скилл `gm-decisions`).

## Правила, которые легко забыть

- Все шкалы обрезаются 0..100 (`StateRules.Clamp`). `AddStress/AddFatigue` множат на черты; `ChangeContentment/ChangeLoyalty` —
  нет (разовые: дилеммы, Проверенный).
- **Срыв**: вид по чертам — Пьяница/Ветеран → запой; Безрассудный → драка (мгновенно, отношения и шанс лёгкой раны у обоих);
  Трус → отказ от заданий; иначе «сел и не смог подняться». `StartBreakdown` снижает стресс и публикует `Breakdown` [В] без автопаузы.
- **Раны**: своя рана — стресс; лёгкие не складываются (остаётся самая длинная); вторая тяжёлая — увечье (`Maim`, черта Калека).
  Лёгкая — [З], тяжёлая — [В].
- **Запреты на задания** — `StateRules.CanTakeQuests`: тяжёлая рана, усталость выше `fatigueQuestBanThreshold`, срыв.
- **Кошелёк** целочисленный: доли вниз (`Share`), цены — `Coins`. Не хватает — платит сколько может, флаг `IsWalletEmpty`,
  снимается доходом. Семейный отсылает долю домой; из доли **награды** гасится долг гильдии → в казну (`DebtRepayment`).
- **Лазарета нет** (постройка не готова): коек 0, лечатся медленнее, с броском осложнения. Платы Общежития (в `PayDaily`, жильё
  первым), двора и Лазарета идут в казну. Тесты без построек подменяют Лазарет: `new StateWorld(infirmary: new FakeInfirmary(…))` —
  подмена и в `HealthSystem`, и в `DecisionSystem`.
- Причина ухода для карточки — `Adventurer.LeaveReasonText`, её ставит `StateSystem` сразу после `Retire`.
- Лояльность игрок видит словами: `StateRules.LoyaltyWord` (тексты пока в коде, `LoyaltyWordsMale/Female`).

## Рецепты

- **Новое занятие** — значение `Activity`, строки в `StateRules.FatiguePerHour/StressPerHour`, при надобности в `IsFreeTime`
  и в выбор `ActivitySystem`; действие модели решений, которое его ставит (скилл `gm-decisions`).
- **Новый эффект черты на показатель** — в данных `TraitEffect` вида `StateRate` (показатель, направление, стрелка/множитель);
  код не нужен, если условие уже есть в `StateRates`.
- **Стресс от нового события** — метод в `StressEvents` (числа — баланс), вызывать из системы события.

## События и лог

`AdventurerWounded`, `WoundComplicated`, `WoundHealed`, `Breakdown`, `DrunkardSkippedDay`, `WalletEmptied`, `AdventurerLeft`
(автопауза), `GrievingEnded`. Лог `Debug` — броски (`roll breakdown #3 … stress=85.2 chance=0.1 rolled=… => yes`),
`Trace` — суточная строка состояния каждого (`AdventurerLog`).

## Песочница

GuildMaster → Sandbox → State: `Light Wound First`, `Heavy Wound First`, `Stress +50 First` (Play Mode), `Preview 90 Days`
(без Play Mode). Вывод — `read_console` с `types: ["all"]`.

## Распоряжения

Скилл `gm-decrees`. Здесь они действуют так: в 00:00 еду и жильё новичка может платить гильдия (`DecreeRules.GuildPaysLiving` —
человек не платит, `DecreeService.PayLiving`); цель довольства — `StateRules.ContentmentTarget(…, world)` + `DecreeRules.ContentmentTerm`
(без мира — без распоряжений); после сдвига лояльности — `DecreeRules.DailyLoyalty`; снятие стресса в таверне × Сухой закон
(`StateService.ApplyHour`), шанс Пьяницы пропустить день — `DecreeRules.DrunkardSkipChance`; компенсация за рану — в `HealthService.Wound`.
