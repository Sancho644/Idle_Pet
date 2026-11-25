# Состояние окружения — 25.11.2026

- Unity `6000.6.7f1`, URP `17.6.0`, Input System `1.20.1`, UGUI `2.6.0`, Test Framework `1.8.0`.
- Unity CLI и Pipeline `1.0.1`, официальный Unity Agent Plugin `0.1.10-beta` в Codex. Pipeline подключён к открытому редактору, исходная активная платформа `StandaloneWindows64` восстановлена после сборок.
- `SampleScene` содержит `Main Camera`, `Directional Light` и `Global Volume`. В настройках качества используются `PC_RPAsset` и `Mobile_RPAsset`.
- Установлены Windows Mono и Android Build Support, SDK/NDK и OpenJDK. Windows IL2CPP отсутствует. Установка VContainer отложена до Bootstrap по TD-003.

## Компиляция и сборки T00

Результаты компиляции, EditMode-теста и обеих сборок приняты.

- Компиляция прошла. Unity Test Runner обнаружил `IdlePet.Tests.EditMode` и выполнил один тест графа сборок. Тест подтвердил ссылки на Domain и Application и отсутствие ссылок на UnityEngine и UnityEditor в Domain.
- Windows x64 Mono Development из `SampleScene` собрана в `Builds/T00/Windows/Idle_Pet.exe`, BuildReport содержит 0 ошибок и 0 предупреждений. Проверка запуска подтвердила инициализацию Mono и Direct3D 12. В журнале Player отмечено предупреждение о недоступности интерфейса D3D12 Info Queue, запуск продолжился. Визуальная проверка не проводилась.
- Android ARM64 IL2CPP Development из `SampleScene` собрана в `Builds/T00/Android/Idle_Pet.apk`. В APK подтверждены ABI `arm64-v8a`, `libil2cpp.so` и флаг `application-debuggable`. Использован исходный шаблонный application ID. ADB не обнаружил устройства, запуск APK на устройстве не проверен.
- BuildReport Android содержит 0 ошибок и 1 предупреждение. Включённый Diagnostics Data требует `SymbolTable` или `Full` для расшифровки стеков в отчётах о сбоях. После сборок консоль редактора показывала 0 ошибок и 1 предупреждение Android. Предупреждение Diagnostics Data и сообщение D3D12 Info Queue не блокируют T00. Автоматические изменения сериализованных настроек от сборок убраны из рабочей копии.

Запуск на физическом Android-устройстве и визуальная проверка Windows Player перенесены на этап первого игрового прототипа. Обе задачи открыты в [backlog.md](../backlog.md).

## Git и LFS

- Ветка T00 `feature/project-setup` создана от `main` на `770d343`. Pipeline `1.0.1` и его зависимости зафиксированы коммитом `68354a9`.
- Git LFS `3.8.1`, фильтры и локальный `pre-push` доступны. `git check-attr` подтвердил шесть расширений из TD-004 и обычное хранение Unity YAML, C# и `.meta`. `.gitattributes` зафиксирован коммитом `b137c3e`. Отслеживаемых LFS-объектов пока нет. Миграция истории не проводилась.
- Содержимое `Builds/T00/` игнорируется Git. Текущие статусы задач приведены в [backlog.md](../backlog.md).
