---
name: gm-decisions
description: Модель решений GuildMaster — DecisionSystem (шаг 8), точки решения DecisionPoints, запреты DecisionBans, действия и оценки DecisionActions/DecisionScope, веса мотивов Motives/MotiveWeights, выбор лучшего с bestChoiceChance, взятие заказа OrderChoice, вечер в таверне TavernEvening, причины ухода LeaveReasons. Использовать, когда меняется то, как люди сами выбирают занятие или заказ, при добавлении действия, точки, запрета или мотива, и при разборе строк лога decide/scores/ban.
---

# Модель решений

Код — `game/Assets/_Project/Scripts/Core/Decisions/`. Дизайн — `docs/mechanics/decision-model.md`, `indirect-control.md`.
Числа — `BalanceSettings.Decisions`, `Adventurers` (таверна, ссоры).

## Карта

| Файл | Что |
|---|---|
| `DecisionSystem.cs` | шаг 8: `Tick`, `Decide`, `Evaluate`, `Choose`, `MainReasons`, запись в лог |
| `DecisionSystem.Parties.cs` | группы: `AddSeekParty`, `Gather`, `InviteWhileValueGrows`, `Invite`, `PermanentPartiesDecide` (скилл `gm-parties`) |
| `DecisionPoints.cs` | `DecisionPoint`, `DecisionPoints.Default` (ночь → вечер → утро → освободился), `CanDecide`, `Today`; `DecisionBan`, `DecisionBans.Default` |
| `DecisionActions.cs` | `DecisionActionKind` (Rest, Tavern, Sleep, TakeOrder, SeekParty, JoinParty, Train, Heal), `DecisionAction`, `DecisionScope` (`FriendsHeadingTo`, `Profiles`), `DecisionActions.Rest/Tavern/Sleep/Train/Heal`, `TavernPrice`, `Scores` |
| `Motives.cs` | `Motives.Weigh(человек, данные, вТаверне)` → `MotiveWeights` (+ `Factors`), `StateFactor` |
| `OrderChoice.cs` | варианты «взять заказ», `RefusesFromNightmares`, `Take`, `Reserve`, `Release`, `Commit`, `Refused` (раскрытия Семейного, Жадного, Ленивого), `AverageReward` |
| `TavernEvening.cs` | итоги вечера в начале ночи: отношения, ссоры (`Quarrel` [З]), раскрытие Соперников |
| `LeaveReasons.cs` | `LeaveCause`, `Of`, `Text`, `IsMoney`, `Keys`; `PersonTextSource` (`{имя}`) |

## Ход `DecisionSystem.Tick`

1. В час начала ночи — `TavernEvening.Settle`.
2. Утром — сначала постоянные группы (`PermanentPartiesDecide`).
3. По каждому активному (копия списка): вечером отметить `InTavernThisEvening`; если `CanDecide` (не на задании, не в запое
   и не «сел», не в Лазарете, без тяжёлой раны, не пропускает день), ещё не решал в этом часу (`decidedThisHour` — в т.ч. ответом
   на приглашение) и не взял заказ — первая наступившая точка из `DecisionPoints.Default` → `Decide`. В конце —
   `WasFreeLastHour`.

`Decide`: варианты точки (+ по варианту на каждый заказ доски, если `OffersOrders`; + `Train`, если двор готов, и `Heal`, если
есть рана, Лекарь и свободная койка, — если `OffersServices`: утро и «освободился») → запреты `DecisionBans` → варианты
«собрать группу» → нет вариантов — ничего; один без оценок (сон) — без выбора → `Evaluate` (ценность = Σ вес мотива × оценка,
оценки −1..1; сортировка по убыванию, при равенстве — по порядку) → `Choose` (лучший с шансом `bestChoiceChance`, бросок
`decision-best`; один вариант — без броска; Кошмары могут отказаться от заказа — тогда следующий) → `PlannedActivity` →
по виду: `TakeOrder` — `OrderChoice.Take`; `SeekParty` — `Gather`; `Heal` — сразу занять койку (`HealthService.Admit`); не заказ,
хотя заказы были — `OrderChoice.Refused`. `DecisionSystem(IInfirmary)` — тот же Лазарет, что у `HealthSystem`.

