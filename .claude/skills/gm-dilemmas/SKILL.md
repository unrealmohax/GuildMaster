---
name: gm-dilemmas
description: Обращения и дилеммы GuildMaster — DilemmaDefinition (варианты, эффекты DilemmaEffect, причины, эффекты появления, вариант по сроку), DilemmasBalance, часть мира DilemmaBook (Dilemma, статусы, перезарядки, отложенный праздник), DilemmaSystem (триггеры в 12:00 и по событиям, лимит, перезарядка, срок ответа, снятие), DilemmaService (открыть, ответить, снять, эффекты, праздник), DilemmaRules (цена варианта, доступность, суммы займа и лечения), DilemmaTextSource, команда AnswerDilemmaCommand, память человека AdventurerMemory (флаги для цепочек), разрешение «с тяжёлой раной», цепочка прощённого беглеца, запрет и разрешение Влюблённым, статьи «Займы авантюристам»/«Обращения»/«Штрафы», раздел отчёта, столбцы сводки, бот, экран «Обращения» и карточка, отладочная «Дилемма». Использовать при правке или добавлении дилеммы, её триггера, варианта или эффекта, флага памяти и цепочки.
---

# Обращения и дилеммы

Код — `game/Assets/_Project/Scripts/Core/Dilemmas/`; память — `Core/Adventurers/AdventurerMemory.cs`; данные —
`Data/Definitions/DilemmaDefinition.cs`, `Data/Balance/DilemmasBalance.cs`, ассеты `Data/Dilemmas/` (генератор
`ClaudeSandbox/Editor/GameDataGenerator.Dilemmas.cs`). Интерфейс — `UI/Models/DilemmasModel.cs`, `UI/Views/DilemmasScreen.cs`
(экран и `DilemmaWindow`). Дизайн — `docs/mechanics/dilemmas.md`, `TechJob/17-dilemmas.md`; решения — `docs/decisions.md` 2026-10-05.

## Карта

| Файл | Что |
|---|---|
| `DilemmaDefinition` | `Trigger` (код триггера), `Source`, `CooldownDays` (0 — общая), `CooldownPerGuild`, `OncePerPair`, `BodyTemplate`, `Reasons` (`{причина}`, с чертой — только у её носителя), `ArrivalEffects`, `Options` (`DilemmaOption`: текст, видимые последствия, эффекты, `IsRefusal`, `PlayerSelectable`, `AnswerFeedKey`), `TimeoutOption` |
| `DilemmasBalance` | срок ответа 2 дня, лимит 3, перезарядка 30, час проверки 12; числа триггеров; цепочка прощённого (`pardonRedemptionChance` 0,5, `pardonHeroMultiplier` 3, `pardonFleeMultiplier` 1,5) |
| `DilemmaBook.cs` | `World.Dilemmas`: `Open`, `Closed` (все с начала игры), `TryGetDilemma/TryGetOpen`, `IsAwaiting(триггер, человек)`, `IsOpen(id дилеммы)`, `TryGetLastArrival` (перезарядка: человек или 0 — гильдия), `PendingFeastId/Option`; `Dilemma` (кто, второй, сотрудник, задание, место, сумма, причина, брошенные, срок, статус, выбранный вариант) |
| `DilemmaRules.cs` | `Find(data, триггер)`, `CooldownDays`, `LoanAmount`, `WeekAndTreatment`, `OptionCost` (сколько заберёт из казны), `IsAvailable`/`IsUnaffordable`/`AvailableOptions`, `HadRecentDeath`, `DailyFromWeekly`, `AreSeparated`, `PartnerBonus` |
| `DilemmaService.cs` | `Open(ctx, определение, настройка)`, `Answer(ctx, обращение, вариант, поСроку)`, `Withdraw`, `ApplyFeast(ctx, кто был в таверне)` и все эффекты |
| `DilemmaSystem.cs` | шаг 10 (после `DecisionSystem`, до `EconomySystem`) |
| `AnswerDilemmaCommand.cs` | ответ игрока (закрытое, скрытый вариант, нет денег — ничего) |
| `DilemmaTextSource.cs` | метки `{имя}` (у персонала — сотрудник), `{напарник}`, `{место}`, `{причина}`, `{сумма}` (у варианта — `OptionCost`, у тела — запрошенная) со ссылками; `Render(…)` |
| `AdventurerMemory.cs` | `Adventurer.Memory`: `MemoryEntry` (флаг, когда, с кем, число); `HasFlag`, `TryGetFlag`, internal `Set`, `Remove` |
| `Core/DebugTools/DebugCommands.cs` | `DebugDilemmaCommand(триггер, человек)` — в обход условий, перезарядки и лимита |

