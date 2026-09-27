---
name: gm-parties
description: Группы GuildMaster — Party и PartyBook (группы под задание и постоянные), оценка группы глазами человека PartyMath/PartyLens/ProfileCache/PresumedParty (перекрытие, шанс, доля, «Товарищи», оценка напарника), сбор группы и приглашения в DecisionSystem.Parties, решения постоянных групп, выход группой, жизнь групп PartyService (складывается, гость → член, ссоры, одиночка, распад), причины отказа PartyReasons. Использовать при правке того, как люди собираются в группы и как живут постоянные группы.
---

# Группы

Код — `game/Assets/_Project/Scripts/Core/Parties/` и `Core/Decisions/DecisionSystem.Parties.cs`. Дизайн — `docs/mechanics/groups.md`.
Числа — `BalanceSettings.Decisions` («Товарищи» в группе, `lonerGroupCompanions`, оценка напарника, постоянные группы),
`Rounds.maxPartySize`, `Growth` (Слаженность). Толкования — `TechJob/12-groups.md`.

Отдельной `PartySystem` нет: решения — в `DecisionSystem`, выход и итог — в `QuestSystem`/`QuestSettlement`.

## Карта

| Файл | Что |
|---|---|
| `Party.cs` | `Party` (`IsPermanent`, `Name`, `MemberIds`, `InitiatorId`, `OrderId`, `JointQuests`, `JointSuccesses`, `GetQuestsTogether`, `GetGuestSuccesses`), `PartyBook` (`Active`, `TryGetParty`, `GetPermanentCount`, `IsNameUsed`, счётчики, свой счётчик id) |
| `PartyMath.cs` | `PartyMath` (обёртки), `PartyLens` (взгляд одного на один заказ: `Overlap`, `Chance`, `Scores`, `Value`), `ProfileCache` (`Solo`, `Grouped`), `PresumedParty` (`Values`, `MinimumAbove`), `Companions`, `ExpectedShare`, `PartnerScore`, `BestPartner`, `Presume`, `MeanGroupValue` |
| `PartyService.cs` | `StartGathering`, `Release`, `Attach`, `AfterQuest`, `TryForm`, `Form`, `CheckQuarrels`, `GroupLoyalty`, `OnRetired`, `Members`, `NameValue`, `NameOf`; поток названий `NameStream = "Parties"` |
| `PartyReasons.cs` | `InvitationRefusal`, `Of`, `Text`, `Keys` (`reason.invite.*`) |
| `DecisionSystem.Parties.cs` | `AddSeekParty`, `Gather`, `InviteWhileValueGrows`, `Invite`, `PermanentPartiesDecide`, запись в лог |

## Состояние у людей и заданий

У человека — `PartyId` (с какой группой собирается или идёт; 0 — ни с какой) и `PermanentPartyId`. У задания — `PartyId`,
`IsPermanentParty`, `PartyTitle`. У пары отношений — `JointSuccesses`.

## Сбор группы под заказ (в часу решения)

1. Утром и «освободился» к каждому разрешённому заказу (не экзамену) — `SeekParty`, если `PartyMath.Presume` дал группу не из одного:
   он + лучшие свободные кандидаты по `PartnerScore` (прирост перекрытия, отношения, дружба, разница рангов), пока растёт ценность.
2. Выбран `SeekParty` → `Gather`: заказ снят с доски (`OrderChoice.Reserve`), `PartyService.StartGathering`, `InviteWhileValueGrows`:
   зовёт по одному лучшего, пока ценность растёт и в группе меньше `maxPartySize`.
3. `Invite`: приглашённый сравнивает `JoinParty` (этот заказ с этой группой) со своими вариантами точки — обычный `Choose`
   с броском `decision-best`. Отказ — `InvitationDeclined` с причиной `PartyReasons`; взял вместо этого заказ один — раскрывается Одиночка.
4. Собрал не меньше `MinimumAbove` (приемлемый минимум против лучшего другого варианта) — `Commit`, выход в следующем часу.
   Нет — идёт один, если лучше отдыха, иначе заказ снова на доске (`Release`), Командный раскрывается.

