# Код игры: архитектура

Правила работы — в корневом `CLAUDE.md`. Здесь — устройство кода игры.

**Кода пока нет.** Архитектура задана в `TechJob/01-architecture.md` и будет описана здесь по мере реализации
(сборки, классы, порядок систем в такте, где что лежит, конвенции).

## Кратко по ТЗ

- Сборки: `GuildMaster.Data` (ScriptableObject-определения), `GuildMaster.Core` (симуляция на обычном C#,
  без MonoBehaviour и `UnityEngine.Random`), `GuildMaster.UI` (uGUI), `GuildMaster.Bootstrap` (запуск),
  `GuildMaster.Debugging` (отладка, прогон без UI), `GuildMaster.Tests` (EditMode).
- Такт симуляции — 1 игровой час; мир меняется только командами; случайность — генератор с зерном,
  отдельный поток на систему.
- Числа баланса — только из `BalanceSettings` (источник — `docs/content/numbers.md`).
