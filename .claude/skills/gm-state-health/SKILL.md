---
name: gm-state-health
description: Состояние и здоровье авантюристов GuildMaster — AdventurerState (усталость, стресс, довольство, лояльность, кошелёк, долг, флаги), занятия Activity и ActivitySystem, суточные сдвиги и срывы StateSystem/StateService, множители черт StateRates, чистые правила StateRules, стресс от событий StressEvents, личные деньги WalletService, раны Condition/HealthService, лечение и Лазарет HealthSystem/IInfirmary, проверка ухода; еда и сытость FoodRules/FoodService (приёмы пищи, бюджет на еду, голод и истощение, расходы на день), походные пайки RationStock/RationRules/RationService (покупка при выходе, еда в пути, делёжка, потери, общий запас постоянной группы, паёк в городе); семья за кадром Family/FamilyRules/FamilyGenerator/FamilyService/FamilySystem (состав, недельная плата, нужда, болезнь и смерть родных, весть по возвращении, траур). Использовать при правке показателей состояния, срывов, ран, расходов людей, ухода из гильдии.
---

# Состояние и здоровье

Код — `Core/State/` и `Core/Health/` (`game/Assets/_Project/Scripts/Core/`). Дизайн — `docs/mechanics/state.md`, `health.md`.
Числа — `BalanceSettings.State`, `Health`, `Expenses`, `Traits`.

## Карта

| Файл | Что |
|---|---|
| `State/AdventurerState.cs` | шкалы 0..100 (`Fatigue`, `Stress`, `Contentment`, `Loyalty`), `Wallet`, `DebtToGuild`, `Conditions`, `Activity`, `Breakdown` + `BreakdownEndsAtHours`, `InInfirmary`, `SkipsDayUntilHours`, тренировка (`TrainingEndsAtHours`, `TrainingStat`, `TrainedOnDay`), сытость `Satiety` и `LastMealTier`, учёт суток (`TasteToday`, `DrankToday`, `PaidInfirmaryToday`), `LastBreakdownAtHours`; для решений и заданий — `PlannedActivity`, `WasFreeLastHour`, `MorningDecisionDay`, `InTavernThisEvening`, `PlannedOrderId`, `QuestRunId`, `QuestParty` |
| `State/Activity.cs` | `Activity` (Resting, Sleeping, Tavern, Training, Infirmary, Binge, OnQuestTravel/Round/Camp), `BreakdownKind`, `PartyContext` (None/Solo/InGroup) |
| `State/ActivitySystem.cs` | шаг 5: занятие и расходы занятия |
| `State/StateSystem.cs` | шаг 6: часовые сдвиги, 00:00 — сутки, начало месяца — проверка ухода |
| `State/StateService.cs` | `AddStress`, `AddFatigue` (× черты), `ChangeContentment`, `ChangeLoyalty` (разово), `StartBreakdown`, `BreakdownKindOf` |
| `State/StateRules.cs` | чистые: `FatiguePerHour`, `StressPerHour`, `CanTakeQuests`, `IsTooTiredForQuests`, `ContentmentTarget`, `CommissionContentment`, `LoyaltyWord` |
| `State/StateRates.cs` | `Multiplier(человек, показатель, рост/падение, данные, PartyContext)`, `PayContentmentSensitivity` |
| `State/StressEvents.cs` | `RoundFailed`, `ComradeWounded`, `AdventurerDied` (+ «Потерявший товарища») |
| `State/WalletService.cs` | `Pay`, `MarkUnpaid`, `ReceiveIncome(IncomeKind.Reward/Loot)`, `TakeLoan`, `DailyLivingCost` (жильё + 3 × простая), `WeeklyExpenses`, `IsBelowWeeklyExpenses`, `MoneyMotiveMultiplier`, `PayDaily` (только жильё), `Share`, `Coins` |
| `Food/FoodRules.cs` | чистые: `HungerStage` (Fed/Hungry/Starving/Exhausted), `StageOf`, `IsHungry`, `AfterMeal`, `HousingCost`, `Reserve`, `BudgetDays`, `Choose` (тип по бюджету или `null` — пропуск), `StatMultiplier`, `DayTaste` |
| `Food/RationStock.cs` | запас пайков (дешёвые/обычные): `Adventurer.Rations` — личный, `Party.Rations` — общий постоянной группы; `TakeOne` — сначала дешёвый |
| `Food/RationRules.cs` | чистые: `ReserveMeals` (запас по оси Риск), `Need`, `Afford` (тип по резерву бюджета), `Cost`, `OwnStock`, `OrderCost`, `IsGreedy`, `IsTeamPlayer` |
| `Food/RationService.cs` | `Buy` (при выходе), `EatOnQuest` (+ делёжка `FindDonor`), `GroupStock`, `IsShortForReturn`, `TryLose`, `OnDeath`, `EatInTown`, `OnJoined`/`OnLeft`/`OnDisbanded` |
| `Food/FoodService.cs` | `ApplyHour` (приём в час приёма → убывание → стресс голода и события стадий), `Eat`, `ApplyDay` (истощение — довольство в 00:00) |
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
   Расходы занятия раз в сутки (запой — `bingeDrinkPerDay` кружек, Лазарет — в казну сколько заплачено). Выпивка — `DrinkEvening`
   в начале ночи (`NightHour`) у всех в городе (не на задании, не в Лазарете, без запоя в этот день): `eveningDrinks` 3–4 кружки,
   у Пьяницы `drunkardEveningDrinks` 10–15, +1 за каждые `drinkStressStep` стресса выше `drinkStressThreshold`; обычный — сверх
   `FoodRules.Reserve`, Пьяница — из всего кошелька; кружка — `expenses.drink` (1); ставит `DrankToday`; оплаченные кружки — в доход
   таверны (`TreasuryService.CountTavernDrink(ctx, кружек)`, `tavernIncomePerServing` 0,15). Заход в таверну днём сам не платит. Еда — не занятие (см. «Еда и сытость»). Утром — бросок пропуска дня Пьяницы.
