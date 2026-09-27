---
name: gm-quests
description: Задания GuildMaster — QuestSystem (шаг 4: выход, путь, ночлег, раунды, возвращение), QuestRun/QuestBook/Straggler, расчёт перекрытия и шанса QuestMath (диаграмма 13 осей, синергии, потолки, воспринимаемый шанс), раунд и лестница провалов QuestRounds, моменты напряжения QuestTension (паника, бегство, бросок вперёд, геройство), решения на задании QuestChoices, события в пути и находки QuestEncounters, итог и выплаты QuestSettlement, экзамены PromotionOrders, событийные задания EventQuests, лента задания. Использовать при любой правке хода, расчёта или итога задания.
---

# Задания: ход и расчёт

Код — `game/Assets/_Project/Scripts/Core/Quests/`. Дизайн — `docs/mechanics/quests.md`. Числа — `BalanceSettings.Travel`,
`Rounds` (лестница `FailureLadder`, `maxPartySize`), `Tension`, `Decisions`, `Traits`, `Adventurers`, `Guild`, `Feed.questFeedKeepDays`.

## Карта

| Файл | Что |
|---|---|
| `QuestSystem.cs` | шаг 4, свой поток: `Tick`, `Departures`, `Depart`, `StartLeg`, `StartReturn`, `Advance`, `AddStraggler`, `LeaveQuest` |
| `QuestRun.cs` | `QuestPhase` (TravelOut → AtSite → TravelBack → Returned), `QuestResult` (скрытый уровень), `QuestDiscovery`/`DiscoveryState`, `QuestRun` (участники, `Members`/`Departed`/`TurnedBack`/`Fled`/`Dead`, `Panicked`, `Rushing`, `Round`, `FailedRounds`, `BonusLost`, `LootLost`, `Retreated`, `Completed`, `Log`), `QuestBook` (`Active`, `Finished`, `Stragglers`), `Straggler` |
| `QuestMath.cs` | без побочных эффектов: `Overlap`, `GroupProfile`, `Synergy`, `CeilingHit`, `RiskPerception`, `PerceivedRequirements`, `PerceivedSoloOverlap`, `RealOverlap`, `EncounterProfile`, `ShiftRank`, `RankNumber` |
| `QuestParty.cs` | `Present`, `Leader` (решающий), `Shield` (принимает удар), `MostWounded`, `Best`, `PartyName`, `Publish` (событие задания со всеми данными для строк), `ContextOf` |
| `QuestRounds.cs` | `Resolve` (раунд на месте), `StepFor`, `Wound`, увечье, Лекарь, `Kill` |
| `QuestTension.cs` | `Moment`, `PanicChance`, `TraitChance`, `TryHero`, `Flee` |
| `QuestChoices.cs` | `ExpectedShare`, `ExpenseScale`, `OrderScores` (и для взятия с доски), `DecideAfterFailure`, `DecideExplore` |
| `QuestEncounters.cs` | `TravelEvent`, `PlanDiscovery`, `Discover` |
| `QuestSettlement.cs` | `Finish`, `ResultOf`, `Split`, `ResultKey` |
| `PromotionOrders.cs` | `Offer` — экзамены в начале утра |
| `EventQuests.cs` | `Create` (из находки), `AnswerEventQuestCommand` |
| `QuestReasons.cs` | тексты `reason.fled`, `reason.nightmares` |

## Ход `QuestSystem.Tick`

(без определений — пропуск) → в 00:00 забыть законченные старше `questFeedKeepDays`, ссоры постоянных групп
(`PartyService.CheckQuarrels`) → идущие домой одни (`Stragglers`: повернувший назад — вернулся; беглец — `DeserterReturned` [В]
и `IsDeserter` или `AdventurerDisappeared` + `Retire(Disappeared)`) → в начале утра `PromotionOrders.Offer` → `Departures`
(у кого `PlannedOrderId`: группа выходит вместе, кто не может — остаётся, `Depart`) → `Advance` каждого активного задания.

**`Depart`**: заказ `InProgress`, у людей занятие задания и `QuestRunId`, `QuestParty` (Solo/InGroup), путь туда — броски сразу в
порядке: длительность → событие в пути → находка; строка «Выход»; Соперники в одной группе раскрываются; Хвастун, переоценивший себя,
запоминается (`Overreached`).

**`Advance` на час**: ночлег (`DayRhythm.IsCampHour` → `OnQuestCamp`, утром строка «ночь прошла») → путь (ходовой час, событие
в пути в свой час, находка в свой) → на месте раунд длиной `roundHours` типа → назад → `QuestSettlement.Finish`.

## Раунд (`QuestRounds.Resolve`)

