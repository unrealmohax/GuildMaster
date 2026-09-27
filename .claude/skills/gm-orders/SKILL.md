---
name: gm-orders
description: Заказы GuildMaster — модель Order и статусы, доска OrderBoard (Open, InWork, Closed, правила Регистратора RegistrarRules, счётчики OrderTotals), генерация OrderGenerator (смесь рангов, профиль требований, награда, описание и намёки, точность описания), OrderSystem (поток по утрам, снятие по сроку, важные заказы игроку), команды SetRegistrarRulesCommand/AnswerImportantOrderCommand/SetSurchargeCommand, репутация гильдии. Использовать при правке прихода, отбора и жизни заказов, скрытого профиля и описаний, Регистратора и репутации.
---

# Заказы, доска, репутация

Код — `game/Assets/_Project/Scripts/Core/Orders/`, репутация — `Core/Guild/`. Дизайн — `docs/mechanics/quests.md` (заказы),
`docs/mechanics/economy.md`. Числа — `BalanceSettings.Orders`, `Ranks`, `Guild`; доли типов — `QuestTypeDefinition.generationWeight`.

## Карта

| Файл | Что |
|---|---|
| `Order.cs` | заказ; `OrderStatus` (Incoming → OnBoard / AwaitingPlayer → Declined / Expired; Taken → InProgress → Done / Failed), `OrderDistance`, `OrderDeclinedBy` (Registrar, Player, NoAnswer). Видимое игроку — public; **скрытое — internal**: `Profile`/`Requirement(StatId)`, `StatCeiling`, `PartySizeCeiling`, `DescriptionAccuracy`, `TrueRank` |
| `OrderBoard.cs` | `Open`, `InWork`, `Closed` (≤ `closedOrdersLimit`), `Rules`, `Totals`, `TryGetOpen`, `TryGetOrder`, `GetPromisedSurcharges`, `TryGetPromotion`; свой `NextId`; `RegistrarRules` (по умолчанию: все типы, до C, минимум 0), `OrderTotals` |
| `OrderGenerator.cs` | internal, чистые функции от потока: `Generate`, `RankMixFor`, `OrdersPerDay`, `BuildProfile`, `Reward`, `LargestAxes`, `IsImportant` |
| `OrderSystem.cs` | шаг 3: `Tick`, `ApplyStart` (поток `OrderStart`), internal `Post`, `Decline`, `Publish`, `WriteGenerated` |
| `OrderCommands.cs` | `SetRegistrarRulesCommand`, `AnswerImportantOrderCommand`, `SetSurchargeCommand` |
| `OrderTextSource.cs` | метки описания `{заказчик}`, `{место}`, `{враг}`, `{груз}` |

## Жизнь заказа

1. **Приход** (`OrderSystem`, в начале утра): число = `OrdersPerDay(репутация)`, дробная часть — бросок `extra-order` **всегда**.
   Каждый: `OrderGenerator.Generate` → `OrderArrived`.
2. **Разбор**: важный (`IsImportant`: «сложный не по времени» или награда от порога) → `AwaitingPlayer`, `OrderAwaitingPlayer` [В]
   с автопаузой «Важный заказ», срок ответа `playerResponseDays`. Обычный → `RegistrarRules.IsAccepted` → `Post` (`OrderPosted`)
   или `Decline(Registrar)` (`OrderDeclinedByRegistrar`). В конце утра — `NewOrdersPosted` / `RegistrarDeclinedOrders` (`count`) для ленты.
3. **Каждый такт**: снять с доски вышедшие по сроку (`OrderExpired`), отклонить важные без ответа (`OrderDeclinedByPlayer` с `noAnswer`).
4. **Взятие** — модель решений (`OrderChoice`, скилл `gm-decisions`): заказ уходит из `Open` в `InWork` сразу (`Taken`), задание
   начинается в следующем часу (`InProgress`), итог — `Done`/`Failed` и архив (скилл `gm-quests`).
5. Особые заказы: **экзамен** (`IsPromotion`, `OwnerId`, без награды, по сроку не снимается, Регистратор и счётчики не касаются)
   и **событийное задание** (`IsEventQuest`, ранг назначает игрок, платит гильдия) — оба создаёт система заданий.

## Генерация (порядок бросков закреплён)

Ранг по смеси для репутации (`RankMixFor`: последняя строка с порогом не выше репутации; G и F остаются при любой репутации) →
«сложный не по времени» (один бросок всегда) → тип по весам → расстояние → профиль (`BuildProfile`) → награда → срок на доске →
заказчик, место, враг, груз → шаблон описания → намёки → точность описания. **Порядок не менять** — сдвинутся все заказы.

`BuildProfile`: главные оси типа — из диапазона главных осей ранга, второстепенные — второстепенных; далеко без Выживания —
Выживание второстепенной; каждая × случайный множитель; все оси диаграммы не ниже `requirementFloor`; Слаженность — 0.
Описание — шаблон типа + намёки на 1–2 самые большие оси (+ «путь неблизкий», + намёк на потолок группы); одинаковые
предложения склеиваются. Люди видят требования через `DescriptionAccuracy` и свою ошибку оценки риска.

## Репутация

`World.Guild.Reputation` 0..`maxReputation`; меняет только `ReputationService.Change(ctx, Δ, причина)` → `ReputationChanged`
(в лог, без строки ленты). От неё — число и ранги заказов и шанс кандидата. Меняют её задания (успех/провал × ранг, гибель).

## Команды и сценарии

| Команда | Сценарий |
|---|---|
| `SetRegistrarRulesCommand(типы или null, ранг, минимум)` — те же правила без события | `registrar all\|none\|Hunt,Escort Ранг Награда` |
| `AnswerImportantOrderCommand(id, принять, доплата)` — принять: на доске с этого часа, срок от принятия | `answer id accept Доплата` / `answer id decline` |
| `SetSurchargeCommand(id, сумма)` — только на доске, ≥ 0, из казны не списывается сейчас | `surcharge id сумма` |

## Рецепты

- **Новое поле заказа, видимое игроку** — public-геттер с internal-сеттером; скрытое — только internal (игрок и UI его не увидят).
- **Новые данные в событии заказа** — в `OrderSystem.Publish` (там id заказа, тип, ранг, расстояние, награда, названия для ленты).
- **Новое правило отбора** — поле в `RegistrarRules` + `IsAccepted` + `IsSameAs` + `ToString` (формат сценария) + разбор в
  `ScenarioCommands`.

## Лог и отладка

`Debug` — заказ целиком при появлении, со скрытым: `order new #4 Hunt F near reward=95 board=5d important=no hardEarly=no
accuracy=0.97 profile Str=16.2 … hints=…: текст`. `Info` — события заказов. В сводке — «Заказов пришло», «На доску»,
«Отклонено Регистратором», «Отклонено игроком», «Снято по сроку», «Репутация».

## Ловушки

- Id заказа — в данных события (`order`), **не** в участниках: участники — люди.
- Стартовые заказы (`ApplyStart`) — без событий и вне счётчиков; тесты состояния убирают `OrderSystem` и снимают стартовые с доски.
- `OrderSystem_DoesNotShiftOtherSystems` — заказы не должны тратить чужие броски.
- Бот «Простой» отвечает на важные заказы, только когда есть человек нужного ранга; до этого заказ ждёт и уходит без ответа.
