# GuildMaster — документация

Дизайн-документация игры (рабочее название **GuildMaster**). Лежит рядом с папкой проекта `game/`, которая уходит в git.

```
GuildMaster/   ← git-репозиторий (всё вместе)
├── game/      ← Unity-проект
├── docs/      ← дизайн-документация (эта папка)
└── TechJob/   ← технические задания на прототип
```

## Содержание

| Файл | Что внутри |
|---|---|
| [game-overview.md](game-overview.md) | Описание игры одним документом, общими словами |
| [concept.md](concept.md) | Концепция: суть игры, столпы, сеттинг, роль игрока |
| [prototype.md](prototype.md) | Прототип: цель, гипотезы, состав, экраны, что определить до сборки (черновик) |
| [core-loop.md](core-loop.md) | Основной игровой цикл: правила вместо ручной работы, ритм месяца |
| [stages.md](stages.md) | Стадии игры: Начало → Развитие → Конкуренция → Глобальное противостояние |
| [mechanics/indirect-control.md](mechanics/indirect-control.md) | Непрямое управление и законы гильдии |
| [mechanics/adventurers.md](mechanics/adventurers.md) | Авантюристы: характеристики, черты характера, поведение |
| [mechanics/buildings.md](mechanics/buildings.md) | Постройки гильдии и города |
| [mechanics/economy.md](mechanics/economy.md) | Экономика: кошелёк авантюриста, доходы и расходы, займы, репутация |
| [mechanics/dilemmas.md](mechanics/dilemmas.md) | Обращения и дилеммы: кто обращается, обещания, цепочки, список |
| [mechanics/laws.md](mechanics/laws.md) | Законы гильдии: три типа (распоряжения, глобальные, путь), список |
| [mechanics/decision-model.md](mechanics/decision-model.md) | Модель решений авантюриста: мотивы, запреты, влияние игрока |
| [mechanics/state.md](mechanics/state.md) | Состояние человека: здоровье, усталость, стресс, довольство, лояльность, кошелёк |
| [mechanics/traits.md](mechanics/traits.md) | Черты характера: оси, особые черты, скрытые черты |
| [mechanics/quests.md](mechanics/quests.md) | Задания: как видны игроку, источники, рычаги, типы |
| [mechanics/staff.md](mechanics/staff.md) | Персонал гильдии: должности, прямое управление |
| [mechanics/groups.md](mechanics/groups.md) | Группы: кто собирает, координатор, распад групп |
| [mechanics/trait-effects.md](mechanics/trait-effects.md) | Черта → влияние: как черты работают в механизмах |
| [mechanics/archetypes.md](mechanics/archetypes.md) | Архетипы авантюристов: роли и нужные им параметры |
| [mechanics/health.md](mechanics/health.md) | Здоровье: ранения и болезни |
| [mechanics/lifecycle.md](mechanics/lifecycle.md) | Жизненный цикл: возраст, уход, смерть, наследие |
| [mechanics/special-squad.md](mechanics/special-squad.md) | Спецгруппа гильдии |
| [mechanics/training.md](mechanics/training.md) | Места прокачки характеристик |
| [mechanics/time.md](mechanics/time.md) | Течение времени |
| [mechanics/city.md](mechanics/city.md) | Развитие города |
| [content/event-feed.md](content/event-feed.md) | Шаблоны ленты событий (прототип) |
| [content/numbers.md](content/numbers.md) | Числа-заглушки для прототипа (черновик) |
| [world/setting.md](world/setting.md) | Мир: без магии, Скверна, окраина королевства, окрестности |
| [research/competitors.md](research/competitors.md) | Похожие игры и референсы |
| [decisions.md](decisions.md) | Журнал принятых решений |
| [open-questions.md](open-questions.md) | Открытые вопросы |

## Условные обозначения

В документах разделяем, что уже решено, а что только предложено:

- **✅ Решено** — определил автор.
- **❔ Под вопросом** — решено предварительно, может быть пересмотрено.
- **💡 Идеи на обсуждение** — предложения, ещё не принятые. Их можно спокойно вычёркивать.
