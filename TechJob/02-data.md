# GM-02. Данные (ScriptableObject)

| Задача | Ветка | Метки коммитов |
|---|---|---|
| **GM-02** | `GM-02_data` | `[GM-02]` — код, `[TechSpec-02]` — правки этого ТЗ |

Дизайн: [prototype.md → Производство](../docs/prototype.md#производство). Числа: [numbers.md](../docs/content/numbers.md).

## Цель

Все определения и числа прототипа лежат в ScriptableObject и правятся без кода. Симуляция читает их через `DataRegistry`.

## Правила

- ScriptableObject — **только определения** (что бывает), не текущее состояние (что сейчас).
- Во время игры ассеты **не изменяются**.
- У каждого определения есть строковый `id` (латиница, неизменный) и `displayName` (для игрока).
- Корневой ассет `GameConfig` ссылается на все остальные. `DataRegistry` строится из него при старте.
- **Валидатор** (Editor-меню **GuildMaster → Validate Data**): пустые ссылки, дубли id, диапазоны вне допустимого, шаблоны ленты с неизвестными подстановками.

## Список ассетов

| Класс | Кол-во в прототипе | Источник данных |
|---|---|---|
| `GameConfig` | 1 | — |
| `BalanceSettings` | 1 | [numbers.md](../docs/content/numbers.md) + допущения ТЗ |
| `StatCatalog` | 1 | [adventurers.md](../docs/mechanics/adventurers.md) |
| `AxisDefinition` | 6 | [trait-effects.md](../docs/mechanics/trait-effects.md) |
| `SpecialTraitDefinition` | 12 | [trait-effects.md](../docs/mechanics/trait-effects.md) |
| `ArchetypeDefinition` | 7 | [archetypes.md](../docs/mechanics/archetypes.md) |
| `QuestTypeDefinition` | 4 | [quests.md](../docs/mechanics/quests.md) |
| `RandomEventDefinition` | 2 | [quests.md](../docs/mechanics/quests.md), [event-feed.md](../docs/content/event-feed.md) |
| `DiscoveryDefinition` | 1 | [quests.md](../docs/mechanics/quests.md) |
| `BuildingDefinition` | 5 | [numbers.md](../docs/content/numbers.md) |
| `StaffRoleDefinition` | 3 | [staff.md](../docs/mechanics/staff.md), [numbers.md](../docs/content/numbers.md) |
| `DecreeDefinition` | 4 | [laws.md](../docs/mechanics/laws.md), [numbers.md](../docs/content/numbers.md) |
| `DilemmaDefinition` | 6 | [dilemmas.md](../docs/mechanics/dilemmas.md) |
| `FeedTemplateSet` | 1 | [event-feed.md](../docs/content/event-feed.md) |
| `NameList` | 1 | новое |
| `OrderTextTemplates` | 1 | новое |

## Классы

### `StatCatalog`

- Для каждого `StatId` (14): `displayName`, `description`, `isSkill`.
- `radarOrder` — порядок 13 осей диаграммы (без `Cohesion`), утверждённый в [quests.md](../docs/mechanics/quests.md#как-считается-перекрытие-как-в-dispatch):
  `Strength, Reaction, Marksmanship, Stealth, Agility, Medicine, Composure, Charisma, Perception, Survival, Knowledge, Crafting, Endurance`.

### Стрелки влияния (`Arrow`)

Общий тип для силы влияния черт. Множитель на крайнем полюсе оси — из `BalanceSettings`:

| Arrow | Множитель на полюсе |
|---|---|
| `StrongUp` ↑↑ | × 2 |
| `Up` ↑ | × 1,5 |
| `None` | × 1 |
| `Down` ↓ | × 0,5 |
| `StrongDown` ↓↓ | × 0,25 |

Для осей множитель плавно растёт от центра к полюсу (формула — ТЗ 04).

### `AxisDefinition`

- `axisId`, `displayName`, `negativePoleName`, `positivePoleName`.
- Для **каждого полюса** — список эффектов (`TraitEffect`, см. ниже).
- Для каждого полюса — `revealTrigger` (id триггера раскрытия, ТЗ 04) и `revealFeedKey` (строка раскрытия).

### `SpecialTraitDefinition`

- `traitId`, `displayName`, `category` (`Past`, `Habit`, `Strength`, `Relation`, `Acquired`), `isAcquired`.
- `effects` — список `TraitEffect`.
- `codeHooks` — список id особых правил, которые делает код (например, `DrunkardMissesQuest`), см. ТЗ 04.
- `requiresPartner` — черта связана с другим человеком (Соперник, Влюблённый).
- `incompatibleWith` — список несовместимых черт (Железные нервы ↔ Кошмары).
- `revealTrigger`, `revealFeedKey`.

### `TraitEffect` (сериализуемый класс)

| Вид (`EffectKind`) | Параметры | Пример |
|---|---|---|
| `MotiveWeight` | мотив, `Arrow` | Жадный: Деньги ↑↑ |
| `StateRate` | показатель (стресс, довольство, лояльность, усталость), направление (рост / падение), `Arrow` | Трус: рост стресса ↑↑ |
| `RiskPerception` | доля (−0,4…+0,3) | Хвастун: −0,4 |
| `TensionModifier` | вид (паника, бегство, бросок вперёд, геройство), добавка шанса | Железные нервы: паника −0,3 |
| `PayContentmentSensitivity` | множитель | Жадный: × 2 |
| `PartyPreference` | предпочтение размера группы / соло | Одиночка: соло |
| `ProfileMultiplier` | параметр, множитель | Калека: −30% к одной характеристике |

### `ArchetypeDefinition`

- `archetypeId`, `displayName`, `description`.
- `mainStats` (2), `secondaryStats` (1–2).
- `kind`: `Role` (обычный), `JackOfAllTrades` (Мастер на все руки), `Novice` (Новичок).
- `generationBoost` — какие параметры поднимать при генерации человека с этой ролью (ТЗ 04).

Для прототипа: Щит, Боец, Стрелок, Разведчик, Лекарь (`Role`), Мастер на все руки, Новичок.

### `QuestTypeDefinition`

- `questTypeId`, `displayName`.
- `mainAxes`, `secondaryAxes` — шаблон профиля.
- `roundHours` — длительность раунда.
- `maxPartySizeCeiling` — потолок размера группы (0 = нет). В прототипе — 0 у всех типов, доставка тоже (решение 2026-09-26).
- `statCeilings` — список «параметр → предел по рангам» (в прототипе пусто, но движок поддерживает).
- Ключи строк ленты: успех раунда, провал раунда.

### `RandomEventDefinition` / `DiscoveryDefinition`

- `id`, `displayName`, `profileTemplate` (оси и множитель к рангу задания), исходы (раны, потеря, добыча), ключи строк ленты.
- Прототип: `Ambush` (засада разбойников), `Beasts` (нападение зверей), `Cave` (пещера).

### `BuildingDefinition`

- `buildingId`, `displayName`, `cost`, `buildDays`, `upkeepPerMonth`, `capacity`, `builtAtStart`.
- `staffRole` — какая должность работает здесь (или пусто).
- `levelCostMultiplier`, `levelTimeMultiplier` — для улучшения (в прототипе улучшение не обязательно).

### `StaffRoleDefinition`

- `roleId`, `displayName`, `baseSalary`, `requiredBuilding`, описание эффекта уровня.

### `DecreeDefinition`

- `decreeId`, `lawNumber` (номер из [laws.md](../docs/mechanics/laws.md)), `displayName`, `description`, `plusText`, `minusText`.
- `scopeKind`: `None`, `Ranks`.
- `allowedDurations`: бессрочно, 7 дней, 30 дней.
- `isBenefit` — отключение вызывает недовольство.
- Параметры эффекта (цены, множители) — из `BalanceSettings`.

### `DilemmaDefinition`

- `dilemmaId`, `number` (из [dilemmas.md](../docs/mechanics/dilemmas.md)), `title`, `source` (авантюрист / персонал).
- `triggerId` — код триггера, `cooldownDays`.
- `bodyTemplate` — текст с подстановками.
- `options` — 2–3 варианта: `text`, `visibleConsequencesText`, `effects` (id эффектов + параметры), `isRefusal`.
- `timeoutOption` — какой вариант срабатывает без ответа (в прототипе — отказ).

### `FeedTemplateSet`

- Список `FeedTemplate`: `key`, `feed` (`Quest` / `Guild`), `importance` (`Normal` / `Notable` / `Important`), `conditions` (соло, далеко, архетип, раскрытая черта), `variants` (строки).
- Заполняется из [event-feed.md](../docs/content/event-feed.md) один в один.

### `NameList`

- `maleNames`, `femaleNames` (по 60+), `groupNameParts` (для названий постоянных групп: «{прилагательное} {существительное}»: «Серые волки», «Ржавые щиты»).
- ❔ Имена — простые, «немецко-славянского» звучания, без фамилий (Бран, Ида, Ян, Мара, Годрик, Вера…).

### `OrderTextTemplates`

- Заказчики, места, враги по типам заданий; шаблоны описаний; **намёки по осям**.

### `BalanceSettings`

Один ассет со вложенными разделами. Разделы повторяют [numbers.md](../docs/content/numbers.md):

| Раздел | Что внутри |
|---|---|
| `Time` | часы в дне, дни в месяце, месяцы в году, скорости, часы фаз дня |
| `Orders` | заказов в день, срок на доске, шанс «сложного», смесь рангов по репутации, доля «далеко», минимальный порог требований (10) |
| `Ranks` | для G–C: диапазоны главных и второстепенных осей, награды, очки ранга, очки для повышения |
| `Rounds` | синергии, неточность описаний, лестница провалов (шансы по ступеням), шанс спасения Лекарем |
| `Travel` | длительность пути, шансы событий в пути и находок |
| `Adventurers` | генерация параметров, естественный минимум параметров (10), распределение осей, шансы особых черт |
| `Growth` | скорость роста навыков и характеристик, опыт на заданиях |
| `Expenses` | расходы авантюристов |
| `State` | усталость, стресс, довольство, лояльность, кошелёк |
| `Health` | сроки ран, эффект Лазарета, осложнения, увечье |
| `Decisions` | вероятность второго варианта, множители стрелок, ошибки оценки риска, формулы ценности |
| `Tension` | паника, бегство, бросок вперёд, геройство |
| `Guild` | стартовые деньги и репутация, репутация за исходы, приток людей, максимум людей |
| `Staff` | зарплаты, диапазон уровня кандидатов |
| `Economy` | комиссия по умолчанию и пределы, доход таверны, банкротство |
| `Decrees` | цены и эффекты распоряжений |
| `Dilemmas` | срок ответа, лимит открытых, перезарядка, час проверки, числа триггеров (числа вариантов — в `DilemmaDefinition`, решение 2026-09-26) |

## Критерии приёмки

- [ ] Все ассеты из таблицы созданы и заполнены по документам-источникам.
- [ ] В коде `GuildMaster.Core` нет «магических» чисел баланса — только чтение `BalanceSettings`.
- [ ] Валидатор проходит без ошибок.
- [ ] Изменение числа в `BalanceSettings` меняет поведение без перекомпиляции.

## Открытые вопросы

- ❔ Стиль имён (см. `NameList`).