## Постоянные группы

- **Решают утром раньше всех** (`PermanentPartiesDecide`): лидер (`QuestParty.Leader` среди свободных членов) сравнивает заказы
  по средней по членам ценности и среднюю ценность отдыха; выбран заказ — каждый член решает как приглашённый (может выйти на
  день), потом лидер зовёт гостей, пока растёт средняя ценность. Идут, если их хотя бы двое.
- **Складывается** (`PartyService.TryForm`, после выполненного задания): наибольший набор вернувшихся без постоянной группы,
  где у каждой пары ≥ `permanentPartySuccesses` совместных успехов и отношения ≥ `permanentPartyMinRelation`. Название —
  «прилагательное существительное» из `NameList`, без повторов; бросок — поток `Parties`.
- **Итог задания**: делёж поровну (`QuestSettlement.Split(…, equal: true)`), Слаженность члена +, гостя — меньше; выполнено —
  +1 совместный успех каждой паре. `AfterQuest`: группа под задание удаляется; постоянная считает задание; гость копит успехи
  и после `newMemberSuccesses` становится членом; одиночка уходит после `lonerLeavesAfterQuests`.
- **Теряет людей**: ссора (раз в сутки `CheckQuarrels` и после заданий: отношения двух членов ≤ `permanentPartyBreakRelation` —
  уходит тот, у кого хуже `GroupLoyalty`), одиночка, гибель и уход из гильдии (`OnRetired` из `AdventurerLifecycle.Retire`).
  Остался один — распалась.

## Выход (`QuestSystem.Departures`)

Группа выходит вместе: первым инициатор/лидер, дальше по порядку в гильдии; кто не может — `PartyService.Release`; остался один —
задание без группы (`Attach`). Соперники в одной группе раскрываются при выходе.

## События и тексты

`PartyGathered`, `PartyNotGathered`, `InvitationDeclined`, `PermanentPartyFormed`, `PermanentPartyDisbanded`, `PartyMemberLeft`
(`cause`: quarrel / loner / gone), `PartyMemberJoined`. Строки — `guild.party.*` (сбор по числу людей: `.pair`, `.three`,
без суффикса — четверо, `.five`, `.six`; `permanentSetOut`, `noPartners(.solo)`, `declined(.solo)`, `permanentFormed(.pair)`,
`permanentDisbanded`, `memberLeft.quarrel`/`.loner`, `memberJoined`). `{группа}` постоянной — `PartyService.NameValue`
(«группа «Серые волки»» в 6 падежах), `{название}` — данные события `title`.

## Лог

`Info`: `party #N seek: …` (предполагаемая группа, минимум, ценности), `decide #5 Имя invite #3: JoinParty#12 …`,
`party #N gathered for #12: …`, `party #N not gathered: …`, `party #N Название decide (#3 Имя): PartyOrder#12 …`.
`Debug` — почему перестал звать. `Trace` — `party-presume`, `partner-eval`.

## Производительность

Оценка групп — основная цена года после заданий. `PartyLens` считает требования и веса один раз; `ProfileCache` живёт час
(`DecisionScope.Profiles`). Шнурки перекрытия — без массивов. Замер — GuildMaster → Sandbox → Parties → `Cost Benchmark`,
`Year Time With And Without Parties`, `Trace Counts Seed 1`.

## Ловушки

- Пока группа невозможна (один свободный человек), код групп **не тратит бросков** — тест `NoPartyPossible_NoRolls_SameLogAsWithoutParties`.
  Любая правка, которая бросает «на всякий случай», сдвинет мир.
- Кто ответил на приглашение, в этом часу второй раз не решает (`decidedThisHour`).
- Экзамен — только одному (запрет в `DecisionBans`).
- Песочница: `Groups 10 Years` (≈40 с) сравнивает с прогоном без групп; `Chronicle Year Seed 1` — летопись постоянных групп в `Logs/`.
