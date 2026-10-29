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
  «Пассивный», «Простой» (приём кандидатов — см. «Люди и репутация» ниже, комиссия 20% первым ходом, правила Регистратора до высшего ранга людей, ответ на
  важные заказы при наличии человека ранга, ранг событийным заданиям по источнику, стройка Общежитие → Лазарет → двор с запасом
  3 месяца расходов — `BuildWithReserveRule`, найм лучшего кандидата за просимую — `HireBestCandidateRule`), «Безрассудный», «Сценарий».
- Сценарий — текст, строка на команду: `такт слово аргументы`, `#` — комментарий, `# seed=N` — зерно. Слова (`ScenarioCommands.Formats`):
  `accept`, `reject`, `autopause Вид on|off`, `commission 0.2`, `registrar …`, `answer id accept Доплата|decline`,
  `eventquest id Ранг|decline`, `surcharge id сумма`, `build IdОпределения`, `buildqueue 1,2`, `unbuild IdПостройки`,
  `offer IdКандидата сумма`, `dismiss IdСотрудника`, `decree IdРаспоряжения on Срок [G,F…]` / `decree Id off`. `ScenarioRecorder` пишет команды бота; незнакомая — комментарием.
- **Новая команда игрока в прогонах** — строка в `ScenarioCommands.Formats`. **Новое поведение бота** — `IBotRule` в списке
  «Простого». **Новый бот** — строка в `Presets`.

## Сводка

Строка — календарный месяц; столбцы: `MonthColumn` (имя, функция от `MonthRecord` — мир на конец месяца и события месяца,
свёртка `Sum`/`Last`/`Mean`, формат) и `PersonColumn` (от `PersonRecord`). **Новый показатель — строка в `SummaryColumns.Monthly`
или `People`**; статьи журнала — сами (`MonthlyFor(data)`). Постройки и персонал — «Построек готово», «В Общежитии», «В Лазарете, %»,
«Легли в Лазарет», «Вылечено в Лазарете», «На дворе, %», «Тренировок», «Персонал», «Нанято», «Невыплат жалованья», «Долг по зарплате»; распоряжения — «Дней: {название}» по каждому (в `MonthlyFor`).
Серия — `SummaryAggregate` (среднее, σ выборки, мин, макс). CSV —
запятая, точка, UTF-8 с BOM.

## Зонды песочницы (меню GuildMaster → Sandbox)

| Меню | Что | Режим |
|---|---|---|
| Time → Log State / Toggle Pause / Speed 1-3 / Debug Speed / Toggle Run In Background | время в Play Mode | Play Mode |
| Adventurers → Log Roster / Accept/Reject First Candidate / Preview Start Lineups | люди, кандидаты, стартовые шестёрки | Play Mode / без |
| State → Light/Heavy Wound First / Stress +50 First / Preview 90 Days | раны, стресс, 90 дней | Play Mode / без |
| Headless → Year Seed 1 Debug / Trace / Year Seeds 1-10 Info / Year Seeds 1-10 Info With Decrees | год в файлы (последний — «Простой» + все четыре распоряжения с первого такта) | без |
| Balance → 10 Years Seeds 1-3 / 1-5 / Apply Overrides To Assets | замер баланса за 10 лет (годовая сводка по рангам; с GM-23 — ещё задания всего, выполнено, «блестяще», соло / в группе / постоянной группой и их выполнено, людей в группах, раундов и раундов на потолке шанса, заданий-угроз и их выполнено), переопределения чисел в памяти из `Logs/Balance/overrides.txt` | без |
| Feed → Dump Templates / Year Feed Seed 1 | вычитка текстов | без |
| Quests → Order Preferences 10 Years | кто какие заказы берёт (~10 с) | без |
| Parties → Groups 10 Years / Chronicle Year Seed 1 / Trace Counts / Cost Benchmark / Year Time With And Without Parties | группы | без |
| UI → Capture All / Capture Current 1920x1080, 1280x720 / Measure Frames 10s / Log Screen State / Restore Game View Size | снимки экранов в `Logs/UiShots/`, замер кадра (скилл `gm-ui`) | Play Mode |
| UI → Import TMP Essentials / Build Font And Theme (`UiSetup`) | шрифт с кириллицей и `UiTheme.asset` | без |
| Generate Game Data | перезаписать ассеты данных | без |

## Через Unity MCP

- Зонд — `execute_menu_item` с путём меню. Вывод `Debug.Log` — `read_console` с `types: ["all"]` (без этого не видно).
- Тесты — `run_tests` (EditMode), подробности — `include_details`.
- Play Mode не крутит кадры без фокуса окна: `Toggle Run In Background` на время проверки, выключить **до** выхода из Play Mode и
  сверить `ProjectSettings.asset` на диске.
- После проверки удалить файлы прогонов из `Logs/` (`sim_*`, `summary_*`, `scenario_*`, `feed_*`, `parties_*`, `UiShots/`).
- Всё, что MCP трогал, вернуть (сцена, выделение, Play Mode); сцену без просьбы не сохранять.

## Как разбирать «почему так вышло»

1. Прогон с зерном на `Debug` (броски) или `Trace` (оценки) — `Year Seed 1 Debug` / окно.
2. Найти строку события (`grep` по id человека `#3` или по типу), выше — `roll …` и `decide …` той же системы и часа.
3. Воспроизвести в тесте: то же зерно + сценарий дают тот же лог (`ScenarioRule`).

## Цена (ориентир)