3. `StateSystem` (шаг 6): каждый час `StateService.ApplyHour` — усталость и стресс по таблице занятий × `StateRates`
   (снятие стресса в таверне ещё × эффект Трактирщика, `StaffRules.EffectMultiplier`), затем `FoodService.ApplyHour`.
   В 00:00 по каждому: `WalletService.PayDaily` (только жильё), довольство к `ContentmentTarget` (вкус еды за сутки, комиссия —
   текущая `Treasury.Commission`), истощение — `FoodService.ApplyDay`, лояльность к довольству, сброс учёта суток, бросок срыва (стресс выше порога), конец спада
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
  снимается доходом. Доход домой не уходит (семье — раз в неделю, см. «Семья»); из доли **награды** гасится долг гильдии → в казну (`DebtRepayment`).
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

## Обращения

Скилл `gm-dilemmas`. `AdventurerState.WoundedQuestProfile` — гильдия разрешила ходить на задания с тяжёлой раной (профиль × число);
`IsLaidUpByWound()` = тяжёлая рана без разрешения — её смотрят `CanTakeQuests`, `CanDecide`, `ActivitySystem` (отдых), `HealthSystem`
(кого кладут на койку). Разрешение снимают: зажившая тяжёлая рана (`HealthSystem`) и любая новая рана — она сразу увечье
(`HealthService.Wound`). `WalletService.Give` (деньги без долга) и `Take` (штраф, возврат утаенного) — internal.

## Еда и сытость

Код — `Core/Food/`; числа — `BalanceSettings.Food` (`FoodBalance`, типы — `MealBalance`, перечисление `MealTier`). Дизайн — `docs/mechanics/food.md`.

- **Час** (`FoodService.ApplyHour`, из `StateSystem` после `StateService.ApplyHour`): в час из `mealHours` (7, 13, 19) — в городе
  `Eat`, на задании (и беглец, и идущий домой — у них занятие задания) — `RationService.EatOnQuest`; сытость −2 везде; стадия «после часа»: голоден (< 30) — стресс +0,5 (× черты); переход в голод (< 10) — `HungerStarted` [З],
  в истощение (0) — `Exhausted` [В] с автопаузой `AutopauseKind.Exhausted`. Приём пищи — не занятие: едят и в Лазарете, и в запое.
- **Выбор** — `FoodRules.Choose`, без бросков и мотивов: резерв `reserveDays` × (жильё + 3 × дешёвая) = 42 в городе; лучший тип,
  на который остатка хватит на `budgetDays` (10) дней (Жадный — × (1 + сила полюса Деньги), Семейный — × 1,5, хук `FamilySavesOnFood`);
  иначе дешёвая, если она есть в кошельке; иначе пропуск (`MealSkipped`, `WalletService.MarkUnpaid`). Город: дешёвая < 102 ≤ простая
  < 192 ≤ роскошная.