## Ход `DilemmaSystem.Tick` (без определений — пропуск)

1. **Снять** открытые, если обратившийся, второй участник или сотрудник ушёл (`DilemmaWithdrawn`, без эффектов, без строки).
2. **По сроку** (`now ≥ DeadlineAtHours`) — `Answer(TimeoutOption, timedOut)`.
3. **По событиям такта**: `DeserterReturned` → «Беглец вернулся» (лимит открытых не действует; брошенные — `QuestRun.GetAbandonedBy`);
   `LootHandedIn` (группа ≥ 2) → «Ссора из-за добычи»: {A} — заметивший утаивание (`QuestRun.SkimCaught`), иначе самый жадный
   (Деньги ≥ 50); {B} — худшие отношения к {A} ниже 0; бросок `loot-dispute`.
4. **В `checkHour` (12:00)** — триггеры по порядку дилемм в данных: просьба в долг (кошелёк < 3 дней, нет долга, Семейный / Пьяница /
   бросок `loan-request` с шансом `DailyFromWeekly(10%)`), раненый (тяжёлая рана без разрешения и кошелёк < 7 дней или Семейный),
   Влюблённые (черта не раскрыта, оба ≥ 7 дней, не на задании), праздник (Трактирщик нанят, средний стресс > 45 или гибель за 7 дней,
   перезарядка на гильдию).
   Общие условия (`IsFree`): не на задании, такое же не ждёт ответа, перезарядка прошла; лимит — `CanOpenMore`. **Бросок — только
   когда все условия выполнены**, свой поток `DilemmaSystem`.

`Open`: срок `responseDays` суток, перезарядка — от появления, строка лога `decide #3 Имя request: LoanRequest #12` (решения
`FileRequest` в модели решений нет), эффекты появления (раскрытия), событие `DilemmaArrived` [З] с автопаузой `DilemmaReceived`.

## Эффекты (`DilemmaService`)

Цели — только активные. `Treasury` (−: «Обращения», +: «Штрафы»), `Contentment`/`Loyalty` (разово), `Stress`/`Fatigue` (× черты),
`Relation` (пара; брошенные к беглецу; в таверне — попарно), `WalletToTreasury` (штраф, «Штрафы»), `GiveLoan` («Займы авантюристам» →
`WalletService.TakeLoan`), `PayWeekAndTreatment` («Обращения» → `WalletService.Give`), `RevealAxisPole`/`RevealTrait` — **в обход
триггера из данных** (`RevealService.TryRevealAxisByDilemma`/`TryRevealTraitByDilemma`; раскрывается, только если черта/полюс есть),
`SetMemoryFlag` (у пары — с другим), `Expel` (`LeaveReason.Expelled`, без `AdventurerLeft`), `AllowQuestWhileWounded`,
`AllowSameParty`/`ForbidSameParty` (ставят флаги `LoversTogether`/`LoversSeparated` сами), `ReturnSkimmedLoot`,
`ContentmentIfRecentDeath`. Цель `AllInTavern` откладывается: `PendingFeast` → `TavernEvening.Settle` → `ApplyFeast`.

## Где действуют последствия

