# Ending System Builder — Инструкция

`Assets/Editor/EndingSystemBuilder.cs` — единый Editor-скрипт для автоматического создания всех ассетов, префабов и сценических объектов системы концовок.

## Запуск

1. Откройте проект в Unity Editor.
2. Убедитесь, что **GameScene** существует и содержит `SceneObjectRegistry` (есть по умолчанию).
3. Выберите в меню:
   - **`Tools → Bureau → Build Ending System (Full)`** — собрать всё одним кликом.
   - Или отдельные шаги:
     - `1. Build Ending Database` — создать `EndingDatabase.asset`.
     - `2. Build Inspector Archetypes` — клонировать Official → Inspector_Male/Female.
     - `3. Build UI Prefabs` — собрать EndingBook / DismissalScreen / ArcTitle.
     - `4. Build Inspector Cinematic` — собрать CinematicGraph + DialogueGraph.
     - `5. Build Scene Points` — добавить InspectorStandPoint / DoorSpawnPoint.
     - `6. Build ENDING_SYSTEM GameObject` — разместить GameObject на сцене.

## Что создаётся

| Путь | Тип | Описание |
|---|---|---|
| `Assets/Resources/EndingSystem/EndingDatabase.asset` | ScriptableObject | 5 концовок: Law, Empathy, Mask, Ambition, Dismissal |
| `Assets/Resources/EndingSystem/EndingBook.prefab` | Prefab | UI книги учёта (Canvas + Image + TMP + Button + EndingBookUI) |
| `Assets/Resources/EndingSystem/DismissalScreen.prefab` | Prefab | Экран отстранения (Canvas + Image + TMP + 2×Button + DismissalScreenUI) |
| `Assets/Resources/EndingSystem/ArcTitle.prefab` | Prefab | Титр арки (Canvas sortingOrder=9999 + CanvasGroup + TMP + ArcTitleDisplay) |
| `Assets/Resources/CinematicGraphs/InspectorVisit.asset` | ScriptableObject | Кинематик: Start → Move Director → Spawn Inspector → Move Inspector → Call Dialogue → End |
| `Assets/Resources/Dialogues/InspectorDialogue.asset` | ScriptableObject | Диалог: Start → 2× Phrase → Choice (4 варианта) → EventNode(AddTraitPoint) → Phrase → End |
| `Assets/Data/Archetypes/Archetype_Inspector_Male.asset` | ScriptableObject | Клон Official, groupID=Inspector |
| `Assets/Data/Archetypes/Archetype_Inspector_Female.asset` | ScriptableObject | Клон Official, groupID=Inspector |
| `Assets/Scenes/GameScene.unity` (модификация) | Scene | Точки InspectorStandPoint, DoorSpawnPoint + регистрация в SceneObjectRegistry |
| `Assets/Scenes/GameScene.unity` (модификация) | Scene | GameObject `[ENDING_SYSTEM]` с компонентами TraitManager + EndingManager |

## Дополнительные меню

- **`Tools → Bureau → Ending System → Validate Build`** — проверка, что все ассеты созданы корректно (логирует ❌/✅ и подсчитывает ошибки).
- **`Tools → Bureau → Ending System → Clean Build (Delete All)`** — удаляет все созданные ассеты (с подтверждением).

## Особенности

- Скрипт **идемпотентен**: повторный запуск не дублирует ассеты, а обновляет/пропускает уже существующие.
- Для запуска нужна **активная лицензия Unity** (Personal / Pro).
- Скрипт автоматически открывает GameScene — закройте её и сохраните изменения перед запуском.
- Скрипт безопасно обрабатывает конфликты с уже существующими объектами на сцене.

## Ручная доработка (опционально)

После сборки рекомендуется вручную:

1. Открыть `EndingDatabase.asset` и заменить placeholder-описания/иллюстрации/музыку на финальные.
2. Расставить `InspectorStandPoint` (у стола Директора) и `DoorSpawnPoint` (у входа в кабинет) в нужные позиции на сцене.
3. При необходимости — настроить `inspectorVisitGraphName` в EndingManager (по умолчанию `InspectorVisit`).
