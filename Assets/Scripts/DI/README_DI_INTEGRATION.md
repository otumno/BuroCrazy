# Zenject DI Integration — Инструкция по настройке (v2)

## ПРОБЛЕМА И РЕШЕНИЕ

**Проблема:** MonoBehaviour-инсталлер нельзя добавить в поле Installers префаба ProjectContext.

**Решение:** Используем ScriptableObject-инсталлер вместо MonoBehaviour.

---

## Созданные файлы

### 1. `Assets/Scripts/DI/ProjectContextInstallerAsset.cs`
ScriptableObject-инсталлер (НЕ MonoBehaviour). Привязывает все 40 синглтонов через `FromMethod`.
Этот файл создаёт asset через `Assets > Create > BuroCrazy > DI > Project Context Installer`.

### 2. `Assets/Scripts/DI/ProjectContextInstaller.cs`
MonoBehaviour-версия инсталлера (для совместимости, может использоваться напрямую).

### 3. `Assets/Scripts/DI/DIBindingValidator.cs`
Валидатор биндингов. При запуске выводит статус всех DI-привязок.

### 4. `Assets/Scripts/DI/Examples/ExampleDIUsage.cs`
Пример использования `[Inject]` атрибута.

### 5. `Assets/Scripts/DI/Editor/ZenjectSceneValidator.cs`
Editor-меню для настройки сцен и создания asset'ов.

---

## ПОШАГОВАЯ ИНСТРУКЦИЯ НАСТРОЙКИ В UNITY

### Шаг 1: Установка Zenject
1. Открой `Window > Package Manager`
2. Нажми `+`, выбери `Add package by name...`
3. Введи `com.svermeulen.extenject` и нажми "Add"
4. Дождись завершения импорта

### Шаг 2: Создание префаба ProjectContext
1. В папке `Assets/Resources` создай пустой GameObject
2. Назови его `ProjectContext`
3. Добавь на него компонент `Zenject.ProjectContext`
4. Перетащи GameObject в папку `Assets/Resources`, чтобы создать префаб
5. **Удали оригинальный GameObject со сцены** — префаб должен существовать только как asset

### Шаг 3: Создание ScriptableObject инсталлера
**ВАЖНО:** Нужен ScriptableObject, а не MonoBehaviour!

**Способ 1 (через меню):**
1. В Project Explorer: Right-click > Create > BuroCrazy > DI > Project Context Installer
2. Или: `BuroCrazy > DI > Create Installer Asset`

**Способ 2 (автоматически):**
1. Открой `BuroCrazy > DI > ZenjectSceneValidator`
2. Нажми кнопку "Create Installer Asset"

Asset появится по пути: `Assets/Scripts/DI/ProjectContextInstallerAsset.asset`

### Шаг 4: Привязка инсталлера к ProjectContext
1. Выбери префаб `Assets/Resources/ProjectContext.prefab`
2. В Inspector найди компонент `Project Context (Script)`
3. Найди поле `Installers` (список)
4. Нажми `+` в этом списке
5. Перетащи созданный ScriptableObject asset в это поле

### Шаг 5: Настройка SceneContext на каждой сцене
**КРИТИЧНО ДЛЯ РАБОТЫ `[Inject]` НА СЦЕНЕ!**

1. Открой нужную сцену (GameScene, MainMenuScene и т.д.)
2. Создай пустой GameObject
3. Назови его `SceneContext`
4. Добавь компонент `Zenject.SceneContext`
5. (Опционально) Добавь `DI > DIBindingValidator` для отладки
6. Повтори для всех сцен в проекте

**Автоматически:**
1. `BuroCrazy > DI > Add SceneContext to Current Scene`

### Шаг 6: Проверка работоспособности
1. Запусти игру на любой сцене
2. В консоли должно появиться:
   ```
   [ProjectContextInstallerAsset] Все синглтоны привязаны к DI-контейнеру.
   [DIBindingValidator] Проверка завершена: 35 OK, 0 ошибок.
   ```
3. **Не должно быть** красных ошибок `ZenjectException`

