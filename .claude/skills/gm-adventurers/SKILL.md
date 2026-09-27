---
name: gm-adventurers
description: Авантюристы GuildMaster — модель Adventurer и AdventurerRoster (активные, архив, кандидаты), генерация AdventurerGenerator и стартовая шестёрка StartScenario, параметры и модификаторы AdventurerStats, архетип ArchetypeCalculator, оси AxisMath, особые черты TraitRules/TraitService, раскрытие RevealService, рост Growth, ранги гильдии GuildRanks, отношения RelationBook/RelationService, приток кандидатов RecruitSystem, уход AdventurerLifecycle. Использовать при работе с людьми, чертами, раскрытиями, архетипами, ростом параметров и рангами.
---

# Авантюристы

Код — `game/Assets/_Project/Scripts/Core/Adventurers/`. Дизайн — `docs/mechanics/adventurers.md`, `traits.md`, `trait-effects.md`,
`archetypes.md`, `training.md`, `lifecycle.md`.

## Карта

| Файл | Что |
|---|---|
| `Adventurer.cs` | человек: `Stats[StatId]`, `Axes[AxisId]` (−100..100), `RevealedAxes`, `Traits` (`TraitInstance`), `GuildRank`, `RankPoints`, `PromotionReadyAtHours`, `ArchetypeId`, `PowerScore`, `State` (`AdventurerState`), `Housing`, счётчики заданий, `IsDeserter`, `IdleOrderDays`, `PartyId`, `PermanentPartyId`, `LeftAtHours`, `LeaveReason`; enum `Gender`, `Housing`, `LeaveReason` |
| `AdventurerRoster.cs` | `Active`, `Archive`, `Candidates` (`Candidate`: человек + `ExpiresAtHours`), `HeadCount`, `TryGetActive`, `TryGetKnown` (и архив) |
| `AdventurerGenerator.cs` | internal: `Generate`, `AddBirthTrait`, `ApplyRankF`, `RerollAxes` |
| `StartScenario.cs` | стартовая шестёрка (поток `StartScenario`, без событий) |
| `AdventurerStats.cs` | `Permanent` / `Effective` / `PermanentProfile`; списки `PermanentModifiers`, `TemporaryModifiers` |
| `Archetypes.cs` | `ArchetypeCalculator.Evaluate/Profile/RoleScoreOf`, `ArchetypeService.Recalculate` |
| `AxisMath.cs` | `PoleOf`, `Strength`, `Multiplier`, `ArrowMultiplier`, `IsExtreme`, `IsOnExtremePole`, `IsNeutral` |
| `Traits.cs` | `TraitRules` (чистые: `CanAdd`, `IsPermanent`, `FindReplaced`, `BirthTraits`, `FindByHook`, `FindWithHook`), `TraitService.TryAcquire/TryRemove` |
| `TraitInstance.cs` | `TraitId`, `Revealed`, `RevealedAtHours`, `PartnerId`, `AcquiredAtHours`, `AffectedStat` (Калека), `SourceQuestTypeId` (Кошмары) |
| `RevealService.cs` | `TryRevealAxis`, `TryRevealTrait`, internal `TryRevealBalanced` |
| `Growth.cs` | `Train`, `PickTrainingStat`, `ApplyQuestExperience`, `ApplyHardQuestComposure`, `ApplyGroupQuestCohesion`, `AddBonus`; чистые `TrainingGain`, `QuestExperienceGain` |
| `GuildRanks.cs` | `AddPoints`, `IsReadyForPromotion`, `Promote`, `FailPromotion`, `CanTakeOrder`, `IsTopRank` |
| `RelationBook.cs`, `RelationService.cs` | пары (симметричные, нет записи — 0), `Change`, `Set`, `AddJointQuest`, `AddJointSuccess`, `LabelsOf`, `AreFriends` |
| `AdventurerSystem.cs` | шаг 13, 00:00: пересчёт архетипа, раскрытие нейтральных осей через `neutralRevealDays` |
| `RecruitSystem.cs` | шаг 13: кандидаты утром, уход без ответа; `AcceptCandidateCommand`, `RejectCandidateCommand` |
| `AdventurerLifecycle.cs` | `Retire(ctx, человек, причина)` — в архив, долг списан (гибель, пропажа), выход из постоянной группы |

## Как устроено

**Параметры.** 14 `StatId`: 8 характеристик (`Vocabulary.IsCharacteristic`) и 6 навыков. Слаженность (`Cohesion`) — множитель
группы, не ось диаграммы задания. `Permanent` = база × постоянные модификаторы (Калека), не ниже естественного минимума —
по нему архетип. `Effective` = `Permanent` × временные (лёгкая рана, усталость выше порога) — для заданий.

