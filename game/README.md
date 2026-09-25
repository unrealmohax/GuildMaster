# GuildMaster

Экономический симулятор гильдии авантюристов с непрямым управлением (рабочее название).

- Дизайн-документация — `../docs/`.
- Технические задания на прототип — `../TechJob/`.

## Проект

- **Unity 6000.6.3f1**, шаблон **Universal 2D** (URP с 2D Renderer).
- Asset Serialization: **Force Text**, Version Control: **Visible Meta Files** (уже настроено).
- Открыть: Unity Hub → Add → выбрать эту папку `game/`.

## Репозиторий

Репозиторий — в корне `GuildMaster/` (вместе с `docs/` и `TechJob/`), пока только локальный. Git LFS включён для картинок, звука, моделей и шрифтов (см. `.gitattributes`).

Чтобы отправить на сервер: создать пустой репозиторий (GitHub и т.п.), затем в корне `GuildMaster/`:

```bash
git remote add origin <адрес репозитория>
git push -u origin main
```

`Library/`, `Logs/`, `UserSettings/`, `*.sln`, `*.csproj` исключены в `.gitignore`.
