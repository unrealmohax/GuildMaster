---
name: gm-economy
description: Экономика гильдии GuildMaster — казна Treasury и журнал LedgerEntry, статьи LedgerCategories, единственный путь денег TreasuryService (Credit/Debit, доход таверны), EconomySystem (банкротство, закрытие гильдии), трудные времена HardTimes, комиссия SetCommissionCommand, отчёт месяца MonthReportSystem/MonthReportSections/MonthReportText. Использовать при добавлении дохода или расхода гильдии, статьи журнала, раздела или строки отчёта месяца, при вопросах о банкротстве и казне.
---

# Экономика гильдии

Код — `game/Assets/_Project/Scripts/Core/Economy/`. Дизайн — `docs/mechanics/economy.md`. Числа — `BalanceSettings.Economy`,
`Guild.startMoney`; пороги черт — `Traits`, `Adventurers.loyalStayLoyaltyBelow`.

## Карта

| Файл | Что |
|---|---|
| `Treasury.cs` | `Money` (может быть < 0), `StartMoney`, `Ledger`, `Commission`, `Bankruptcy` (`NegativeSinceHours`, `Active`, `StartedAtHours`, `EndsAtHours`), `IsClosed`, `ClosedAtHours`; internal счётчики порций таверны; `LedgerEntry` (время, статья, сумма со знаком, комментарий, `RelatedId`, `BalanceAfter`) |
| `LedgerCategory.cs` | `LedgerFlow`, `ExpenseKind` (`Mandatory` — даже в минус, `IfAffordable` — только если хватает), `LedgerCategories.All` |
| `TreasuryService.cs` | `Credit`, `Debit` → bool, `CanAfford`, internal `CountTavernFood/Drink`, `SettleTavern` |
| `EconomySystem.cs` | шаг 11: 00:00 — доход таверны; каждый час — банкротство; начало месяца — `HardTimes` |
| `HardTimes.cs` | `IsNow` (банкротство или уход за месяц), `Apply` (Проверенный, раскрытие Преданного) |
| `SetCommissionCommand.cs` | комиссия в пределах `commissionLimits`, смена — `CommissionChanged` [О] |
| `MonthReport.cs` | `MonthReport`, `ReportSection`, `ReportLine` (`Money`, `Number`, `Change`, `People`), `ReportItem`, `MonthReportHistory` (`World.Reports`) |
| `MonthReportSections.cs` | `ReportPeriod`, `MonthReportSection`, `MonthReportSections.Default` (Деньги, Люди, Заказы, Задания, Постройки, Персонал, Репутация), ключи `report.*`, `TextKeys`; подробности строк `BuildingDetail`/`StaffDetail` |
| `MonthReportSystem.cs` | перед `FeedSystem`: в 00:00 первого числа — `Build` → история, лог `Info`, событие `MonthReportReady` [З] |
| `MonthReportText.cs` | отчёт строками: `Lines`, `Label` (первый вариант шаблона), `Amount`, `Value` |

## Инвариант и правила

- **`StartMoney + Σ Ledger.Amount == Money`.** Деньги гильдии меняет только `TreasuryService` — каждая операция = запись журнала
  + строка лога `Debug` (`ledger tavern +3 money=2014 food=4 drinks=2`). Отмечает, с какого часа казна в минусе.
- Нулевая сумма записи не даёт. `Debit` `IfAffordable`-статьи без денег — `false`, ничего не пишет.
- Кошельки людей — не казна (скилл `gm-state-health`). Казну трогают: таверна, комиссия, погашение долга, платы за Общежитие,
  Лазарет и двор, доплаты, событийные задания, находки, стройка, содержание, зарплаты. Трофеи заданий — деньги извне.
- Статьи сейчас: доходы — `Tavern`, `Commission`, `DebtRepayment`, `Dormitory`, `Infirmary`, `TrainingYard`; обязательные расходы —
  `Surcharges`, `EventQuests`, `Discoveries`, `Upkeep`; только если хватает — `Construction`, `Salaries` (скилл `gm-buildings-staff`).
- **1-е число, 00:00**: `EconomySystem` (трудные времена) → `StaffSystem` (долги по зарплате, уход) → `BuildingSystem` (содержание) →
  `SalarySystem` (зарплаты) → `MonthReportSystem` — всё это в отчёте прошедшего месяца.

## Банкротство (каждый час, `EconomySystem`)

Казна в минусе `bankruptcyStartDays` суток подряд → `BankruptcyStarted` [В] с автопаузой, срок `bankruptcyMonths` месяцев.
Казна ≥ 0 (или минус начался заново) → `BankruptcyLifted` [З]. Срок истёк в минусе → `GuildClosed` [В] (участники — лучшие
люди; данные `days`, `died`, `left`, `money`), `IsClosed = true` → `Simulation.IsFinished`: такты и команды больше ничего не делают,
прогон без интерфейса кончается раньше (`Result.GuildClosed`).

## Отчёт месяца

`ReportPeriod` — от прошлого отчёта до текущего часа включительно; то, что было в прошлом отчёте (`WasReported`), не повторяется;
стартовый состав — не новость. Журнал — по индексам `LedgerFrom..LedgerTo`. «Заказы» и «Задания» — разница счётчиков
`OrderTotals` с прошлым отчётом; «Репутация» — `ReportLine.Change`. Раскрытия — по `TraitInstance.RevealedAtHours` /
`Adventurer.GetAxisRevealedAtHours` (подробность `trait:{id}` / `axis:{ось}:{полюс|Balanced}`).

## Рецепты

**Новая статья журнала.**
1. Поле в `LedgerCategories` (`new LedgerCategory("кодЛатиницей", поток, вид расхода)`) и строка в `All`.
2. Шаблон `ledger.{код}` в генераторе данных (`GameDataGenerator.Feed.cs`) — валидатор требует.
3. Операции — `TreasuryService.Credit/Debit(ctx, статья, сумма, комментарий, связанныйId)`.
4. Сводка прогона подхватит статью сама (`SummaryColumns.MonthlyFor(data)` — «Доход: …» / «Расход: …»).

**Новый раздел отчёта.** Строка в `MonthReportSections.Default`: ключ заголовка, список ключей подписей, сборка
`Func<ReportPeriod, List<ReportLine>>`; ключи попадут в `TextKeys` — шаблоны `report.*` в генераторе. Если нужны данные «на конец
месяца» для разницы — хранить их в `MonthReport` (как `ReputationAtEnd`, `OrdersAtEnd`).

**Новая строка раздела** — в его сборке и ключ подписи в его списке.

## Ловушки

- `MonthReportSystem` пишет отчёт и в пустом мире — тесты, сравнивающие логи чужих систем, отбрасывают его строки.
- `EconomySystem` бросков не тратит; `EconomySystems_DoNotShiftOtherSystems` это проверяет — не добавлять случайность без причины.
- Трудные времена считают уходы с `LeaveReason.Left` за месяц до часа проверки включительно, не раньше начала игры. Уход персонала
  трудными временами не считается.
- С постройками и персоналом у тестов, доживающих до 1-го числа, в расходах есть содержание (Зал 50 + Таверна 40) и зарплаты
  стартового персонала.
- Комиссия действует на награды, выплаченные после смены, и на цель довольства со следующего расчёта (00:00).