**Архетип.** `оценка роли = 0,7·ср(основные) + 0,2·ср(вспомогательные) + 0,1·ср(прочие)`; лучшая ниже `noviceThreshold` —
Новичок; лучшая − среднее всех 14 ниже `jackOfAllTradesThreshold` — Мастер на все руки (PowerScore = среднее); иначе лучшая роль.
Роли — архетипы вида `Role` в порядке GameConfig. `ArchetypeService.Recalculate` вызывается после изменений параметров
(рост и черты вызывают сами) и раз в сутки; смена — `ArchetypeChanged` без автопаузы.

**Оси.** Значение −100..100; полюс по знаку; `Strength = |v|/100` к своему полюсу. Крайний — `|v| ≥ extremePoleThreshold`,
нейтральный — `|v| < neutralThreshold`.

**Черты.** До `maxSpecialTraits`, не больше одной из категории, без несовместимых. Приобретённая вытесняет приобретённую
(`FindReplaced`), кроме постоянной (Калека: эффект `ProfileMultiplier`). Черты с партнёром — Соперник, Влюблённый
(`PartnerId`); у Потерявшего товарища `PartnerId` — погибший. Все черты скрыты до раскрытия; **скрытые действуют так же, как
раскрытые**. `TryAcquire` сам делает раскрытие `OnAcquire` и эффект Проверенного.

**Раскрытие.** Система в своём месте вызывает `RevealService.TryRevealAxis(ctx, человек, ось, триггер)` или
`TryRevealTrait(ctx, человек, id, триггер)`; раскроется, только если `RevealTrigger` совпал с триггером полюса/черты в данных.
Событие `AxisRevealed`/`TraitRevealed` [В] с автопаузой, строка ленты — `revealFeedKey` из данных. Нейтральная ось —
`AxisBalanced` [З] без автопаузы, сама, через `neutralRevealDays`. Время раскрытия — для отчёта месяца.

**Рост.** Замедление к 99: прибавка × (1 − значение/100); характеристика растёт в `characteristicSlowdown` раз медленнее;
от 99 — × `above99Multiplier`. Хладнокровие и Слаженность не тренируются и опыта заданий не получают — у них свои правила
(`ApplyHardQuestComposure`, `ApplyGroupQuestCohesion`). Тренировки на дворе в игре пока нет — `Growth.Train` есть, построек нет.

**Ранг.** Очки дробные (частичный успех × 0,5). Готов к повышению — очков хватает, ранг не последний, прошёл
`PromotionReadyAtHours`; тогда `PromotionOrders` даёт экзамен (скилл `gm-quests`). Заказ берут своего ранга и ниже.

**Генерация** (`AdventurerGenerator.Generate`): пол → имя (без повторов по возможности) → возраст → тип по
`ArchetypeDefinition.generationWeight` → уровень → параметры → оси → черты «от рождения» (с партнёром — только если в гильдии есть
подходящий) → кошелёк, ранг G, город → архетип → стартовое состояние (последние броски). **Порядок бросков не менять.**

**Кандидаты.** Утром раз в сутки шанс `CandidateChance(данные, репутация)`, если `HeadCount < maxAdventurers`; ждут
`candidateWaitDays`. Принять — `AcceptCandidateCommand` (партнёрская черта появляется у партнёра, если он может её взять,
иначе снимается у новичка).

**Уход.** `AdventurerLifecycle.Retire` только перекладывает в архив; **событие и автопаузу публикует вызывающий**
(`AdventurerLeft`, `AdventurerDied`, `AdventurerDisappeared`).

## Рецепты

- **Новый модификатор параметра** — строка в `AdventurerStats.PermanentModifiers` или `TemporaryModifiers`.
- **Новое раскрытие** — значение в конец `RevealTrigger`, повесить на полюс/черту в генераторе данных, вызвать `TryReveal…`
  там, где черта «впервые повлияла на исход».
- **Эффект черты в своей системе** — искать черту по `TraitHook` (`TraitRules.FindWithHook`), числа — в `TraitsBalance`.
- **Человек в тесте** — `StateWorld.Add(черты…)` (все параметры 30, оси 0) или `PartyTestSupport.Person` (скилл `gm-testing`).

## Ловушки

- Реестр без определений — людей нет вовсе (старт и приток пропускаются).
- Черта у кандидата с партнёром — партнёр выбирается из гильдии; при отказе кандидату связь не создаётся.
- `RelationBook` хеширует ключ пары своим `PairKeyComparer` — стандартный хеш `long` вырождался при сотнях людей.
- Лог генерации — `Debug` (`start` / `candidate`: тип, уровень, параметры, оси, скрытые черты).
