# Cinematic Graph System — Руководство по сборке и настройке

## Содержание
1. [Обзор системы](#1-обзор-системы)
2. [Структура файлов](#2-структура-файлов)
3. [Узлы графа](#3-узлы-графа)
4. [Режимы выполнения](#4-режимы-выполнения)
5. [Триггеры и запуск графов](#5-триггеры-и-запуск-графов)
6. [Передача параметров](#6-передача-параметров)
7. [JSON импорт/экспорт](#7-json-импортэкспорт)
8. [Пример: Первый день без VIP](#8-пример-первый-день-без-vip)

---

## 1. Обзор системы

Система Cinematic Graph позволяет создавать cut-scenes (кинематики) визуальным редактированием графа узлов. Граф состоит из узлов (nodes), соединённых связями (edges). Каждый узел выполняет определённое действие (перемещение персонажа, показ диалога, изменение флагов и т.д.).

**Основные возможности:**
- Визуальное редактирование графов в Unity Editor
- Фоновый режим выполнения (не блокирует игрока)
- Передача параметров в граф (clientGender, флаги и т.д.)
- Случайные задержки и взвешенные случайные выборы
- Централизованное управление триггерами

---

## 2. Структура файлов

```
Assets/Scripts/Cinematic/
├── CinematicGraph.cs           — ScriptableObject граф
├── CinematicPlayer.cs          — Исполнитель графа
├── CinematicTrigger.cs         — legacy-компонент триггера (для обратной совместимости)
├── CinematicTriggerManager.cs  — Единый менеджер триггеров (НОВЫЙ)
├── CharacterRegistry.cs        — Реестр персонажей на сцене
├── SceneObjectRegistry.cs      — Реестр объектов сцены
│
├── Nodes/
│   ├── CinematicNode.cs        — Базовый класс узла
│   ├── NextNode.cs             — Базовый класс узла с одним выходом
│   ├── StartNode.cs            — Начальный узел
│   ├── EndNode.cs              — Конечный узел
│   ├── MoveToNode.cs          — Перемещение персонажа
│   ├── CameraNode.cs          — Управление камерой
│   ├── EventNode.cs           — Игровые события (деньги, влияние, HP)
│   ├── ConditionNode.cs       — Условный узел
│   ├── SpawnCharacterNode.cs  — Спавн персонажа
│   ├── CallDialogueNode.cs    — Вызов диалога
│   ├── RandomNode.cs          — Случайный выбор (NEW)
│   ├── CallCinematicGraphNode.cs — Вызов подграфа (NEW)
│   └── WaitForCharacterDespawnNode.cs — Ожидание деспавна (NEW)
│
└── Editor/
    ├── CinematicEditorWindow.cs   — Главное окно редактора
    ├── CinematicGraphView.cs      — GraphView для редактора
    ├── CinematicNodeView.cs       — Визуальное представление узла
    └── CinematicJSONImporter.cs   — Импорт/экспорт JSON
```

---

## 3. Узлы графа

### Базовые узлы

| Узел | Описание | Поля |
|------|----------|------|
| **Start** | Начальная точка графа | `characterID` — ID персонажа по умолчанию |
| **End** | Конечная точка | — |
| **Move To** | Перемещение персонажа | `targetKey` — ключ точки назначения |
| **Camera** | Управление камерой | `cameraMode`, `targetPosition`, `duration` |

### Диалоги и речь

| Узел | Описание | Поля |
|------|----------|------|
| **Say Dialog** | Диалог персонажа (UI) | `speakerName`, `text`, `portrait`, `voiceClip` |
| **Call Dialogue** | Вызов диалоговой системы | `dialogueGraphName`, `targetClient` |
| **Say Bubble** | Всплывающая речь | `text`, `duration` |

### Условия и случайность

| Узел | Описание | Поля |
|------|----------|------|
| **Condition** | Проверка условия | `conditionKey`, `operation`, `value`, `trueNode`, `falseNode` |
| **Random** *(NEW)* | Случайный выбор с весами | `outcomes` — список `weight` + `nextNode` |

### События

| Узел | Описание | Поля |
|------|----------|------|
| **Event** | Игровое событие | `eventType` (enum), `intValue`, `stringValue`, `boolValue` |

**Типы событий EventNode:**
```
AddMoney, AddInfluence, AddStrike           — Изменение ресурсов
SetFlag, LockControl, UnlockControl          — Флаги и управление
SetCursor, ActivateObject, DeactivateObject — UI и объекты
AddDocumentToHand, RemoveDocumentFromHand   — Документы
PlaySound, PlayMusic, StopMusic             — Аудио
RestorePreviousMusic                        — Восстановление музыки
OpenDirectorDesk, CloseDirectorDesk         — UI директора
ShowNotification                           — Уведомления
ApplyTrait, RemoveTrait, SetSpeedMultiplier  — Персонажи
TriggerAllergy                             — Аллергия
AddReputation, SetReputation, RemoveReputation — HP/Репутация (NEW)
```

### Вызов подграфов

| Узел | Описание | Поля |
|------|----------|------|
| **Call Cinematic Graph** *(NEW)* | Вызов другого графа | `targetGraph`, `waitForCompletion`, `executionMode` |
| **Wait For Despawn** *(NEW)* | Ожидание уничтожения персонажа | `characterKey` |

### Спавн

| Узел | Описание | Поля |
|------|----------|------|
| **Spawn Character** | Спавн клиента | `archetypeID`, `spawnPointKey`, `targetKeyForReference`, `forcedGoal` |

---

## 4. Режимы выполнения

```csharp
public enum ExecutionMode
{
    FullControl,  // Блокирует управление, курсор, управляет камерой
    Background    // Не блокирует управление, не меняет курсор
}
```

**FullControl** — обычный кинематик, блокирует игрока до завершения.
**Background** — фоновый кинематик, выполняется параллельно с игрой.

---

## 5. Триггеры и запуск графов

### CinematicTriggerManager (НОВЫЙ)

Единый менеджер для всех триггеров. Располагается на сцене (синглтон).

```csharp
// Структура триггера
[Serializable]
public class CinematicTriggerData
{
    public string id;               // Уникальный ID
    public bool enabled;            // Включён
    public TriggerType triggerType;  // Тип события
    public int requiredDay;         // Необходимый день
    public CalendarDayPeriodType requiredPeriod;
    public string requiredFlagKey;  // Ключ флага
    public int requiredFlagValue;
    public int requiredMoney;
    public int requiredStrikes;
    public CinematicGraph graphToPlay; // Граф для запуска
    public bool once;               // Запустить только раз
    public float delay;
    public bool useRandomDelay;
    public float minDelay, maxDelay;
    public float excludeEndSeconds;
    public CinematicPlayer.ExecutionMode executionMode;
    public bool forceBackground;
}
```

**Типы триггеров (TriggerType):**
- `OnDayStart` — в начале дня
- `OnPeriodStart` — в начале периода
- `OnFlagSet` — при установке флага
- `OnMoneyReached` — при достижении суммы денег
- `OnStrikeCount` — при количестве страйков
- `Manual` — ручной запуск

**Публичные методы:**
```csharp
// Регистрация
manager.AddTrigger("tutorial_day1", TriggerType.OnDayStart, tutorialGraph);

// Управление
manager.EnableTrigger("tutorial_day1", false); // Выключить
manager.ResetTrigger("tutorial_day1");           // Сбросить (чтобы сработал снова)
manager.TriggerNow("tutorial_day1");             // Запустить вручную
manager.ResetAllTriggers();                      // Сбросить все
```

### Старый CinematicTrigger

Старый компонент `CinematicTrigger` оставлен для обратной совместимости, но рекомендуется использовать `CinematicTriggerManager`.

---

## 6. Передача параметров

Параметры передаются через `Dictionary<string, object>` в графе:

```csharp
// В графе есть поле:
public Dictionary<string, object> runtimeParameters;

// Пример: SpawnCharacterNode читает clientGender
if (player.CurrentGraph.runtimeParameters.TryGetValue("clientGender", out object genderObj))
{
    if (genderObj is Gender gender)
        client.SetGender(gender);
}
```

**Пример использования в коде:**
```csharp
// Создаём граф с параметрами
var graph = Resources.Load<CinematicGraph>("Path/To/Graph");
graph.runtimeParameters = new Dictionary<string, object>
{
    { "clientGender", Gender.Female }
};

// Запускаем
var player = FindObjectOfType<CinematicPlayer>();
player.Play(graph, ExecutionMode.Background);
```

---

## 7. JSON импорт/экспорт

### Импорт из JSON

1. Открыть `Tools > Cinematic Graph Editor`
2. Нажать **Import from Clipboard** (читает JSON из буфера обмена)
3. Граф автоматически сохраняется в `Assets/Data/CinematicGraphs/`
4. Узлы автоматически расставляются (AutoLayout)

### Экспорт в JSON

1. Открыть граф в редакторе
2. Нажать **Export to Clipboard**

### Формат JSON

```json
{
  "name": "GraphName",
  "nodes": [
    {
      "id": "node_001",
      "type": "start",
      "chr": "Director",
      "pos": [100, 200]
    },
    {
      "id": "node_002",
      "type": "say_dialog",
      "speaker": "Клиент",
      "text": "Привет!",
      "portrait": "Client_Normal",
      "next": "node_003"
    }
  ],
  "links": [
    { "from": "node_001", "to": "node_002" }
  ]
}
```

---

## 8. Пример: Первый день без VIP

### Структура туториала первого дня

```csharp
// В CinematicTriggerManager добавляем:
trigger.id = "tutorial_day1";
trigger.triggerType = TriggerType.OnDayStart;
trigger.requiredDay = 1;
trigger.graphToPlay = tutorialGraph;
trigger.once = true;
trigger.delay = 2f;
trigger.forceBackground = false; // Блокируем игрока во время туториала
```

### JSON туториала первого дня

```json
{
  "name": "Tutorial_Day1",
  "nodes": [
    {
      "id": "start_001",
      "type": "start",
      "chr": "Director",
      "pos": [50, 50]
    },
    {
      "id": "move_001",
      "type": "move",
      "key": "DirectorDesk",
      "pos": [350, 50]
    },
    {
      "id": "say_001",
      "type": "say_dialog",
      "speaker": "Директор",
      "text": "Добро пожаловать в Bureau! Давайте начнём с работы.",
      "portrait": "Director_Happy",
      "next": "end_001"
    },
    {
      "id": "end_001",
      "type": "end",
      "pos": [650, 50]
    }
  ],
  "links": [
    { "from": "start_001", "to": "move_001" },
    { "from": "move_001", "to": "say_001" }
  ]
}
```

### Подключение туториала

1. Создать ScriptableObject графа `Assets/Data/CinematicGraphs/Tutorial_Day1.asset`
2. В `CinematicTriggerManager` на сцене добавить триггер:
   - `id = "tutorial_day1"`
   - `triggerType = OnDayStart`
   - `requiredDay = 1`
   - `graphToPlay = Tutorial_Day1`
3. При первом запуске игры туториал автоматически запустится

---

## 9. Новые узлы (детальное описание)

### RandomNode

```csharp
[CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Random")]
public class RandomNode : CinematicNode
{
    public List<RandomOutcome> outcomes;
}

[Serializable]
public class RandomOutcome
{
    public float weight = 1f;      // Вес исхода
    public CinematicNode nextNode; // Следующий узел
}
```

**Пример:** 3 исхода с весами 1:2:3
- Шанс первого: 1/(1+2+3) = 16.7%
- Шанс второго: 2/(1+2+3) = 33.3%
- Шанс третьего: 3/(1+2+3) = 50%

### CallCinematicGraphNode

Вызывает другой граф как подпрограмму:
- `waitForCompletion = true` — ждёт завершения, затем продолжает
- `waitForCompletion = false` — запускает в фоне и сразу продолжает
- `executionMode` — FullControl или Background

### WaitForCharacterDespawnNode

Ждёт уничтожения персонажа по ключу в `CharacterRegistry`:
```csharp
public string characterKey; // Ключ для поиска
```

---

## 10. Отладка

### Включить отладочные сообщения

```csharp
// В CinematicTriggerManager:
public bool debugLog = true;

// В Console будет:
// [CinematicTriggerManager] День изменился: 1
// [CinematicTriggerManager] Запуск триггера: tutorial_day1
```

### Проверить состояние триггеров

```csharp
// В Editor:
var states = CinematicTriggerManager.Instance.GetTriggerStates();
foreach (var s in states)
    Debug.Log($"{s.id}: triggered={s.hasTriggered}");
```

### Ручной запуск графа

```csharp
var player = FindObjectOfType<CinematicPlayer>();
player.Play(graph, ExecutionMode.FullControl);
```

---

## 11. Совместимость

- **Unity**: 2022.3+
- **Input System**: Package required
- **Сохранение**: Состояния триггеров сохраняются в `SaveData` через `GetTriggerStates()` / `RestoreTriggerStates()`