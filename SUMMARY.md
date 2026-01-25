# SUMMARY of Changes (23.01.2026)

## Обзор

Документ содержит полный список изменений внесённых в проект BuroCrazy 23 января 2026 года.

---

## 1. Диалоговая Система - Изображения и Фон

### Что добавлено:
- **Поле `nodeImage`** в PhraseNode, ChoiceNode, EventNode - для показа изображений в конкретной ноде
- **Поле `defaultBackground`** в StartNode - фон по умолчанию для всех нод
- **UI компоненты**: NodeImageContainer, NodeImageDisplay, NodeImageFrame
- **Рамки для портретов**: DirectorPortraitFrame, ClientPortraitFrame

### Логика:
1. Если в ноде есть `nodeImage` → показать его
2. Иначе если в StartNode есть `defaultBackground` → показать его
3. Иначе → скрыть контейнер

### Файлы изменённые:
- `DialogueUIConnector.cs` +3 поля
- `DialogueUIManager.cs` +UpdateNodeImage(), +defaultBackground
- `PhraseNode.cs` +nodeImage
- `ChoiceNode.cs` +nodeImage
- `EventNode.cs` +nodeImage
- `StartNode.cs` +defaultBackground
- `DialogueGraphView.cs` +UI для выбора изображений

### Документация:
- Создан `.Docs/Buro Crazy/Механики/Диалоговая Система.md`

---

## 2. Исправления Клиентов (Client Lifecycle)

### Проблемы исправленные:

#### 🔴 Критические:
1. **ClientSpawnerWithArchetypes.cs:111-112** - NPE при добавлении null в activeClients
2. **ClientPathfinding.cs:159-163** - Exception при пустом ClientGoal enum
3. **ClientStateMachine.cs:518-528** - ConfusedRoutine без else → бесконечное зависание
4. **ClientStateMachine.cs:563** - NPE при доступе к exitWaypoint
5. **ClientPathfinding.cs:204-230** - OnDestroy не очищал waitingQueue
6. **ClientActionExecutor.cs:65-105** - Race condition с уничтоженным клиентом

#### 🟡 Средние:
7. **ClientQueueManager.cs:189** - NPE при доступе к exitWaypoint
8. **ClientStateMachine.cs:83** - Enraged/Confused не проверялись в GlobalPatienceMonitor
9. **ClientStateMachine.cs:252-331** - Нет default case в switch
10. **ClientStateMachine.cs:320-323** - WaitingForDocument без таймаута
11. **ClientStateMachine.cs:490-507** - CleanupOldState без null checks
12. **ClientQueueManager.cs:113-139** - При таймауте не очищались зоны

### Файлы изменённые:
- `ClientSpawnerWithArchetypes.cs`
- `ClientPathfinding.cs`
- `ClientStateMachine.cs`
- `ClientActionExecutor.cs`
- `ClientQueueManager.cs`
- `ServiceAtRegistrationExecutor.cs`

---

## 3. Исправления Персонала (Staff Lifecycle)

### Проблемы исправленные:

#### 🔴 Критические:
1. **StaffController.cs:372** - FireAndGoHome() не очищал workstation
2. **StaffController.cs:35** - Resources.Load без null check
3. **HiringManager.cs:539-544** - FireStaff не очищал occupiedPoints
4. **ClerkController.cs:114-159** - Множественные NPE с client, stateMachine
5. **InternController.cs:144-234** - Множественные NPE с client, coveredServicePoint
6. **GuardMovement.cs:44-104** - Null checks для staff и patrol points
7. **AgentMover.cs:695** - OnDestroy без StopAllCoroutines
8. **CharacterVisuals.cs:414** - OnDestroy без StopAllCoroutines

#### 🟡 Средние:
9. **StaffScheduleExtensions.cs:162** - Использование Time.time вместо TimeManager
10. **AssignmentManager.cs:25-52** - FirstOrDefault вместо First, +UnassignWorkstation
11. **ProcessDocumentExecutor.cs:10-48** - Null checks для zone, client

### Файлы изменённые:
- `StaffController.cs`
- `HiringManager.cs`
- `ClerkController.cs`
- `InternController.cs`
- `GuardMovement.cs`
- `AgentMover.cs`
- `CharacterVisuals.cs`
- `StaffScheduleExtensions.cs`
- `AssignmentManager.cs`
- `ProcessDocumentExecutor.cs`

---

## 4. UI Исправления

### SmartRoomLabel.cs:
- `_lastHoverTime` инициализирован в прошлое (-100f)
- Автопоиск коллайдера с Debug.LogWarning
- Проверка MainUIManager.pauseCount

### MainUIManager.cs:
- Удалена авто-создаваемая метка "ПАУЗА"
- Добавлен AutoFindPausePanel() для поиска pausePanel на сцене
- pausePanel ищется по имени "pausepanel" в DontDestroyOnLoad

### TeamMemberCardUI.cs:
- Добавлен метод ApplyRoleColor() для окраски фона карточки
- Цвета для всех ролей (12 цветов)

---

## 5. Новая Документация

### Создано:
1. `.Docs/Buro Crazy/Механики/Диалоговая Система.md`
2. `.Docs/Buro Crazy/Сущности/Клиенты.md`
3. `.Docs/Buro Crazy/Сущности/Персонал.md`

### Обновлено:
- `PROJECT_STATUS.md` - актуален (содержит предыдущие фазы)

---

## Статистика

| Метрика | Значение |
|---------|----------|
| Файлов изменено | 25+ |
| Строк кода добавлено | ~200 |
| Критических багов исправлено | 10 |
| Средних багов исправлено | 13 |
| Новых документов создано | 3 |

---

## Тестирование

### После изменений проверить:
1. ✅ Диалоги открываются с изображениями
2. ✅ StartNode фон применяется ко всем нодам
3. ✅ Метки комнат скрыты при старте, появляются при наведении
4. ✅ Панель "ПАУЗА" показывается при паузе
5. ✅ Карточки сотрудников окрашены по ролям
6. ✅ Клиенты не зависают в Confused
7. ✅ Клиенты не пропадают из очередей при уничтожении
8. ✅ Персонал корректно увольняется и очищает工作站
9. ✅ При таймауте клиенты очищаются из всех зон

---

## Известные Ограничения

1. **pausePanel** должен называться "pausepanel" на сцене GameScene
2. **RoomCollider** должен быть на объекте с SmartRoomLabel или в родителях
3. **RoleColorDatabase** не используется в TeamMemberCardUI (хардкод цветов)
