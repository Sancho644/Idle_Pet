# Idle_Pet

Рабочий проект уютного 3D-симулятора виртуального питомца. Windows (Steam) и Android (Google Play), free-to-play. Игрок ухаживает за одним существом, обустраивает комнату, наблюдает за поведением, ростом и эволюцией. Питомец продолжает жить вне игры без ограничения времени.

## Разработка
Unity 6000.6.7f1 / URP 17.6.0. Unity CLI и Pipeline 1.0.1, Codex с официальным Unity Agent Plugin 0.1.10-beta. Blender MCP и Mixamo — инструменты будущего арт-пайплайна. Git LFS для бинарных ассетов по TD-004.

## Документация
- [GDD v1.3](docs/design/GDD.md) и [симуляция питомца SD-01–SD-15](docs/design/pet-simulation.md).
- [Стартовый баланс v0.1](docs/design/balance-v0.1.md), [освещение и сон](docs/design/lighting-and-sleep.md), [биологический ритм](docs/design/circadian-rhythm.md) и [пищеварение](docs/design/digestion-and-waste.md).
- [Решения](docs/decisions.md) и [открытые вопросы](docs/open-items.md).
- [TDD v0.2](docs/technical/TDD.md), [ядро симуляции T01](docs/technical/simulation-engine.md), [конфигурации](docs/technical/configuration-system.md), [локализация](docs/technical/localization.md) и [целостность времени](docs/technical/time-integrity.md).
- [Уведомления](docs/design/notifications.md) — утверждённое поведение и варианты реализации T04.
- [Roadmap](docs/roadmap.md), [Backlog](docs/backlog.md), [состояние окружения](docs/technical/setup.md).
- [AGENTS.md](AGENTS.md) — инструкции для агента.

T00 и DESIGN приняты. В DESIGN включены уточнения R-01–R-05 и TD-015–TD-017. T01 готов к началу и ожидает отдельного задания. Результаты проверок и отложенные задачи приведены в описании окружения и backlog.
