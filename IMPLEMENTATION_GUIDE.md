# Инструкция по настройке новых систем BuroCrazy

## 1. Enums (Assets/Scripts/Enums/)

### DocumentSubtype.cs
Добавлено в enum `DocumentType` поле `subtype` для типизации документов (Certificate, Contract, Report, Statement, Approval, Request, Complaint, Warrant, Protocol).

**Настройка:**
- Откройте `DocumentType.cs`
- Убедитесь что добавлено поле `public DocumentSubtype subtype = DocumentSubtype.None;`

### EquipmentType.cs
Новый enum для типов оборудования (Copier, Stapler, SpecialSeal, BindingMachine, Calculator, Computer, ArchiveShelf, Safe, Phone, WaterCooler).

---

## 2. Document Data System (Assets/Scripts/Data/Documents/)

### DocumentData.cs
ScriptableObject для настройки документов.

**Создание ассетов:**
1. Правый клик в Project → Bureau/Documents/Document Data
2. Настройте поля:
   - `documentType` - базовый тип
   - `subtype` - подтип
   - `requiredEquipment` - оборудование для обработки
   - `minSkillLevel` - минимальный уровень навыка
   - `baseProcessingTime` - время обработки
   - `baseFee` - стоимость

**Примеры документов:**
```
- Справка о доходах (Form1 → Certificate, оборудование: Copier)
- Договор аренды (Form2 → Contract, оборудование: Stapler)
- Жалоба (Request → Complaint, оборудование: None)
```

---

## 3. Equipment System (Assets/Scripts/Gameplay/)

### OfficeEquipment.cs
Компонент для объектов оборудования.

**Установка на объекты:**
1. Добавьте `OfficeEquipment` на объект (копир, сейф, кулер)
2. Настройте:
   - `equipmentType` - тип оборудования
   - `durability` - прочность (0-100)
   - `speedMultiplier` - множитель скорости
   - `operationalSprite` / `brokenSprite` - спрайты

### EquipmentManager.cs
Менеджер для отслеживания оборудования.

**Настройка:**
1. Создайте Empty GameObject "EquipmentManager"
2. Добавьте компонент `EquipmentManager`
3. Оборудование автоматически регистрируется через `OfficeEquipment.Start()`

---

## 4. Client Archetypes (Assets/Scripts/Characters/)

### ClientArchetype.cs
ScriptableObject для типов клиентов.

**Создание архетипов:**
1. Правый клик → Bureau/Characters/Client Archetype
2. Настройте:
   - `archetypeID` - уникальный ID (elderly, worker, homeless, student, elite)
   - `patience` - терпение в секундах
   - `stressResistance` - сопротивляемость стрессу (0-1)
   - `aggressionTendency` - склонность к агрессии (0-1)
   - `allowedGoals` - доступные цели
   - `thoughtPool` - пул мыслей
   - `allowedClothingColors` - разрешенные цвета одежды

**Примеры архетипов:**
```
- Бабушка: терпение 45с, высокая агрессия, добрые мысли
- Бизнесмен: терпение 20с, низкая агрессия, деловые мысли
- Бездомный: терпение 60с, средняя агрессия, грустные мысли
```

### ArchetypeDatabase.cs
База данных архетипов.

**Настройка:**
1. Создайте ArchetypeDatabase
2. Добавьте все архетипы в `allArchetypes`
3. Настройте `spawnWeights` для вероятностей спавна

---

## 5. Visual Diversity (Assets/Scripts/Data/Visuals/)

### HairStyleData.cs
ScriptableObject для причесок.

**Создание:**
1. Bureau/Visuals/Hair Style
2. Настройте спрайты и цвета

### OutfitData.cs
ScriptableObject для одежды.

**Создание:**
1. Bureau/Visuals/Outfit
2. Настройте тип (Casual, Formal, Workwear, Rags, Uniform, Suit)

### HairStyleDatabase.cs / OutfitDatabase.cs
Базы данных для быстрого доступа.

---

## 6. Director Creation Book (Assets/Scripts/UI/Creation/)

### DirectorCreationBookUI.cs
UI книги создания директора.

**Настройка:**
1. Создайте Canvas с книгой
2. Добавьте `DirectorCreationBookUI`
3. Настройте ссылки на UI элементы

### BookPageData.cs
Страницы книги.

**Создание:**
1. Bureau/Creation/Book Page
2. Добавьте текст и выборы с эффектами

---

## 7. Academy Training (Assets/Scripts/Managers/Academy/)

### AcademyScenarioManager.cs
Менеджер обучения.

**Настройка:**
1. Создайте Empty GameObject "AcademyManager"
2. Добавьте `AcademyScenarioManager`
3. Настройте сценарии в листе `allScenarios`

### AcademyUI.cs
UI панели обучения.

---

## 8. Job Instructions (Assets/Scripts/Data/Policies/)

### JobInstruction.cs
ScriptableObject для инструкций.

**Создание:**
1. Bureau/Policies/Job Instruction
2. Настройте категорию и эффекты

