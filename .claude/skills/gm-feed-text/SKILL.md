---
name: gm-feed-text
description: Лента событий и тексты GuildMaster — FeedSystem (событие → ключ → шаблон → вариант → строка), ключи FeedKeys, условия шаблонов FeedConditions, лента гильдии и заданий FeedState/FeedEntry, движок подстановки TextRenderer (метки {имя}, падежи {место:р}, род [м|ж], привязка @метка), значения TextValue/ITextSource/EventTextSource, шаблоны FeedTemplateSet, имена и падежи NounForms, словарь меток TextPlaceholders, тексты причин reason.*. Использовать при добавлении строки ленты, метки, условия шаблона, при ошибках подстановки и склонения.
---

# Лента событий и тексты

Код — `game/Assets/_Project/Scripts/Core/Feed/`, данные — `Scripts/Data/Text/`, шаблоны заполняет
`ClaudeSandbox/Editor/GameDataGenerator.Feed.cs`. Дизайн текстов — `docs/content/event-feed.md`.

## Карта

| Файл | Что |
|---|---|
| `FeedSystem.cs` | шаг перед автопаузой, свой поток: события такта → строки; `ToImportance`, `Mark` ([О]/[З]/[В]) |
| `FeedKeys.cs` | константы ключей; `Keys` (тип события → функция ключа), `Fixed` (ключи, которым обязан быть шаблон), `GuildWorthy`, `GuildLines`; `Of`, `HasLine`, `IsGuildWorthy`, `GuildLineOf` |
| `FeedConditions.cs` | `Checks` (вид условия → проверка), `Select` (самый конкретный шаблон), `AllHold`, `CanCheck` |
| `FeedState.cs`, `FeedEntry.cs` | `World.Feed.Guild` (≤ `guildFeedLimit`), счётчики строк по важности, последний вариант ключа; строка: время, лента, важность, текст, `Links`, `TemplateKey`, `QuestRunId` |
| `TextRenderer.cs` | `Render(шаблон, источник, ошибки)` — общий движок (лента, описания заказов, причины, отчёт) |
| `TextValue.cs` | `Person`, `Noun`, `Word`, `Number`; `ITextSource.TryGet(метка, out значение)` |
| `EventTextSource.cs` | метки из события: таблица `Sources`, `FindPerson`, `PersonValue` |
| `Data/Text/FeedTemplateSet.cs` | `FeedTemplate` (ключ, лента, важность, условия, варианты), `FeedCondition` |
| `Data/Text/TextPlaceholders.cs` | словарь меток: `People`, `Other`, `Gendered`, `DilemmaOnly`, `CaseSuffixes` |
| `Data/Text/NameList.cs`, `Data/Common/NounForms.cs` | имена в 6 падежах; формы и род названий |

## Путь строки (`FeedSystem.Tick`)

Событие такта → ключ `FeedKeys.Of(событие, мир, данные)` (нет ключа — нет строки) → шаблоны ключа `DataRegistry.FeedTemplates` →
`FeedConditions.Select` (все условия выполнены; больше условий — конкретнее; при равенстве — первый в данных) → случайный
вариант, **кроме последнего у этого ключа** (один вариант — без броска) → `TextRenderer.Render` с `EventTextSource` → лента
гильдии или лента задания (`quest` в данных события) → лог `Info`: `feed [В] guild.adventurer.left: Ян ушёл из гильдии: …`.
Важность строки — из шаблона, не из события. Ошибки шаблона — `Error` в лог, строка всё равно пишется с меткой как есть.

Строки заданий: в `QuestRun.Log`; важные для гильдии копируются в ленту гильдии; раскрытие у человека на задании — строка
и в ленте его задания.

## Разметка текстов

- `{метка}` — именительный; `{метка:р|д|в|т|п}` — падеж. Значение в начале предложения — с заглавной.
- `[м|ж]` — род ближайшего **предыдущего** человека-метки в том же предложении, если его нет — ближайшего следующего.
- `[м|ж]@имя` — явная привязка. Названия — только с привязкой и 3–4 формами `[м|ж|ср|мн]@постройка`; нет формы — мужская.
- `[…]@метка`, которой нет в строке, берёт род из источника — разрешено только текстам `reason.*` (они встают в чужую строку).
- Метки людей (`{имя}`, `{напарник}`, `{лекарь}`, `{щит}`, `{решающий}`) склоняются по `NameList`; у событий персонала участников нет —
  `{имя}` берётся из данных события `staff` (`TextValue.Person`); `{постройка}` — `building`. Названия — по `NounForms`
  из данных события (`place`, `enemy`, `client`, `cargo`, `party`, `title`…); числа — `count`, `total`, `amount`, `income`, `expense`;
  `{причина}` — `reason`; `{архетип}` — в роде первого участника.

## Рецепты

**Новое событие со строкой.**
1. Строка в `FeedKeys.Keys` (тип → ключ; вариант по данным события — функция). Ключ-константа; если ключ не из данных — ещё
   в `FeedKeys.Fixed` (валидатор потребует шаблон).
2. Шаблон(ы) в `GameDataGenerator.Feed.cs`: ключ, лента, важность, варианты (лучше несколько — вариант не повторяется подряд),
   условия. Запустить генератор, затем Validate Data.
