---
name: gm-settlement
description: Поселение GuildMaster — часть мира SettlementState (Безопасность, население, уровень SettlementLevel, отсчёт покидания, последние изменения SettlementChange, счётчики SettlementTotals, снимок SettlementSnapshot для отчёта), SettlementService (единственный путь менять: ChangeSecurity, ChangePopulation, нападение, рост за постройку, доход таверны от жителей, проверка покидания), SettlementRules (уровень, ранг × сдвиг, население за месяц, дни до покидания), SettlementSystem (итоги заказов такта, спад, население, нападения), заказ-угроза QuestTypeDefinition.isThreat, поток заказов от населения, общий путь закрытия GuildClosure и ClosedCause, раздел баланса Settlement, статьи «Таверна: жители» и «Ущерб от нападений», раздел отчёта «Поселение», столбцы сводки, вкладка «Поселение», верхняя панель, уведомления, отладка. Использовать при правке Безопасности, уровней, населения, нападений, покидания, угроз и их интерфейса.
---

# Поселение

Код — `game/Assets/_Project/Scripts/Core/Settlement/`, закрытие — `Core/Economy/GuildClosure.cs`. Дизайн — `docs/mechanics/stakes.md` → «A.
Поселение», `docs/mechanics/city.md`. Числа — `BalanceSettings.Settlement` (`SettlementBalance`), поток заказов — `Orders.baseOrdersPerDay`,
`populationPerOrder`, `reputationPerExtraOrder`. Таблица чисел — `docs/content/numbers.md` → «Поселение».

## Карта

| Файл | Что |
|---|---|
| `SettlementState.cs` | `SettlementLevel` (Calm, Normal, Alarm, Siege — больше = хуже), `SettlementState` (`World.Settlement`: `Name` — `NounForms` или null, `Security`, `Population`, `Level`, `StartSecurity/StartPopulation`, `ZeroSecuritySinceHours`, `Changes` ≤ `recentChanges`, `Totals`, internal `TavernRemainder`), `SettlementChange` (время, фактический Δ, причина, заказ, тип и ранг), `SettlementTotals` (угрозы выполнено/упущено, нападения, ущерб, погибшие жители), `SettlementSnapshot` (для `MonthReport.SettlementAtEnd`), `SettlementReasons` (ключи причин) |
| `SettlementRules.cs` | `LevelOf`, `RankFactor` (G = 1 … C = 5), `IsThreat(data, typeId)`, `MonthlyPopulationChange` (от нуля, допуск float), `LocalsTavernIncome`, `DaysUntilAbandoned` |
| `SettlementService.cs` | `Initialize` (конструктор `Simulation`), `ChooseName` (последний бросок `StartScenario`), `ChangeSecurity`, `ChangePopulation`, internal `BuildingReady`, `MonthlyPopulation`, `Attack`, `SettleLocalsTavern`, `CheckAbandoned`; события уровня и отсчёта; `WithSettlement` — название в данные события |
| `SettlementSystem.cs` | после `DilemmaSystem`, перед `EconomySystem`; свой поток |
| `Economy/GuildClosure.cs` | `ClosedCause` (Bankruptcy, SettlementAbandoned), `Close(ctx, причина)` → `Treasury.IsClosed/ClosedAtHours/ClosedCause`, `GuildClosed` (+ `cause`, `settlement`), `BestPeople` |
| `DebugTools/SettlementDebugCommands.cs` | `DebugSecurityCommand(Δ)`, `DebugAttackCommand` |
| `UI/Models/SettlementModel.cs` | вкладка: имя, население и изменение с прошлого отчёта, Безопасность, уровень, «что дальше», отсчёт, изменения (новые сверху), `ChangeText`, `Delta` |

## Ход такта (`SettlementSystem.Tick`)

Без определений или гильдия закрыта — ничего. Затем:
1. **События такта** (только опубликованные до системы — счёт до начала): `QuestReturned` (не экзамен, не событийное задание; заказ —
   по `order`, задание — по `quest`): выполнено — + `threatDonePerRank`/`otherDonePerRank` × ранг; нет — − `failedPerRank` × ранг, погибли
   все вышедшие — ещё − `partyLost`. `OrderExpired`, `OrderDeclinedByRegistrar`, `OrderDeclinedByPlayer` (`noAnswer`) у угрозы — −
   `threatMissedPerRank` × ранг.
2. **00:00** — спад `dailyDecay` («night»), доход «Таверна: жители» (население × `localsTavernIncome`, в осаде × `siegeTavernMultiplier`,
   × Сухой закон, остаток — на завтра); **1-е число** — население по уровню (в отчёт закончившегося месяца).
3. **`NightHour` в осаде** — бросок `settlement-attack`; нападение: бросок погибших, бросок ущерба, стресс всем в гильдии не на задании.
4. **Каждый час** — `CheckAbandoned`: население 0 или ноль Безопасности `abandonDays` суток → `GuildClosure.Close(SettlementAbandoned)`.