**Примеры инструкций:**
```
- "Здороваться с клиентами" (категория Greeting)
- "Приоритет пожилым" (категория Priority)
- "Не помогать бездомным" (категория SpecialClients)
- "Скрывать часть денег" (категория MoneyHandling)
```

### InstructionManager.cs
Менеджер инструкций.

**Настройка:**
1. Создайте Empty GameObject "InstructionManager"
2. Добавьте `InstructionManager`
3. Создайте `JobInstructionDatabase` и назначьте

---

## 9. Bureaucracy Routes (Assets/Scripts/Data/Bureaucracy/)

### BureaucracyRoute.cs
Маршруты документов.

**Создание:**
1. Bureau/Bureaucracy/Route
2. Настройте этапы:
   - Регистрация (требуется регистратор)
   - Подпись директора (требуется подпись)
   - Оплата (требуется кассир)
   - Архивирование (требуется архивариус)

---

## 10. Notifications (Assets/Scripts/Managers/NotificationManager/)

### NotificationManager.cs
Система нотификаций.

**Использование:**
```csharp
NotificationManager.Instance.ShowNotification(
    targetGameObject,
    NotificationType.Success,
    "Документ принят!"
);
```

---

## 11. Staff Schedule & Punctuality (Assets/Scripts/Characters/Controllers/)

### StaffController.cs (обновлен)
Добавлены поля:
- `punctuality` - педантичность (0-1)
- `maxLateness` - максимальное опоздание
- `hasLunchBreak` - есть ли обед

**Настройка для Intern:**
- `hasLunchBreak = false`
- `breakDuration = 0`

**Настройка для остальных:**
- `hasLunchBreak = true`
- `breakStartTime = shiftDuration / 2` (ровно середина смены)

### StaffPunctualityConfig.cs
Конфигурация расписаний.

**Создание:**
1. Bureau/Config/Punctuality Config
2. Настройте глобальные параметры

---

## 12. UI Panels

### PolicyBookUI.cs (обновлен)
Добавлены вкладки:
- Политики
- Инструкции

**Настройка:**
1. Добавьте кнопки `policiesTabButton` и `instructionsTabButton`
2. Добавьте панели `policiesPanel` и `instructionsPanel`

### InstructionItemUI.cs
Элемент списка инструкций с toggle.

### PolicyDeskButton.cs
Кнопка на столе директора.

### WorldNoticeBoard.cs
Доска объявлений в мире.

---

## 13. Client Spawner with Archetypes

### ClientSpawnerWithArchetypes.cs
Обновленный спавнер с архетипами.

**Настройка:**
1. Замените старый ClientSpawner на этот
2. Назначьте `archetypeDatabase`
3. Настройте `maxClients` и `spawnInterval`

---

## 14. Тесты (Assets/Scripts/Editor/Tests/)

### DocumentDataTests.cs
Тесты системы документов.

### ArchetypeDatabaseTests.cs
Тесты базы архетипов.

### ClientArchetypeTests.cs
Тесты архетипов.

### StaffScheduleTests.cs
Тесты расписания и опозданий.

**Запуск:**
- Window → General → Test Runner
- Run All

---

## Порядок настройки

1. **Создайте базы данных:**
   - ArchetypeDatabase
   - HairStyleDatabase
   - OutfitDatabase
   - JobInstructionDatabase

2. **Создайте данные:**
   - 5-6 ClientArchetype
   - 5-10 HairStyleData
   - 10-15 OutfitData
   - 5-10 JobInstruction

3. **Настройте менеджеры:**
   - EquipmentManager
   - InstructionManager
   - NotificationManager

4. **Настройте UI:**
   - PolicyBookUI с вкладками
   - WorldNoticeBoard
   - PolicyDeskButton

5. **Обновите существующие:**
   - StaffController (расписание)
   - CharacterVisuals (разнообразие)

6. **Тестируйте:**
   - Запустите тесты
   - Проверьте в Editor

---

## Частые ошибки

1. **NullReference в EquipmentManager**
   - Проверьте что OfficeEquipment на сцене

2. **Клиенты без архетипа**
   - Проверьте что ArchetypeDatabase назначен

3. **Инструкции не работают**
   - Проверьте что InstructionManager существует

4. **Опоздания не считаются**
   - Проверьте что punctuality > 0
   - Проверьте что hasArrivedToday сбрасывается в StartShift()

---

## Готовые префабы

Создайте следующие префабы:

1. **NotificationPrefab** - Canvas с NotificationUI
2. **FloatingTextPrefab** - TextMeshPro с FloatingText
3. **InstructionItemPrefab** - UI с InstructionItemUI
4. **PolicyBookCanvas** - полная панель книги политик

---

## Интеграция с существующим кодом

### Обновление CharacterVisuals
```csharp
// Добавьте в Start()
var archetype = GetComponent<ClientArchetype>();
if (archetype != null)
{
    SetupFromArchetype(archetype);
}
```

### Обновление ClientSpawner
```csharp
// Замените на ClientSpawnerWithArchetypes
public ClientSpawnerWithArchetypes archetypeSpawner;
```

### Обновление StaffController
```csharp
// В StartShift()
CalculateArrivalTime();
UpdateBreakLogic();
```