| Что | Где |
|---|---|
| Беглец ждёт ответа — заказов не берёт | запрет `"deserter awaits the guild's answer"` в `DecisionBans.Default` |
| Прощён: первый момент напряжения — искупление или бегство × 1,5 | `QuestTension.FleeChance` (флаг `Pardoned` тратится), `QuestRun.IsRedeeming` → `TryHero` без порогов, шанс × 3 |
| Разрешение «с тяжёлой раной» | `AdventurerState.WoundedQuestProfile` / `IsLaidUpByWound()` — в `CanDecide`, `CanTakeQuests`, `ActivitySystem`, `HealthSystem` (не кладут на койку); профиль × 0,6 — `AdventurerStats.WoundedQuestMultiplier`; новая рана → увечье и снятие (`HealthService.Wound`); зажила тяжёлая — снятие (`HealthSystem`) |
| Влюблённые вместе | `PartyMath.PartnerScore` + `DilemmaRules.PartnerBonus` |
| Влюблённые врозь | `DecisionSystem.Parties.Invitable` (`IsSeparated`), `PartyService.Separate` (позже вступивший уходит, строка `guild.party.memberLeft.separated`) |
| Праздник | `TavernEvening.Settle` в конце → `DilemmaService.ApplyFeast` |

## Лента, журнал, отчёт, сводка, бот

- Ключи: `guild.dilemma.arrived` (`FeedKeys.DilemmaArrived`), ответ — ключ варианта из данных (`feedKey` события, `FromPayload`).
- Статьи: `LedgerCategories.Loans` (`loans`), `Dilemmas` (`dilemmas`), `Fines` (`fines`); комментарий — id дилеммы.
- Отчёт: раздел `report.dilemmas` — пришло, ответили, без ответа, снято (`MonthReportSections.BuildDilemmas`).
- Сводка: «Обращений», «Без ответа, %», «Обращений: {название}», «Ответ: {название} — {вариант}» (`SummaryColumns.MonthlyFor`).
- Бот «Простой» — `AnswerDilemmasRandomlyRule`: на каждое новое — вариант наугад из доступных или «не отвечать» (равный шанс; своя
  случайность из id и часа обращения). Сценарий: `dilemma IdОбращения НомерВарианта`.

## Интерфейс

`ScreenId.Dilemmas` (вкладка навигации), `DilemmasModel` (открытые + 15 последних закрытых; `Answer` помнит отправленные до применения),
`DilemmaWindow` (`PopupKind.Dilemma`, открывается само через `PopupQueue`), уведомление `NotificationsModel.Dilemmas`, календарь
`CalendarModel.DilemmaAnswersDue`, отладочная панель — строка «Дилемма» (выбор дилеммы, человек — последняя открытая карточка).
Зонд: Sandbox → UI → `Spawn Sample Dilemmas`; снимки 17–18 в `Capture All`.

## Рецепты

**Новая дилемма.** Значение в конец `DilemmaTrigger`; id в `DilemmaIds` и заполнение в генераторе (тексты, варианты, эффекты, ключи
ответов + шаблоны `dilemma.*` в `.Feed.cs`); числа триггера — `DilemmasBalance`; триггер — метод в `DilemmaSystem.DailyTriggers` (или
по событию в `Tick`) с `IsFree`/`CanOpenMore` до броска; ожидаемое число дилемм — в таблице количеств `DataValidator`.

**Новый эффект варианта.** Значение в конец `DilemmaEffectKind`, ветка в `DilemmaService.Apply`/`ApplyToPerson`; если он стоит денег —
в `DilemmaRules.OptionCost`; если действует позже — флаг памяти (`MemoryFlag` в конец) и чтение там, где он влияет.

## Ловушки

- **Тестовые миры без обращений.** `StateWorld` по умолчанию убирает `DilemmaSystem` (ответы по сроку меняли бы довольство и кошельки
  в чужих тестах); тесты обращений — `new StateWorld(dilemmas: true)` / `new QuestWorld(dilemmas: true)`.
- В реальной игре обращения сдвигают мир (деньги, довольство, уходы), поэтому серия года отличается от прошлой.
- Ответ на паузе применяется сразу (`ApplyCommandsNow`); пока время идёт — в начале следующего такта, модель помнит отправленное.
- Генератор: `AllowSameParty`/`ForbidSameParty` ставят флаги сами — отдельный `Flag(LoversTogether)` после них затёр бы число (0,5).
- `Dilemma`, `AdventurerMemory`, `MemoryEntry` — части мира: публичные методы только `Is/Has/Get/TryGet…` (`ArchitectureTests`).
- «Ссора из-за добычи» в серии года почти не возникает: в группах редко бывают пары с отношениями ниже 0.
