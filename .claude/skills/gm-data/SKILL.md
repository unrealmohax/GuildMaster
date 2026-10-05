---
name: gm-data
description: Слой данных GuildMaster — ScriptableObject-определения (GameConfig, Definition, оси, черты, архетипы, типы заданий, события, находки), числа баланса BalanceSettings и его разделы, DataRegistry, эффекты черт TraitEffect и кодовые правила TraitHook/RevealTrigger, словарь перечислений, валидатор данных (GuildMaster → Validate Data) и генератор ассетов в песочнице. Использовать при добавлении числа баланса, поля или определения, при правке ассетов в Data/, при ошибках валидатора и при вопросах «где лежит это число».
---

# Данные: определения, баланс, валидатор

Код — `game/Assets/_Project/Scripts/Data/` (сборка `GuildMaster.Data`, без логики). Ассеты — `game/Assets/_Project/Data/`.
Дизайн чисел — `docs/content/numbers.md`, эффекты черт — `docs/mechanics/trait-effects.md`.

## Карта

| Где | Что |
|---|---|
| `Data/GameConfig.cs` | корневой ассет: `Balance`, `StatCatalog`, списки `Axes`, `SpecialTraits`, `Archetypes`, `QuestTypes`, `RandomEvents`, `Discoveries`, `Buildings`, `StaffRoles`, `Decrees`, `Dilemmas`, `FeedTemplates`, `NameList`, `OrderTexts`; `AllDefinitions()` |
| `Data/Balance/BalanceSettings.cs` + 19 разделов | `Time`, `Orders`, `Ranks`, `Rounds`, `Travel`, `Adventurers`, `Growth`, `Expenses`, `State`, `Health`, `Decisions`, `Tension`, `Traits`, `Guild`, `Staff`, `Economy`, `Decrees`, `Dilemmas`, `Feed` |
| `Data/Definitions/` | `AxisDefinition` (+ полюса с эффектами и триггерами), `SpecialTraitDefinition`, `TraitEffect`, `ArchetypeDefinition`, `QuestTypeDefinition` (+ `StatCeiling`), `EncounterDefinition`, `RandomEventDefinition`, `DiscoveryDefinition`, `BuildingDefinition`, `StaffRoleDefinition`, `DecreeDefinition`, `DilemmaDefinition`, `StatCatalog` (порядок осей диаграммы `RadarOrder`) |
| `Data/Text/` | `FeedTemplateSet` (+ `FeedTemplate`, `FeedCondition`), `NameList` (имена в 6 падежах, части названий групп), `OrderTextTemplates`, `TextPlaceholders` (словарь меток) |
| `Data/Vocabulary/` | `Vocabulary.cs` (`StatId` 14, `AxisId` 6, `AxisPole`, `Motive` 6, `GuildRank` G–C, падежи, род), `EffectEnums.cs`, `DefinitionEnums.cs` (`TraitHook`, `RevealTrigger`, `TraitCategory`, `FeedConditionKind`, `DilemmaTrigger`…) |
| `Data/Common/` | `Definition` (id + displayName), `NounForms`, `IntRange`, `FloatRange`, `[OptionalReference]` |
| `Core/Simulation/DataRegistry.cs` | доступ из симуляции |
| `Debugging/Validation/` | `DataValidator` (≈700 строк: `Check…` по видам), `SerializedFieldCheck`, `TextMarkupCheck`, `DataValidatorMenu` |
| `ClaudeSandbox/Editor/GameDataGenerator*.cs` | генератор всех ассетов, кроме баланса |

## Правила

- **ScriptableObject — только определения и числа**, во время игры не меняются. Поля `[SerializeField] private` + геттеры,
  сеттеров нет. Текущее состояние — только в `WorldState`.
- **Числа баланса — только в `BalanceSettings`.** В Core констант баланса нет. Значение по умолчанию поля = стартовое число
  из документов; ими заполняется новый ассет и тесты (`TestData`, `PeopleData`). Дальше источник истины — ассет.
- **Перечисления, сохранённые в ассетах** (`StatId`, `AxisId`, `TraitHook`, `RevealTrigger`, `EffectKind`, `FeedConditionKind`,
  `DilemmaEffectKind`…), хранятся числами — **новые значения только в конец**, существующие не переставлять.
- Черты и правила находятся **по `TraitHook`, а не по id**: `TraitRules.FindByHook(data, hook)` / `TraitRules.FindWithHook(человек, hook, data)`.
  Данные говорят «какое правило», код реализует его в своей системе.