---

## ИСПОЛЬЗОВАНИЕ [Inject] В НОВОМ КОДЕ

```csharp
using UnityEngine;
using Zenject;
using Managers;

public class MyNewScript : MonoBehaviour
{
    // Внедряем зависимости через DI
    [Inject] private AudioManager _audioManager;
    [Inject] private ClientQueueManager _queueManager;

    private void Start()
    {
        // Старый способ ВСЁ ЕЩЁ РАБОТАЕТ:
        // AudioManager.Instance.PlaySound(...);
        
        // Новый способ (более чистый):
        _audioManager.PlaySound(Scriptables.Audio.SoundID.UI_Click_Default);
    }
}
```

---

## ПРИВЯЗАННЫЕ СИНГЛТОНЫ (40 шт.)

| Менеджер | Статус |
|----------|--------|
| AudioManager | ✅ |
| ClientQueueManager | ✅ |
| SaveLoadManager | ✅ |
| CalendarManager | ✅ |
| TimeManager | ✅ |
| WaveManager | ✅ |
| HiringManager | ✅ |
| ArchiveManager | ✅ |
| PlayerWallet | ✅ |
| DirectorManager | ✅ |
| ProgressionManager | ✅ |
| UpgradeManager | ✅ |
| PolicyManager | ✅ |
| DocumentManager | ✅ |
| EquipmentManager | ✅ |
| DurabilityManager | ✅ |
| ExperienceManager | ✅ |
| FinancialLedgerManager | ✅ |
| OrderManager | ✅ |
| PhoneManager | ✅ |
| StoryStateManager | ✅ |
| TransitionManager | ✅ |
| MainUIManager | ✅ |
| MusicPlayer | ✅ |
| LightingManager | ✅ |
| AchievementManager | ✅ |
| TeletypeManager | ✅ |
| DialogueUIManager | ✅ |
| AssignmentManager | ✅ |
| GuardManager | ✅ |
| ArchiveRequestManager | ✅ |
| ScenePointsRegistry | ✅ |
| PayStaffManager | ✅ |
| UIGlobalSettingsManager | ✅ |
| ClientSpawner | ✅ |
| BureaucracyManager | ✅ |
| AcademyScenarioManager | ✅ |
| TutorialBureaucracyQuest | ✅ |
| DocumentQualityManager | ✅ |
| GameLifecycleManager | ✅ |
| NotificationManager | ✅ |

---

## TROUBLESHOOTING

### Ошибка: "ZenjectException: Unable to resolve 'AudioManager'"
**Причина:** ProjectContext не загружается или инсталлер не привязан.
**Решение:** 
1. Проверь что префаб ProjectContext лежит в `Assets/Resources/`
2. Проверь что ScriptableObject инсталлер добавлен в поле Installers префаба

### Ошибка: "ZenjectException: Could not find dependency 'X', needed by 'Y'"
**Причина:** Менеджер X не привязан к контейнеру.
**Решение:** Добавь `BindSingleton<X>()` в `ProjectContextInstallerAsset.InstallBindings()`.

### Логи показывают "NO BINDING but Instance exists"
**Это НОРМАЛЬНО** для обратной совместимости! Старые менеджеры работают через `Instance`, а новый DI работает параллельно.

### Предупреждение: "No validators found"
**Причина:** DIBindingValidator не добавлен на GameObject со SceneContext.
**Решение:** Игнорируй — это необязательный инструмент отладки.

---

## ВАЖНЫЕ ЗАМЕЧЕНИЯ

1. **Обратная совместимость:** Все старые вызовы `SomeManager.Instance.DoSomething()` продолжают работать.
2. **Постепенный переход:** Можно использовать `[Inject]` в новых скриптах, не удаляя старый код.
3. **SceneContext обязателен:** Без него Zenject не сможет внедрять зависимости в объекты на сцене.
4. **ProjectContext как префаб:** Должен лежать в Resources и НЕ быть на сцене.
5. **ScriptableObject инсталлер:** Должен быть добавлен в поле Installers префаба ProjectContext.
