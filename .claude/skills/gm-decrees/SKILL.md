---
name: gm-decrees
description: Распоряжения GuildMaster — DecreeDefinition (правило DecreeEffect, область DecreeScopeKind, сроки DecreeDuration, isBenefit), DecreesBalance, часть мира DecreeBook (ActiveDecree, отрезки DecreeSpan), команда ToggleDecreeCommand, DecreeService (включить, сменить, отменить, по сроку, платежи), DecreeSystem (снятие по сроку), DecreeRules (эффекты для других систем — приток, халявщики, запрет соло, компенсация, Сухой закон, довольство, лояльность), статья журнала «Распоряжения», раздел отчёта месяца, столбцы сводки, экран «Распоряжения» и календарь. Использовать при правке или добавлении распоряжения, его эффекта, срока или области.
---

# Распоряжения

Код — `game/Assets/_Project/Scripts/Core/Decrees/`; данные — `Data/Definitions/DecreeDefinition.cs`, `Data/Balance/DecreesBalance.cs`,
ассеты `Data/Decrees/` (генератор `GameDataGenerator.FillDecrees`). Интерфейс — `UI/Models/DecreesModel.cs`, `UI/Views/DecreesScreen.cs`.
Дизайн — `docs/mechanics/laws.md`, `TechJob/16-decrees.md`; числа — `docs/content/numbers.md` → «Распоряжения».

## Карта

| Файл | Что |
|---|---|
| `DecreeDefinition` | `Effect` (`DecreeEffect` — какое правило реализует код), `NameForms` (6 падежей + род), `Description`, `PlusText`, `MinusText`, `ScopeKind` (`None`/`Ranks`), `DefaultRanks`, `AllowedDurations`, `IsBenefit` |
| `DecreeBook.cs` | часть мира `World.Decrees`: `Active` (`ActiveDecree`: `Ranks`, `Duration`, `TermStartedAtHours`, `EndsAtHours` — 0 у бессрочного, `HasRank`), `History` (`DecreeSpan` — включено/выключено), `GetActiveHours(id, от, до]` |
| `DecreeRules.cs` | чистые правила: `Find(data, effect)`, `IsOn/TryGetOn`, `Days(срок)`, `NormalizeRanks`; эффекты — `GuildPaysLiving`, `CandidateChanceMultiplier`, `CandidateWorkShift`, `IsSoloBanned(order)`, `Compensation(kind, maimed)`, `OrderSafetyMultiplier`, `TavernIncomeMultiplier`, `TavernStressReliefMultiplier`, `DrunkardSkipChance`, `ContentmentTerm`, `DailyLoyalty`, `IsAffectedByBenefit` |
| `DecreeService.cs` | `Enable`, `Change` (срок — заново, без строки), `Revoke` (игрок; льгота — довольство `benefitCancelContentment` у затронутых), `Expire`, `PayLiving` (5 в день за новичка), `CompensateWound` |
| `ToggleDecreeCommand.cs` | `(id, вкл., ранги, срок)`: пустая область у распоряжения с областью или недопустимый срок — ничего; то же самое — без событий |
| `DecreeSystem.cs` | шаг 14 (после `RecruitSystem`, до `MonthReportSystem`): снять те, у кого `now ≥ EndsAtHours`; бросков нет |

## Где действуют эффекты