3. Данные, нужные меткам, положить в событие (`.With("place", …)`). Задание — через `QuestParty.Publish`, заказ — `OrderSystem.Publish`.
4. Важное для гильдии событие задания — в `GuildWorthy`; отдельная строка гильдии — в `GuildLines`.

**Новая метка.** Имя в `TextPlaceholders` (нужный набор; валидатор ловит незнакомые), источник — строка в
`EventTextSource.Sources` (или свой `ITextSource`, как `OrderTextSource`, `PersonTextSource`).

**Новое условие шаблона.** Значение в конец `FeedConditionKind` (+ поле в `FeedCondition`, если нужно), строка в
`FeedConditions.Checks`. Условие без проверки не выполняется — шаблон с ним никогда не выберется.

**Новый текст причины** (`reason.*`) — ключ в `Keys` своего класса (`LeaveReasons`, `QuestReasons`, `PartyReasons`) и шаблон;
выбирается самый конкретный, первый вариант, без бросков; скрытая черта не называется (условие `RevealedTrait`/`RevealedAxisPole`).

## Правила содержания

- Строка не выдаёт скрытое: черта или полюс называются только при условии «раскрыта».
- «Поправился» — только когда зажила последняя рана.

## Проверка

- Тесты: `TextRendererTests`, `FeedTemplateTests` (все шаблоны × 2 пола × 4 рода названий без остатков разметки),
  `FeedSystemTests` (год: каждое событие с ключом даёт строку), `FeedDeterminismTests`. Помощники — `DictionarySource`,
  `FeedTestTemplates`.
- Песочница: GuildMaster → Sandbox → Feed → `Dump Templates` (все варианты для Ян/Мара, ошибки — `!!`), `Year Feed Seed 1`.
  Файлы — в `Logs/`, после просмотра удалить.
- Сводка: «Лента [О]», «Лента [З]», «Лента [В]».

## Ловушки

- `FeedSystem` тратит свой поток на выбор варианта — новые строки не сдвигают чужие броски, но сдвигают выбор вариантов.
- Реестр только из чисел строк не даёт.
- Шаблоны в ассете перезаписывает генератор — правка текста руками в ассете пропадёт.
- **Ссылки в строках.** `EventTextSource` даёт значениям меток `TextLink` (`TextValue.WithLink`), `TextRenderer.Render(…, spans)` пишет
  места подставленных значений, `FeedSystem` кладёт их в `FeedEntry.Spans` — во **все** места создания `FeedEntry`. Новая метка
  с объектом мира — дать ей ссылку, иначе в интерфейсе она не кликается. Интерфейсная сторона — скилл `gm-ui`.
- Распоряжения: ключи `guild.decree.*`; ключ Сухого закона зависит от участника (раскрытый Пьяница или нет) — скилл `gm-decrees`.
- Обращения (скилл `gm-dilemmas`): `guild.dilemma.arrived` (у Трактирщика `{имя}` — из `staff`), ответы — ключи вариантов из данных
  (`feedKey` события). Тексты самих обращений (тело, варианты) рендерит `DilemmaTextSource` (`{сумма}` у варианта — его деньги).
- Еда: ключи `guild.food.hunger` (`HungerStarted`, [З]) и `guild.food.exhausted` (`Exhausted`, [В]) в `FeedKeys.Fixed`; причина
  `reason.hunger` («не мог[|ла]@имя прокормиться»); статьи `ledger.tavernFood` / `ledger.tavernDrinks`. Приёмы пищи строк не дают.
- Пайки: `quest.rations.bought|enough|short` (по `kind` события `RationsBought`, `{число}` — дни еды), `quest.rations.shared`
  (`{имя}` отдал, `{напарник}` получил), `quest.rations.ranOut`, `quest.loss.rations` (вид `rations` у `QuestLoss`). Голодный приём в пути
  (`QuestMealMissed`) строки не даёт.
- Семья: ключи `guild.family.need` [З], `guild.family.ill` [З], `guild.family.died` [В] в `FeedKeys.Fixed`; метка `{родной}` — родной
  первого участника по номеру `relative` в данных события, слово из `NameList.relatives` (род есть: в `TextPlaceholders` — и `Other`,
  и `Gendered`), скобка рода к ней — только с привязкой и тремя формами `[|ла|ло]@родной`; причина `reason.familyNeed`.
- Поселение (скилл `gm-settlement`): метка `{поселение}` (данные `settlement`, род — из названия, скобка — `[|а|о|и]@поселение`, ссылка
  `TextLinkKind.Settlement`), ключи `guild.settlement.*` и `guild.closed.settlement` в `FeedKeys.Fixed`; население за месяц — строка только
  при `reason = month`; `SecurityChanged` строки не даёт.

## Люди и репутация (GM-22)

- Метка **`{друг}`** — человек из данных события `friend` (`EventTextSource`, словарь `TextPlaceholders`) и в тексте причин
  (`PersonTextSource(…, friend)`).
- Ключи: `guild.adventurer.followedFriend` [В] — `AdventurerLeft` с `cause=FollowedFriend` (выбор ключа в `FeedKeys`), `guild.adventurer.sulking` [О] —
  `SulkingDay`; причина `reason.followedFriend` «уш[ёл|ла]@имя вслед за {друг:т}». `CandidateAcceptRefused` и `ReputationChanged` — без строк.
- Шаблоны интерфейса `ui.reputation.reason.*` (9 причин) и `ledger.signingBonuses` — в генераторе `.Feed.cs`.