`ChangeSecurity`: обрезка → запись изменения → лог `Debug` `security 61.5 -> 60.0 expired #42 Hunt E` → `SecurityChanged` (Trace) →
уровень (вниз в тревогу/осаду — `SettlementLevelDropped` [В] + автопауза `SettlementAlarm`; прочее — `SettlementLevelChanged` [З]) →
отсчёт (стала 0 — `SettlementAbandoning` [В] + автопауза; выше 0 — `SettlementAbandoningLifted` [З]).

## Связи с другими подсистемами

- **Заказы**: `OrderGenerator.OrdersPerDay(баланс, репутация, население)`; доска помечает угрозы (`OrderRow.IsThreat`).
- **Постройки**: `BuildingService.CompleteIfDue` сам вызывает `SettlementService.BuildingReady` (+ `populationPerBuilding`) —
  `BuildingSystem` стоит позже, событие `BuildingReady` система поселения не увидела бы.
- **Казна**: статьи `TavernLocals` (доход), `AttackDamage` (обязательный); закрытие по банкротству — тоже через `GuildClosure`.
- **Отчёт месяца**: раздел `report.settlement` — Безопасность и население было → стало, угрозы выполнено/упущено, нападения и ущерб (если были).
- **Лента**: `guild.settlement.calm|normal.down|normal.up|alarm.down|alarm.up|siege|attacked|abandoning|abandoningLifted|grew|shrank`,
  `guild.closed.settlement`; метка `{поселение}` (данные `settlement`, ссылка `TextLinkKind.Settlement` → вкладка).
- **Сводка**: «Безопасность», «Население», «Уровень», «Угроз выполнено/упущено» (разница `Totals` с началом месяца — `MonthRecord.SettlementFrom`,
  события `SecurityChanged` на Trace сводка не хранит), «Нападений»; статьи — сами.
- **Интерфейс**: верхняя панель (`TopBarModel.Settlement`, цвет `TopBarView.LevelColor`, `UiTheme.good`), вкладка `GuildTab.Settlement`,
  уведомление (покидание → осада → тревога), окно поражения (`DefeatModel.Cause`), подсказка Регистратора, отладка.

## Рецепты

- **Новый сдвиг Безопасности** — причина в `SettlementReasons`, вызов `ChangeSecurity` в своей системе (или в `SettlementSystem.ApplyEvents`,
  если по событию раньше в такте), шаблон `ui.settlement.reason.{причина}` в генераторе и ключ — в `UiTextKeys.SettlementReasonKeys`.
- **Новое число** — поле в `SettlementBalance` (значение по умолчанию = документ), `PeopleData.NoSettlementPressure` — обнулить, если давит.
- **Новая причина закрытия** — значение в конец `ClosedCause`, строка ленты в `FeedKeys` (ключ `GuildClosed`), текст в `DefeatModel`.

## Ловушки

- **Тесты**: `StateWorld`/`QuestWorld` по умолчанию без `SettlementSystem` (`settlement: true`); `PeopleData.NoQuests()` вызывает
  `NoSettlementPressure()` — иначе без заданий поселение покидают за несколько месяцев и прогон встаёт. Годовые прогоны «Простого» на
  числах по умолчанию тоже могут закрыться — тестам, которым нужен целый год, — `NoSettlementPressure()`.
- Сводка не считает последние неполные сутки: при раннем закрытии сравнивать с поселением на конец последнего целого месяца.
- Поток заказов растёт с населением: при Спокойно население растёт на 2% в месяц и к 10-му году «Простого» достигает ~400 (≈11 заказов
  в день) — следить в замерах.
- Нападение бросает только в осаде (один бросок за ночь); «пустой» случай бросков не тратит. `SettlementSystem_DoesNotShiftOtherSystems`
  сравнивает время, приток и людей; заказы законно зависят от населения.
- Строку с обратным слешем (`'\n'`) в правках через heredoc Bash не вставлять — экранирование съедается; править такие места через Edit.

## Проверка

`SettlementTests` (сдвиги, уровни, население, нападения, покидание, доход, детерминизм, «не сдвигает чужие»), `SettlementSummaryTests`,
`SettlementUiTests`. Зонд: GuildMaster → Sandbox → Balance → `10 Years Seeds 1-5` и `… Passive Bot` (в `done.txt` — причина и дата
закрытия, Безопасность, население, нападения, угрозы; в `yearly_*.csv` — по годам). Снимки: UI → `Capture All` (шаг `4b_guild_settlement`).

## Люди и репутация (GM-22)

- Население кормит приток: `RecruitSystem.PopulationMultiplier` = население / `startPopulation` (не больше `influxPopulationCap` 2). Пустеет
  поселение — кандидатов меньше, при 0 — нет. Принятый кандидат население не уменьшает.
- Жадная гильдия (низкое довольство) гасит приток молвой, теряет людей и с ними — угрозы: в замере «Безрассудный» теряет поселение
  в 2 из 3 зёрен.
