---
name: gm-debug-headless
description: Отладка и прогоны GuildMaster без интерфейса — лог симуляции SimLogger (уровни Error/Info/Debug/Trace, формат строк, EventLogLevels, AdventurerLog), прогон HeadlessRun и окно GuildMaster → Run Headless…, боты PlayerBot/PlayerBots и правила IBotRule, сценарии ScenarioScript/ScenarioCommands, сводка RunSummary/SummaryColumns/SummaryAggregate/SummaryCsv, меню-зонды песочницы (Sandbox → Time/Adventurers/State/Headless/Feed/Quests/Parties) и их запуск через Unity MCP. Использовать для диагностики поведения симуляции, прогона года или серии зёрен, замеров баланса и производительности, добавления столбца сводки, команды сценария или бота.
---

# Лог, прогоны, сводка, зонды

Код — `game/Assets/_Project/Scripts/Core/Logging/`, `Scripts/Debugging/Headless/`; зонды — `game/Assets/_Project/ClaudeSandbox/Editor/`
(описание каждого — `ClaudeSandbox/README.md`). Файлы прогонов — `game/Logs/` (в `.gitignore`).

## Лог симуляции

- Строка: `[Год.Месяц.День ЧЧ:00] [Система] [Уровень] текст`, числа без культуры. Логгер пишет свой уровень и всё выше.
- Уровни: `Error` — исключения, ошибки шаблонов; `Info` — события, решения (`decide …`), группы, строки ленты (`feed …`), отчёт
  месяца; `Debug` — броски (`roll …`), генерация людей и заказов, запреты, журнал казны, мелкие шаги заданий; `Trace` — оценки
  вариантов (`scores`, `order-eval`, `party-presume`, `partner-eval`), суточная строка каждого, часы и фазы.
- Уровень события — `EventLogLevels` (по умолчанию `Info`). Событие пишется сразу после шага своей системы, **после её бросков**.
- Код логирования: простые строки — `ctx.Log.Write<T0…T3>(уровень, формат, …)` (формат только при включённом уровне, без упаковки);
  сложные — `if (log.IsOn(уровень)) { var b = log.Begin(уровень); …; log.Commit(); }`. **Не передавать в `Write` строку со склейкой** —
  она построится даже на выключенном уровне.
- Лог случайных чисел не тратит и мир не меняет (тест `SimulationLogTests`).

## Прогон

`HeadlessRun.Run(data, зерно, дней, бот, лог[, watch])` — перед каждым тактом ходит бот, затем такт; `Result` (симуляция, такты,
события, время, строк лога, сводка, сценарий, `GuildClosed`). `RunToFiles(config, Options, прогресс)` — серия: прогон `i` — зерно
`Seed + i`; файлы `sim_{зерно}_{дата}.log`, `summary_{зерно}.csv`, `scenario_{зерно}.txt`, при серии — `summary_{зерно}_x{N}.csv`.
Первая строка лога — параметры прогона; команды бота — `[Bot] [Info] send …`.

Окно **GuildMaster → Run Headless…**: Game Config, зерно, дней (360), уровень лога, бот, прогонов (1–100), «Открыть папку Logs».

## Боты и сценарии

- `PlayerBot` = имя + список `IBotRule` (правило получает `BotTurn`: мир на чтение, `Send`, номер такта). Готовые — `PlayerBots.Presets`:
  «Пассивный», «Простой» (принять всех кандидатов, комиссия 20% первым ходом, правила Регистратора до высшего ранга людей, ответ на
  важные заказы при наличии человека ранга, ранг событийным заданиям по источнику), «Сценарий».
- Сценарий — текст, строка на команду: `такт слово аргументы`, `#` — комментарий, `# seed=N` — зерно. Слова (`ScenarioCommands.Formats`):
  `accept`, `reject`, `autopause Вид on|off`, `commission 0.2`, `registrar …`, `answer id accept Доплата|decline`,
  `eventquest id Ранг|decline`, `surcharge id сумма`. `ScenarioRecorder` пишет команды бота; незнакомая — комментарием.
- **Новая команда игрока в прогонах** — строка в `ScenarioCommands.Formats`. **Новое поведение бота** — `IBotRule` в списке
  «Простого». **Новый бот** — строка в `Presets`.

## Сводка

Строка — календарный месяц; столбцы: `MonthColumn` (имя, функция от `MonthRecord` — мир на конец месяца и события месяца,
свёртка `Sum`/`Last`/`Mean`, формат) и `PersonColumn` (от `PersonRecord`). **Новый показатель — строка в `SummaryColumns.Monthly`
или `People`**; статьи журнала — сами (`MonthlyFor(data)`). Серия — `SummaryAggregate` (среднее, σ выборки, мин, макс). CSV —
запятая, точка, UTF-8 с BOM.

## Зонды песочницы (меню GuildMaster → Sandbox)

| Меню | Что | Режим |
|---|---|---|
| Time → Log State / Toggle Pause / Speed 1-3 / Debug Speed / Toggle Run In Background | время в Play Mode | Play Mode |
| Adventurers → Log Roster / Accept/Reject First Candidate / Preview Start Lineups | люди, кандидаты, стартовые шестёрки | Play Mode / без |
| State → Light/Heavy Wound First / Stress +50 First / Preview 90 Days | раны, стресс, 90 дней | Play Mode / без |
| Headless → Year Seed 1 Debug / Trace / Year Seeds 1-10 Info | год в файлы | без |
| Feed → Dump Templates / Year Feed Seed 1 | вычитка текстов | без |
| Quests → Order Preferences 10 Years | кто какие заказы берёт (~10 с) | без |
| Parties → Groups 10 Years / Chronicle Year Seed 1 / Trace Counts / Cost Benchmark / Year Time With And Without Parties | группы | без |
| Generate Game Data | перезаписать ассеты данных | без |

## Через Unity MCP

- Зонд — `execute_menu_item` с путём меню. Вывод `Debug.Log` — `read_console` с `types: ["all"]` (без этого не видно).
- Тесты — `run_tests` (EditMode), подробности — `include_details`.
- Play Mode не крутит кадры без фокуса окна: `Toggle Run In Background` на время проверки, выключить **до** выхода из Play Mode и
  сверить `ProjectSettings.asset` на диске.
- После проверки удалить файлы прогонов из `Logs/` (`sim_*`, `summary_*`, `scenario_*`, `feed_*`, `parties_*`).
- Всё, что MCP трогал, вернуть (сцена, выделение, Play Mode); сцену без просьбы не сохранять.

## Как разбирать «почему так вышло»

1. Прогон с зерном на `Debug` (броски) или `Trace` (оценки) — `Year Seed 1 Debug` / окно.
2. Найти строку события (`grep` по id человека `#3` или по типу), выше — `roll …` и `decide …` той же системы и часа.
3. Воспроизвести в тесте: то же зерно + сценарий дают тот же лог (`ScenarioRule`).

## Цена (ориентир)

Год с ботом «Простой» без лога — порядка 1,5–2 с (основная цена — задания и оценка групп); `Info` — десятки тысяч строк в год;
`Trace` в разы больше. Замеры цены отдельных расчётов — `Parties → Cost Benchmark`.