**Решение действует со следующего часа**: `ActivitySystem` поставит `PlannedActivity`, `QuestSystem` выведет по `PlannedOrderId`.

## Мотивы и оценки

Шесть `Motive` (Деньги, Слава, Безопасность, Товарищи, Отдых, Утешение). Вес = вес по умолчанию × черты (эффекты `MotiveWeight`:
у осей плавно по силе полюса, у особых черт — полная стрелка; утешение Пьяницы — только для варианта в таверне) × состояние
(кошелёк ниже недельных расходов → Деньги; усталость → Отдых; стресс → Утешение и Безопасность; лёгкая рана → Безопасность).
Оценки вариантов: отдых и таверна — `DecisionActions`; заказ — `QuestChoices.OrderScores` от воспринимаемого шанса
(`QuestMath.PerceivedSoloOverlap`); заказ с группой — `PartyMath`/`PartyLens`.

## Запреты (`DecisionBans.Default`)

Таверна утром и днём — только Пьянице или при стрессе выше `daytimeTavernStress`. Заказ (одному и с группой): выше ранга
(кроме своего экзамена), `!CanTakeQuests`, выйти в следующем часу поздно, чужой экзамен, экзамен — только одному, заказ уже
взят (кроме `JoinParty` той группы, что зовёт). Тренировка: двор полон (считаются и выбравшие двор в этом часу), уже тренировался
сегодня, есть рана, усталость выше 90, в кошельке меньше платы. Причина запрета — в лог `Debug` (`ban …`).

## Причины ухода

`LeaveReasons.Of`: до `maxReasons` мотивов с весом выше базового → `LeaveCause` по состоянию (пустой кошелёк, рана, недавний
срыв за `recentBreakdownDays`); ничего — `LowLoyalty`. `Text` — шаблоны `reason.*` (самый конкретный: раскрытая черта
называется, скрытая — нет; первый вариант, без бросков). Если главная — деньги, раскрывается Наёмник.

## Рецепты

- **Новое действие** — значение в `DecisionActionKind`, `DecisionAction(вид, занятие, вТаверне, функция оценок)` в
  `DecisionActions`, добавить в `Options` нужных точек. Оценки ограничены −1..1.
- **Новая точка** — строка в `DecisionPoints.Default` (порядок = приоритет), `IsDue`, варианты, `OnDecided`.
- **Новый запрет** — строка в `DecisionBans.Default` (причина + правило).
- **Новый фактор состояния** — `StateFactor` и строка в `Motives.Weigh`; причина — `LeaveCause` в конец + шаблон `reason.*`
  в генераторе (валидатор требует шаблон на каждый ключ).

## Лог (главный инструмент разбора)

- `Info`: `decide #3 Имя evening: Tavern 2.21 (best; Rest 1.43) because Comfort 0.95 (stress 72), …`; `decide … TakeOrder#12 …`.
- `Debug`: `ban …`, `… night: Sleep (no choice)`, броски `decision-best`, `quarrel`, `tavern evening: N people`.
- `Trace`: `scores #3 Имя evening Tavern=2.21: Money 1x-0.01 …` по вариантам; `order-eval` по заказам.

## Ловушки

- Любой новый вариант сдвигает броски `decision-best` у всех, кому он доступен, а значит — и всё после них (задания, группы).
  Если вариант бывает «пустым», он не должен добавляться и тратить броски (так сделаны `Train` и `Heal`).
- Оценки `Train` (Слава 0,4, Безопасность 1, Товарищи 0,2, Отдых −0,3) проигрывают отдыху у нейтральных людей: тренируются в основном
  те, у кого Слава ↑ (Амбициозные, Безрассудные).
- Тесты, которые проверяют расписание вечера, ставят `decisions.bestChoiceChance = 1`.
- `DecisionScope` живёт один час: друзья и профили кешируются на час (отношения и параметры за час не меняются).