Год с ботом «Простой» без лога — порядка 1,5–2 с (основная цена — задания и оценка групп); `Info` — десятки тысяч строк в год;
`Trace` в разы больше. Замеры цены отдельных расчётов — `Parties → Cost Benchmark`.
- Обращения (скилл `gm-dilemmas`): бот «Простой» — `AnswerDilemmasRandomlyRule` (наугад или без ответа); сценарий `dilemma Id Вариант`;
  столбцы «Обращений», «Без ответа, %», по видам и вариантам (`MonthlyFor`); лог `Info` — `decide … request: …`, `dilemma #N …: option K`,
  `Debug` — броски `loan-request`, `loot-dispute`, `pardon-redemption`. Зонд: UI → `Spawn Sample Dilemmas`.
- Еда (скилл `gm-state-health`): столбцы «Сытость», «Приёмов: дешёвая / простая / роскошная, %», «Приёмов», «Пропущено приёмов»,
  «Дней в голоде», «Дней в истощении» (`MonthRecord.Meals/MealShare/HungerDays` — `SummaryRecorder` считает приёмы по событиям
  `MealEaten` и часы стадий голода), «Ушли из-за голода»; «Доход: Таверна: еда» и «…: выпивка» — статьи. Лог `Debug` — `meal #3 Имя
  Simple 2 (wallet=…, spare=…)` / `meal … skipped (wallet=0)`; события `MealEaten`/`MealSkipped` — на `Trace`; суточная строка — `satiety=`.
  Распределение кошельков на момент еды — прогон `Year Seed 1 Debug` и разбор строк `meal`.
- Пайки (скилл `gm-state-health` → «Походные пайки»): столбцы «Пайков куплено», «Пайков потеряно», «Делёжек», «Голодных приёмов в пути»,
  «Припасы кончились», «Отступлений из-за еды» (`SummaryColumns.SumOf` — сумма числа из данных событий). Лог `Debug` — `rations quest #N …`
  (покупка), `ration #id Имя Cheap [from …]` (еда в пути и паёк в городе), бросок `ration-loss`; `RationsBought`/`RationsShared` — `Debug`,
  `QuestMealMissed` — `Trace`; решение отступить «из-за еды» — `because food 0.5, …`.
- Поселение (скилл `gm-settlement`): столбцы «Безопасность», «Население», «Уровень» (код: 0 спокойно … 3 осада), «Угроз выполнено»,
  «Угроз упущено» (разница `SettlementTotals` с началом месяца — `MonthRecord.SettlementFrom`: события `SecurityChanged` на Trace, сводка
  их не хранит), «Нападений»; статьи «Таверна: жители», «Ущерб от нападений» — сами. Итог прогона — причина закрытия (`ClosedCause`)
  в логе и в окне Run Headless. Зонд Balance: `10 Years Seeds 1-5 Passive Bot`, в `done.txt` — закрытие и поселение, в `yearly_*.csv` —
  Безопасность, минимум, худший уровень, население, нападения, угрозы по годам. Лог `Debug` — `security 61.5 -> 60.0 expired #42 Hunt E`,
  `population 80 -> 82 month`, бросок `settlement-attack`.

## Люди и репутация (GM-22)

- **Боты.** «Простой» принимает кандидатов `AcceptCandidatesWithReserveRule(SimpleCandidateReserveMonths = 1, SimpleMinPeople = 8)`: пока людей
  (с принятыми в этот ход) меньше 8 — если хватает на подъёмные, дальше — если после них остаётся месяц расходов. **«Безрассудный»**
  (`PlayerBots.Reckless`, в `Presets` и окне Run Headless): комиссия 50%, `AcceptCandidatesWithReserveRule(0)`, Регистратор до высшего ранга,
  `BuildWithReserveRule(0, …)`, `HireBestCandidateRule`, `AnswerDilemmasRandomlyRule`; на важные заказы и событийные задания не отвечает.
  Зонд: Balance → `10 Years Seeds 1-3 Reckless Bot`.
- **Сводка**: «Шанс кандидата» (среднее за месяц, `MonthRecord.CandidateChance`), «Молва» (на конец месяца), «Не в духе, дней», «Ушли вслед за
  другом», «Репутация: остывание» (сумма за месяц), «Репутация: потери» (сумма отрицательных, кроме остывания); «Расход: Подъёмные» — статья.
- **Лог**: `Debug` — броски `candidate`, `sulk-day`, `follow-friend`, строка `reputation day quests=N reputation=…`; `ReputationChanged` несёт
  `reason=…`.
- **Замер 10 лет** (ассеты): «Простой» жив во всех 5 зёрнах, к 5-му году казна ~185 тыс., население ~330; «Безрассудный» к 5-му году ~44 тыс.
  и население ~40, покинут в 2 из 3 зёрен. Таблица — `docs/content/numbers.md` → «Люди и репутация».

## Риск в заданиях (GM-23)

- **Сводка**: «Шанс раунда: доля на потолке, %» (раунды `RoundSuccess`/`RoundFail` с `chance` ≥ `maxRoundChance` − 1e-5) и «Выполнено угроз, %»
  (законченные задания, у типа которых флаг угрозы; экзамены не в счёт). Оба — среднее по месяцам, месяц без раундов или угроз даёт 0 —
  точные доли за год считать по годовой сводке зонда Balance. Потолок читается из `month.Game.Data`.
- **Лог**: строка раунда (`Debug`) по-прежнему `quest #N round K: overlap=… synergy=…`, бросок `round` — с шансом после потолка; бросок
  экзамена `exam` — тоже с потолком, в данных броска — перекрытие.
- **Замер 10 лет** (ассеты): «Простой» — выполнено 95,5%, «блестяще» 90,6% выполненных, групповых 38%, к 5-му году казна ~133 тыс., население
  ~257 (зерно 2 едва не покинуто на 1-м году); «Безрассудный» — покинуто в 3 из 3. Таблица — `docs/content/numbers.md` → «Расчёт раунда».