- **Приём** (`Eat`): `WalletService.Pay`; `сытость = max(сытость, min(потолок, сытость + прибавка))`; `TasteToday += вкус`;
  `LastMealTier`; `TreasuryService.CountTavernMeal(тип)`; лог `Debug` `meal #3 Имя Simple 2 (wallet=…, spare=…)`; событие `MealEaten`
  (`tier`, `guildPays`; лог `Trace`, без строки). Новичок по распоряжению — `DecreeService.PayMeal` за простую, если сам не выбрал
  роскошную (её платит сам).
- **Сутки**: цель довольства + `FoodRules.DayTaste` (сумма вкуса / 3, пропуск — 0) вместо прежнего «ест в таверне»; истощённый (и на
  задании) — довольство −10 после сдвига к цели (`ApplyDay`); `TasteToday` сбрасывается.
- **Последствия голода в других системах**: штраф характеристик — `AdventurerStats.HungerMultiplier` (временный, только 8 характеристик,
  архетип не трогает; на задании — по замороженной сытости); мотивы — `StateFactor.Hunger` (Деньги × 1,5, Безопасность × 0,7);
  уход — `LeaveCause.Hunger` (`reason.hunger`, считается денежной: раскрывает Наёмника).
- **Расходы на день** — `WalletService.DailyLivingCost(человек, баланс)` = жильё + 3 × простая (город 9): через неё — «расходы на неделю»
  (мотив «Деньги», 63 в городе), займы и триггеры обращений, мерило денег в оценке заказа (`QuestChoices.ExpenseScale`).
- Интерфейс: карточка — полоса «Сытость» со словом стадии, «ест … еду», значок ● у имени; отладка — «Сытость» в показателях и кнопки
  «Сытость 0/100» (`DebugSetStateCommand`, `StateStat.Satiety`).

## Походные пайки

Дизайн — `docs/mechanics/food.md` → «Припасы в поход»; числа — `FoodBalance` (`rationCheap`/`rationRegular` — `MealBalance`, тип `RationTier`;
`rationReserveMeals`, `cautiousExtraMeals`, `rationLossChance`, `rationLossShare`, `rationLossFailures`, `greedyNoShareAxis`,
`shareRelation`, `hungryRetreatBonus`). Без бросков, кроме потери.

- **Покупка** (`Buy`, из `QuestSystem.Depart`): приёмы ожидаемого задания по ритму дня с часа выхода (`TravelPlan`) + запас
  (3 × (1 − сила Безрассудного) + 3 × сила Труса). Тип (`Afford`): обычные, если `кошелёк − FoodRules.Reserve ≥ число × 2`; иначе дешёвые,
  сколько хватит денег. Постоянная группа — в общий запас, доли поровну. Пайки — в доход таверны (`TreasuryService.CountTavernRations`,
  статья «Таверна: еда»). Строка `quest.rations.bought|enough|short`, лог `Debug` `rations quest #N …`.
- **Еда в пути** (`EatOnQuest`): свой запас (член идущей постоянной группы — общий, потом личный) → товарищ по заданию с личным запасом
  (`FindDonor`: не Жадный — Деньги ≥ 50; Командные первыми; больше запас), отношения +3, `RationsShared` → иначе голодный приём
  `QuestMealMissed` (Trace); у группы ни одного пайка — раз за задание `RationsRanOut`. Лог `Debug` `ration #id Имя Cheap [from …]`.
- **Город** (`Eat` → `EatInTown`): нет денег даже на дешёвую — паёк (член постоянной группы — из общего); `LastMealWasRation`, порция
  не в доход таверны, «кошелёк пуст» отмечается.
- **Потери, гибель, решение отступить, постоянная группа** — скиллы `gm-quests` и `gm-parties`.

### Ловушки еды

- `StateWorld.Add` даёт 1000 монет — такие люди едят роскошную (15 в день): тесты, считающие кошелёк, учитывают еду; человек
  без денег в долгом тесте **голодает** (стресс, штраф, довольство) — держать сытость `LambdaSystem` или пополнять кошелёк.
- Стресс голода +0,5 равен снятию стресса отдыхом: у отдыхающего голодного стресс стоит, растёт лишь относительно сытого.
- На задании сытость теперь убывает: долгий тест с человеком на задании без пайков — голод (стресс, штраф характеристик, истощение).
  Пайки покупаются при `Depart` из кошелька — тесты, считающие кошелёк после выхода, учитывают покупку.
- Сытость по умолчанию у нового `AdventurerState` — 100; генерация ставит `food.startSatiety`.
- Сводка: «Сытость», «Приёмов: … %», «Пропущено приёмов», «Дней в голоде/истощении» (часы стадий из `SummaryRecorder`), «Ушли из-за голода».

## Семья