| Распоряжение | Где в коде |
|---|---|
| Еда и жильё для новичков | `StateSystem` 00:00 — `GuildPaysLiving` → `DecreeService.PayLiving` вместо `WalletService.PayDaily` (порция таверны засчитывается); `RecruitSystem.CandidateChance(data, world)` × 1,5; `AdventurerGenerator.Generate(…, workAxisShift)` — Труд −20 |
| Только группой | запрет `"solo forbidden by decree"` в `DecisionBans.Default` (только `TakeOrder`, экзамен — нет); `DecisionSystem.Parties.Gather` — минимум 2, одному не идти, без раскрытия Командного; довольство — `ContentmentTerm` |
| Компенсация за ранение | `HealthService.Wound` → `CompensateWound` (20 / 100 / увечье 300 вместо 100; доход кошелька `IncomeKind.Compensation`); `DecisionScope.SafetyMultiplier` — в `Evaluate` для `action.IsOrder` и в `PartyLens`/`MeanGroupValue`; `StateSystem` — `DailyLoyalty` после `MoveLoyalty` |
| Сухой закон | `TreasuryService.SettleTavern` × 0,6; `StateService.ApplyHour` — снятие стресса в таверне × 0,7, Пьянице 0; `ActivitySystem.TrySkipDay` — шанс 0; довольство Пьяницы −10 — `ContentmentTerm` |

`StateRules.ContentmentTarget(…, world = null)` — без мира распоряжения не учитываются (старые тесты так и зовут).

## События, лента, отчёт, сводка

- События (конец `SimEventType`): `DecreeEnabled` [О] (Сухой закон — [З], участник — первый с **раскрытым** Пьяницей), `DecreeChanged`
  (без строки), `DecreeRevoked` (льгота — [З] «встретили молча», иначе [О] «Отменено»), `DecreeExpired` [О], `InjuryCompensated` (без строки).
  Данные: `decree` (NounForms), `decreeId`.
- Ключи `guild.decree.*` в `FeedKeys` (+ `Fixed`); Сухой закон без участника — `guild.decree.prohibition.quiet`.
- Журнал: `LedgerCategories.Decrees` (обязательный расход), комментарий — id распоряжения, связанный — человек.
- Отчёт: раздел `report.decrees` — `report.decrees.acted` (`ReportItem` с `Detail = decree:{id}`, `Days`, `Amount`; текст —
  шаблон `report.decrees.item`, `MonthReportText.DecreeItem`) и `report.decrees.cost`.
- Сводка: «Расход: Распоряжения» (статья) и «Дней: {название}» по каждому (`SummaryColumns.MonthlyFor`).
- Сценарий: `decree Id on Срок [G,F,…]` / `decree Id off`. Бот «Простой» распоряжения не трогает; прогон со всеми четырьмя —
  GuildMaster → Sandbox → Headless → `Year Seeds 1-10 Info With Decrees`.

## Интерфейс

`DecreesModel` — карточки и черновик (ранги, срок, «вкл.»): у выключенного выбор — только черновик, у включённого — сразу
`ToggleDecreeCommand`; пока отправленное не применено (тот же час), черновик главнее мира. Последний ранг не снять. `DecreesScreen` —
карточки в прокрутке. Календарь — `CalendarModel.DecreesEnd`. Зонд: Sandbox → UI → `Enable Sample Decrees`.

## Рецепты

**Новое распоряжение.** Значение в конец `DecreeEffect`; id и заполнение в `GameDataGenerator.FillDecrees` (+ 6 падежей);
числа — `DecreesBalance`; эффект — функция в `DecreeRules` (выключено → нейтральное значение, бросков нет) и вызов в системе,
которой он касается; кого задевает отмена льготы — `IsAffectedByBenefit`; ожидаемое число в таблице количеств валидатора.
Тест эффекта — `DecreeTests`.

**Новая область** (постройки, группы): значение в конец `DecreeScopeKind`, поле области в `ActiveDecree`, разбор в команде,
кнопки в `DecreesScreen`.

## Ловушки

- Пустой мир распоряжений — `world.Decrees.Active.Count == 0` — все правила возвращают «как без них»: умножение на `1f` и `+ 0f`
  не меняют числа, поэтому серия без распоряжений совпадает с прошлой. Новый эффект не должен тратить бросков в этом случае.
- Включение на паузе: события лежат в шине до следующего такта.
- Срок отсчитывается от часа применения команды (до сдвига времени в такте).
- Названия распоряжений в ленте — `{распоряжение}` из данных события (`EventTextSource`), ссылки на экран нет.