- Эффекты осей и черт — список `TraitEffect` (вид `EffectKind`: `MotiveWeight`, `StateRate`, `RiskPerception`, `TensionModifier`,
  `PayContentmentSensitivity`, `PartyPreference`, `ProfileMultiplier`; условие `EffectCondition`). У оси сила эффекта растёт
  от центра к полюсу (`AxisMath.ArrowMultiplier`), у особой черты — полная.
- Тексты: `NounForms` — 6 падежей + род; у имён и названий заказов все формы обязательны (валидатор). Ключи ленты —
  строки; определения ссылаются на них полями `…FeedKey` (`revealFeedKey`, `balancedFeedKey`, `roundSuccessFeedKey`…).
- Числа вариантов дилемм — в самих `DilemmaDefinition`, не в балансе; сила эффектов черт — стрелками или долями в `TraitEffect`.

## DataRegistry

`DataRegistry.FromConfig(config)` — полный; `new DataRegistry(balance)` — только числа (`HasDefinitions == false`: людей,
заказов, заданий и строк нет). Дубль id или пустая ссылка в списке — исключение при сборке.
Методы: `Get<T>(id)`, `TryGet<T>(id, out)`, `All<T>()` (в порядке GameConfig), `Axis(AxisId)`, `FeedTemplates(ключ)`,
`HasFeedKey`, `NameForms(имя)`, свойства `Balance`, `Config`, `Stats`, `Names`, `OrderTexts`.

## Рецепты

**Новое число баланса.**
1. Поле `[SerializeField] private … = стартовое;` + геттер в нужном разделе `Data/Balance/*Balance.cs` (`[Min]`/`[Range]` — валидатор проверит).
2. Читать в Core как `ctx.Data.Balance.Раздел.Поле`.
3. Ассет `BalanceSettings.asset` сам не обновится: новое поле получит значение по умолчанию при следующей сериализации
   (генератор данных сохраняет ассет, баланс не перезаписывая). Проверить в ассете.
4. Число записать в `docs/content/numbers.md`.
5. В тестах менять так: `PeopleData.Set("раздел.поле", значение)` или `GameData.Edit(data.Balance, "раздел.поле", p => …)`.

**Новое определение / поле определения.** Класс или поле в `Data/Definitions/`; заполнение — в генераторе
(`ClaudeSandbox/Editor/GameDataGenerator*.cs`, приватные поля ставятся отражением); если это новый вид определений — список
в `GameConfig` и `AllDefinitions()`; проверка — в `DataValidator` (и ожидаемое число в таблице количеств в его начале).

**Новое кодовое правило черты.** Значение в конец `TraitHook` (или `RevealTrigger`), в генераторе — повесить на черту,
в системе — искать по хуку.

## Генератор и валидатор

- **GuildMaster → Sandbox → Generate Game Data** перезаписывает определения, шаблоны ленты, имена и тексты заказов целиком;
  `BalanceSettings` не трогает. Правки ассетов руками генератор затрёт — правку вносить в генератор.
- Ассеты правятся через Unity Editor/MCP или генератор, не текстом YAML.
- **GuildMaster → Validate Data** (`DataValidator.Validate(config)`): отражением обходит поля (пустые ссылки, кроме
  `[OptionalReference]`; `[Range]`/`[Min]`; диапазоны min ≤ max), дубли id, связи, разметку текстов. Требует шаблоны для:
  `FeedKeys.Fixed`, `LeaveReasons.Keys`, `QuestReasons.Keys`, `PartyReasons.Keys`, `MonthReportSections.TextKeys`,
  `balancedFeedKey` осей; все 6 падежей у имён. Ошибка — данные битые, предупреждение — подозрительно (норма — 0).
- Через MCP итоговую строку валидатора видно только в `read_console` с `types: ["all"]`; полный отчёт — вывод теста
  `DataValidatorTests.RealConfig_HasNoErrors` (`run_tests` с `include_details`).

## Ловушки

- Тесты с людьми берут **реальные определения, но баланс по умолчанию** (`PeopleData`) — правка ассета баланса тесты не ломает,
  а правка значения по умолчанию в коде — ломает.
- Комментарии, `[Tooltip]`, `[Header]` в Data — без ссылок на документы и ТЗ (исключение — генератор в песочнице).
- Распоряжения находятся по `DecreeDefinition.Effect` (`DecreeEffect`), не по id; валидатор требует правило и его уникальность.