Шанс = `QuestMath.Overlap` (площадь пересечения фигуры группы и требований на 13 осях `StatCatalog.RadarOrder`, по секторам) +
синергии пар (в пределах потолка), в 0..1. Профиль группы = Σ эффективных параметров × (один — 1, группа — Слаженность/100) ×
паника 0,5 × бросок вперёд 1,3. Потолок (`CeilingHit`: людей больше потолка или ось выше потолка оси) — провал сам.

Успех → цель выполнена, путь назад. Провал → ступень `FailureLadder` (стресс, время, бонус, раны — «щит» или бросившийся вперёд,
геройство, Лекарь, снаряжение, добыча, гибель самого тяжело раненого с шансом спасения Лекарем, гибель всех) → моменты
напряжения по каждому → решение группы (`DecideAfterFailure`: решающий — высший ранг, потом Хладнокровие; продолжить или
отступить; «сомнение»/«спор» по разнице ценностей). Экзамен — один раунд без синергий, потолков, лестницы и решений.
**Порядок бросков** — в комментарии `QuestRounds`: раунд, бонус, рана, увечье, геройство, Лекарь, снаряжение, добыча, гибель,
момент напряжения по каждому, решение группы.

## Моменты напряжения (`QuestTension.Moment`, по каждому по порядку выхода)

Ветеран раскрывается → стресс выше порога — срыв (как паника) → бегство (трус на крайнем полюсе, Бывший дезертир) → паника
`max(0,(стресс − Хладнокровие + сдвиг)/делитель) + черты` → бросок вперёд (безрассудный на крайнем полюсе). Кто-то запаниковал —
Железные нервы, кто не дрогнул, «держится». Беглец один — задание кончается отступлением.

## Итог (`QuestSettlement.Finish`)

Уровень (`ResultOf`: блестящий / успех / частичный / провал / катастрофа — скрыт, игрок видит «выполнено / нет») → заказ `Done`/`Failed`
в архив → выполнено: награда − комиссия в казну (событийное задание платит гильдия), доплата из казны, делёж по `PowerScore`
(постоянная группа — поровну) между вернувшимися живыми, долг из доли награды → трофеи (бонус, добыча, тайник) — деньги извне,
Беспринципный утаивает часть → находка, мимо которой прошли, — награда из казны и событийное задание → очки ранга (блестящий × 2,
частичный × 0,5), репутация ± × ранг, опыт, отношения → приобретённые черты (Кошмары, срыв Ветерана), раскрытия → строки →
`PartyService.AfterQuest`. Гибель — `QuestRounds.Kill` (архив, `StressEvents.AdventurerDied`, репутация −, [В] с автопаузой).

## Лента задания

Строки — в `QuestRun.Log` и счётчик в `World.Feed` (`FeedState.AddQuest`); важные для гильдии (`FeedKeys.IsGuildWorthy`: гибель,
отступление, катастрофа) копируются в ленту гильдии; у бегства — своя строка гильдии (`FeedKeys.GuildLineOf`). Условия шаблонов
`Solo`/`Group`, `Far`/`Near`, `QuestType`. Событие задания публикуется через `QuestParty.Publish` — он кладёт `quest`, `order`,
`questType`, ранг, `distance`, `solo`, названия, группу и решающего.

## Рецепты

- **Новая ступень лестницы или исход** — данные `RoundsBalance.FailureLadder` + обработка в `QuestRounds` на своём месте в порядке
  бросков; строка ленты.
- **Новая реакция напряжения** — значение в конец `TensionKind`, эффекты `TensionModifier` в данных, шаг в `QuestTension.Moment`.
- **Новое событие задания** — через `QuestParty.Publish`; частое — `Debug` в `EventLogLevels`; строка — ключ `quest.*` в `FeedKeys`.

## Отладка

- Лог `Info` — решения (`decide … TakeOrder#12`), шанс раунда, строки ленты; мелкие шаги — `Debug`; `Trace` — `order-eval`.
- Сводка: «Заказов взято», «Выполнено», «Не выполнено», уровни результата, «Гибелей», «Бегств», «Раскрытий на заданиях»,
  «Шанс раунда», «Экзаменов сдано»; по людям — «Выполнено», «Не выполнено», «Жив».
- Песочница: GuildMaster → Sandbox → Quests → Order Preferences 10 Years (`QuestProbe`).
- Тестовый мир — `QuestWorld` (`AddOrder`, `Start`, `StartParty`, `AtSite`, `RunToEnd`, `DoEvents`), скилл `gm-testing`.

## Ловушки

- Люди сами берут стартовые заказы. Тесты механик, которым задания мешают, — `PeopleData.NoQuests()`.
- Взятый заказ уходит с доски сразу; не смог выйти — `ReturnToOpen`.
- На задании люди не срываются от стресса по суткам и не уходят из гильдии.
- `QuestSystem_WithoutQuests_DoesNotShiftOtherSystems` — без заданий система не тратит бросков.
