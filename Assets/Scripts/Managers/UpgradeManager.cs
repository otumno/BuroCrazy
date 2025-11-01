// Файл: Assets/Scripts/Managers/UpgradeManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Нужен для FirstOrDefault и других Linq-методов

public class UpgradeManager : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static UpgradeManager Instance { get; private set; }
    // -------------------------

    [Header("База данных апгрейдов")]
    [Tooltip("Перетащите сюда ВСЕ ассеты UpgradeData, которые существуют в игре.")]
    public List<UpgradeData> allUpgradesDatabase; // Список всех возможных апгрейдов

    // --- Состояние купленных апгрейдов ---
    // Используем HashSet для быстрого поиска купленных апгрейдов по имени
    private HashSet<string> purchasedUpgradeNames = new HashSet<string>();
    // -------------------------------------

    // --- Событие для оповещения UI об изменениях ---
    public event System.Action OnUpgradePurchased;
    // ---------------------------------------------

    void Awake()
    {
        // Стандартная реализация Singleton
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Раскомментируй, если менеджер должен быть "бессмертным"
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Инициализация (пока пустая, но может понадобиться для загрузки)
        InitializeUpgrades();
    }

    // Инициализация статусов (например, при загрузке сохранения)
    private void InitializeUpgrades()
    {
        // Пока просто убедимся, что база данных назначена
        if (allUpgradesDatabase == null || allUpgradesDatabase.Count == 0)
        {
            Debug.LogError("[UpgradeManager] База данных апгрейдов (All Upgrades Database) не назначена или пуста!", this);
        }
        else
        {
             // Убираем null элементы из базы на всякий случай
             allUpgradesDatabase.RemoveAll(item => item == null);
             Debug.Log($"[UpgradeManager] Инициализирован. Загружено {allUpgradesDatabase.Count} апгрейдов из базы.");
        }
    }

    /// <summary>
    /// Проверяет, куплен ли апгрейд по его ScriptableObject.
    /// </summary>
    public bool IsUpgradePurchased(UpgradeData upgrade)
    {
        if (upgrade == null) return false;
        // Проверяем наличие имени апгрейда в нашем списке купленных
        return purchasedUpgradeNames.Contains(upgrade.name); // Используем upgrade.name как уникальный ключ
    }

    /// <summary>
    /// Проверяет, доступны ли все требования для покупки апгрейда.
    /// </summary>
    public bool AreRequirementsMet(UpgradeData upgrade)
    {
        if (upgrade == null || upgrade.requirements == null) return true; // Нет требований = выполнено

        // Проверяем, что каждый апгрейд из списка требований КУПЛЕН
        foreach (var req in upgrade.requirements)
        {
            if (req == null || !IsUpgradePurchased(req)) // Если требование null или не куплено
            {
                return false; // Требования не выполнены
            }
        }
        return true; // Все требования выполнены
    }

    /// <summary>
    /// Пытается купить апгрейд.
    /// </summary>
    /// <returns>True - если покупка успешна, False - если нет.</returns>
    public bool PurchaseUpgrade(UpgradeData upgrade)
    {
        // 1. Проверки
        if (upgrade == null)
        {
            Debug.LogError("[UpgradeManager] Попытка купить null апгрейд!");
            return false;
        }
        if (IsUpgradePurchased(upgrade))
        {
            Debug.LogWarning($"[UpgradeManager] Апгрейд '{upgrade.upgradeName}' уже куплен.");
            return false; // Уже куплен
        }
        if (!AreRequirementsMet(upgrade))
        {
            Debug.LogWarning($"[UpgradeManager] Не выполнены требования для апгрейда '{upgrade.upgradeName}'.");
            return false; // Требования не выполнены
        }
        if (PlayerWallet.Instance == null)
        {
            Debug.LogError("[UpgradeManager] PlayerWallet не найден! Невозможно списать деньги.");
            return false;
        }
        if (PlayerWallet.Instance.GetCurrentMoney() < upgrade.cost)
        {
            Debug.LogWarning($"[UpgradeManager] Недостаточно средств для покупки '{upgrade.upgradeName}'. Нужно: ${upgrade.cost}, Есть: ${PlayerWallet.Instance.GetCurrentMoney()}");
            // Можно добавить сообщение игроку
            return false; // Не хватает денег
        }

        // 2. Списание денег
        PlayerWallet.Instance.AddMoney(-upgrade.cost, $"Покупка апгрейда: {upgrade.upgradeName}");

        // 3. Добавление в список купленных
        purchasedUpgradeNames.Add(upgrade.name); // Используем имя ассета как ID
        Debug.Log($"<color=green>[UpgradeManager] Апгрейд '{upgrade.upgradeName}' успешно куплен!</color>");

        // 4. Применение эффекта активации/деактивации объектов
        ApplyUpgradeEffects(upgrade);

        // 5. Оповещение UI и других систем
        OnUpgradePurchased?.Invoke(); // Вызываем событие, чтобы UI мог обновиться

        // 6. Обновление графа навигации, если нужно
        if ((upgrade.objectsToActivate != null && upgrade.objectsToActivate.Count > 0) || 
            (upgrade.objectsToDeactivate != null && upgrade.objectsToDeactivate.Count > 0)) // <-- Добавляем проверку деактивации
        {
            var graphBuilder = FindFirstObjectByType<GraphBuilder>();
            if (graphBuilder != null)
            {
                Debug.Log($"[UpgradeManager] Перестроение графа навигации после покупки '{upgrade.upgradeName}'...");
                graphBuilder.BuildGraph();
            } else {
                 Debug.LogWarning($"[UpgradeManager] GraphBuilder не найден на сцене. Граф не будет перестроен после покупки '{upgrade.upgradeName}'.");
            }
        }


        return true; // Покупка успешна
    }

    /// <summary>
    /// Применяет все эффекты апгрейда (активация и деактивация объектов).
    /// </summary>
    private void ApplyUpgradeEffects(UpgradeData upgrade)
    {
        int activatedCount = 0;
        int deactivatedCount = 0;

        // --- 1. ЛОГИКА ДЕАКТИВАЦИИ (Убрать хлам, заменить стулья) ---
        if (upgrade.objectsToDeactivate != null)
        {
            foreach (string objectName in upgrade.objectsToDeactivate)
            {
                if (string.IsNullOrEmpty(objectName)) continue;

                // Для деактивации используем GameObject.Find,
                // так как мы ищем УЖЕ АКТИВНЫЙ объект.
                GameObject targetObject = GameObject.Find(objectName); 
                
                if (targetObject != null)
                {
                    if (targetObject.activeSelf)
                    {
                        targetObject.SetActive(false);
                        deactivatedCount++;
                        Debug.Log($"[UpgradeManager] Деактивирован объект: {objectName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[UpgradeManager] (Деактивация) Не найден АКТИВНЫЙ GameObject с именем '{objectName}' для апгрейда '{upgrade.upgradeName}'.");
                }
            }
        }

        // --- 2. ЛОГИКА АКТИВАЦИИ (Поставить диваны, мусорки) ---
        if (upgrade.objectsToActivate != null)
        {
            foreach (string objectName in upgrade.objectsToActivate)
            {
                if (string.IsNullOrEmpty(objectName)) continue;

                // Используем нашу специальную функцию для поиска НЕАКТИВНЫХ объектов
                GameObject targetObject = FindInactiveGameObjectByName(objectName); 

                if (targetObject != null)
                {
                    if (!targetObject.activeSelf)
                    {
                        targetObject.SetActive(true);
                        activatedCount++;
                        Debug.Log($"[UpgradeManager] Активирован объект: {objectName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[UpgradeManager] (Активация) Не найден GameObject с именем '{objectName}' для апгрейда '{upgrade.upgradeName}'. Проверьте точное имя объекта на сцене.");
                }
            }
        }
         
        if (activatedCount > 0) Debug.Log($"[UpgradeManager] Активировано {activatedCount} объектов для апгрейда '{upgrade.upgradeName}'.");
        if (deactivatedCount > 0) Debug.Log($"[UpgradeManager] Деактивировано {deactivatedCount} объектов для апгрейда '{upgrade.upgradeName}'.");
    }


    // --- Методы для сохранения/загрузки ---

    /// <summary>
    /// Возвращает список имен купленных апгрейдов для сохранения.
    /// </summary>
    public List<string> GetPurchasedUpgradeNamesForSave()
    {
        return new List<string>(purchasedUpgradeNames);
    }

    /// <summary>
    /// Загружает статусы апгрейдов из списка имен.
    /// </summary>
    public void LoadPurchasedUpgrades(List<string> loadedNames)
    {
        purchasedUpgradeNames.Clear(); // Очищаем текущие
        if (loadedNames == null) return;

        int appliedCount = 0;
        foreach (string name in loadedNames)
        {
            // Находим апгрейд по имени в нашей базе данных
            UpgradeData upgrade = allUpgradesDatabase.FirstOrDefault(u => u != null && u.name == name);
            if (upgrade != null)
            {
                purchasedUpgradeNames.Add(name); // Добавляем в список купленных
                ApplyUpgradeEffects(upgrade); // Применяем эффект активации
                appliedCount++;
            }
            else
            {
                Debug.LogWarning($"[UpgradeManager] При загрузке не найден апгрейд с именем '{name}' в базе данных.");
            }
        }

        Debug.Log($"[UpgradeManager] Загружено и применено {appliedCount} купленных апгрейдов.");

        // Может потребоваться перестроить граф после загрузки и активации всех объектов
        var graphBuilder = FindFirstObjectByType<GraphBuilder>();
        graphBuilder?.BuildGraph();

        // Оповещаем UI, что данные загружены
        OnUpgradePurchased?.Invoke();
    }

     /// <summary>
    /// Сбрасывает все купленные апгрейды (для новой игры).
    /// </summary>
    public void ResetUpgrades()
    {
        // Деактивируем ВСЕ объекты, связанные с апгрейдами (нужно знать их имена)
        // Этот шаг сложнее, т.к. нужно иметь полный список имен апгрейд-объектов.
        // Проще пересоздать сцену или иметь механизм отката.
        // Пока просто очистим список купленных.
        purchasedUpgradeNames.Clear();
        Debug.Log("[UpgradeManager] Статус всех апгрейдов сброшен.");
        OnUpgradePurchased?.Invoke(); // Оповестить UI
    }

    // --- Конец методов сохранения/загрузки ---


     // --- Метод для UI ---
     /// <summary>
    /// Возвращает статус апгрейда (Locked, Available, Purchased).
    /// </summary>
    public UpgradeStatus GetUpgradeStatus(UpgradeData upgrade)
    {
        if (upgrade == null) return UpgradeStatus.Locked; // Или другое состояние ошибки
        if (IsUpgradePurchased(upgrade)) return UpgradeStatus.Purchased;
        if (AreRequirementsMet(upgrade)) return UpgradeStatus.Available;
        return UpgradeStatus.Locked;
    }


    // --- Вспомогательные методы поиска неактивных объектов ---

    /// <summary>
    /// Рекурсивно ищет GameObject по имени, включая неактивные.
    /// </summary>
    private GameObject FindInactiveGameObjectByName(string name)
    {
        // Получаем все корневые объекты сцены
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject rootObject in rootObjects)
        {
            // Ищем в самом корневом объекте
            if (rootObject.name == name)
            {
                return rootObject;
            }

            // Ищем в дочерних объектах рекурсивно
            Transform childFound = FindRecursive(rootObject.transform, name);
            if (childFound != null)
            {
                return childFound.gameObject;
            }
        }
        return null; // Не найден
    }

    /// <summary>
    /// Рекурсивная часть поиска.
    /// Важно: использует GetChild(i), чтобы найти неактивные дочерние объекты.
    /// </summary>
    private Transform FindRecursive(Transform parent, string name)
    {
        // Проверяем всех дочерних, включая неактивных
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            
            if (child.name == name)
            {
                return child; // Найден!
            }
            
            // Идем глубже
            Transform found = FindRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }
        return null; // Не найден в этой ветке
    }

} // Конец класса UpgradeManager


// Enum для статусов апгрейда в UI
public enum UpgradeStatus
{
    Locked,     // Требования не выполнены
    Available,  // Можно купить (если хватает денег)
    Purchased   // Уже куплен
}