Код — `Core/Family/`; числа — `BalanceSettings.Family` (`FamilyBalance`). Дизайн — `docs/mechanics/family.md`, ТЗ — `TechJob/20-family.md`.

| Файл | Что |
|---|---|
| `Family.cs` | `Adventurer.Family` (`Members`, `LivingCount`, `HasLiving`, `HasLivingSpouse`, `TryGetIll`); `FamilyMember` (роль, пол, имя, `Health` + `IllSinceHours`/`IllUntilHours`, `Alive` + `DiedAtHours`, `PendingNews`); `FamilyRole`, `FamilyHealth`, `FamilyNews` |
| `FamilyRules.cs` | чистые: шансы состава (`ParentAliveChance`, `SpouseChance` — Одиночка, Семейный × 2, Влюблённый 0, `ChildChance`), `MemberCost`/`Norm`, `Payment` (обычный — сверх `FoodRules.Reserve`, Семейный — ⌈норма × 1,3⌉ из всего кошелька), `IllChance`, `DeathChance`, `DeathStress`, `RelativeValue` (метка `{родной}`); `RelativeTextSource` |
| `FamilyGenerator.cs` | internal `Generate(rng, данные, человек)` — броски состава; `FitToTraits` — Влюблённому убрать супруга (без бросков) |
| `FamilyService.cs` | `PayWeek` (→ `WalletService.SendHome`, нужда, события), `WeekOfIllness` (бросок `family-death`, выздоровление), `RollIllness` (`family-ill`), `FallIll`, `Die`, `DeliverNews`, `ApplyDay` (стресс нужды, конец траура) |
| `FamilySystem.cs` | шаг 6, **перед** `StateSystem`, свой поток: каждый час — вести вернувшимся; 00:00 — день платы (`IsPayday`: каждые 7 суток от дня начала), начало месяца — болезни, сутки — `ApplyDay` |

- **Состояние**: `AdventurerState.FamilyInNeed` (заплачено меньше нормы; снимает полная плата), `IsMourning` + `MourningEndsAtHours`.
- **Эффекты в других системах**: цель довольства −10 (`StateRules.ContentmentTarget`); снятие стресса × 0,5 в трауре (`StateRates.Multiplier`);
  `StateFactor.FamilyNeed` — Деньги × 1,5 (`Motives`, в логе `family need`); `LeaveCause.FamilyNeed` (`reason.familyNeed`, денежная —
  раскрывает Наёмника, проверяется раньше голода); повод «Просьбы в долг» (скилл `gm-dilemmas`).
- **Весть**: человек на задании (`IsOnQuest`) получает `PendingNews`; в первый час вне задания — `FamilyMemberIll` [З] / `FamilyMemberDied`
  [В] (автопауза `AutopauseKind.FamilyDeath`, стресс +30/+50 × черты, траур 30 дней). Поправился до возвращения — болезнь не сообщается.
- **События**: `FamilyMoneySent` (`amount`, `norm`; уровень `Debug` — сводка не видит событий `Trace`), `FamilyNeedStarted`, `FamilyMemberIll`,
  `FamilyMemberDied` (`relative` — номер родного в семье, `role`). Лог `Debug`: `family pay #3 Имя norm=22 sent=22 (wallet … -> …)`,
  `family #3 Имя: Spouse Ида ill for 4 weeks / recovered / died`, `need over`, `mourning over`; суточная строка — `familyNeed`, `mourning`.
- **Интерфейс**: карточка — блок «Семья» (`CardModel.Family/FamilyNorm/FamilyInNeed/Mourning`, слова родства — `UiStrings.RelativeWords`,
  «†» есть в шрифте); отладка — `DebugEventKind.FamilyIllness/FamilyDeath`; отчёт месяца — `report.people.familyIll/familyDied` (только
  то, о чём человек уже знает).
- **Сводка**: «Людей с семьёй, %», «Членов семьи в среднем», «Отправлено домой», «Недель в нужде», «В нужде», «Болезней», «Смертей в семье»,
  «Ушли из-за нужды семьи».

### Ловушки семьи

- `StateWorld.Add` создаёт людей **без семьи** — `FamilySystem` у них ничего не делает; тесту семьи — добавить родных
  (`adventurer.Family.Add(new FamilyMember(роль, пол, имя))`, `FamilyTests.WithFamily`: жена и двое детей = 22 в неделю).
- Плата идёт и с задания; нужда держит стресс (+1/сутки) — у новичков в месяцы 4–7 совпадает с голодом.
- `Debug`-лог прогона вырос на строку `family pay` на человека в неделю.
