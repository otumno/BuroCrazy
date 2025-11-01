// Файл: Assets/Scripts/UI/UpgradePanelUI.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Нужен для OrderBy и др.
using System.Collections; // Нужен для корутин (IEnumerator)

public class UpgradePanelUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private Transform upgradesGridContainer; // Контейнер GridLayoutGroup
    [SerializeField] private GameObject upgradeIconPrefab;    // Префаб иконки

    [Header("Панель Деталей (Попап)")]
    [SerializeField] private UpgradePopupUI upgradePopup; // Ссылка на скрипт попапа

    // Список для хранения ссылок на созданные UI иконки
    private List<UpgradeIconUI> currentIcons = new List<UpgradeIconUI>();

    // Подписываемся на событие менеджера при включении
    private void OnEnable()
    {
        // Проверяем наличие UpgradeManager перед подпиской
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased += RefreshPanel; // Подписываемся на событие покупки
            
            // Запускаем корутину вместо прямого вызова, чтобы избежать "двойного клика"
            StartCoroutine(DelayedRefreshPanel());
        }
        else
        {
             Debug.LogError("[UpgradePanelUI] UpgradeManager не найден! Панель не будет работать.", this);
             // Можно скрыть панель или показать сообщение об ошибке
             gameObject.SetActive(false);
        }
    }

    // Отписываемся при выключении, чтобы избежать утечек памяти
    private void OnDisable()
    {
        // Останавливаем корутину, если панель выключили до того,
        // как она успела отработать
        StopAllCoroutines();

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgradePurchased -= RefreshPanel;
        }
         // Закрываем попап, если он был открыт
         if (upgradePopup != null && upgradePopup.gameObject.activeSelf) {
             upgradePopup.ClosePopup();
         }
    }

    // Используем System.Collections.IEnumerator, чтобы избежать конфликта имен
    private System.Collections.IEnumerator DelayedRefreshPanel()
    {
        // Ждем до конца текущего кадра
        // Это даст Event System время "успокоиться" после клика,
        // который, возможно, и активировал эту панель.
        yield return new WaitForEndOfFrame(); 
        
        // Теперь, в следующем кадре, безопасно перестраиваем UI
        RefreshPanel();
    }


    /// <summary>
    /// Полностью перерисовывает сетку апгрейдов.
    /// </summary>
    public void RefreshPanel()
    {
        // Проверяем наличие необходимых компонентов
        if (upgradesGridContainer == null || upgradeIconPrefab == null || UpgradeManager.Instance == null)
        {
            Debug.LogError("[UpgradePanelUI] Не настроены ссылки на Grid Container, Icon Prefab или UpgradeManager!", this);
            return;
        }

        // 1. Очищаем старые иконки
        foreach (Transform child in upgradesGridContainer)
        {
            Destroy(child.gameObject);
        }
        currentIcons.Clear();

        // 2. Получаем все апгрейды из базы данных менеджера
        List<UpgradeData> allUpgrades = UpgradeManager.Instance.allUpgradesDatabase;
        if (allUpgrades == null) return; // Выходим, если база пуста

        // 3. Сортируем апгрейды (сначала доступные, потом купленные, потом заблокированные)
        allUpgrades = allUpgrades
                        .OrderBy(u => UpgradeManager.Instance.GetUpgradeStatus(u)) // Сортируем по статусу (Available -> Purchased -> Locked)
                        .ThenBy(u => u.cost) // Внутри статуса сортируем по цене
                        .ToList();

        // 4. Создаем и настраиваем иконки для каждого апгрейда
        foreach (UpgradeData upgrade in allUpgrades)
        {
            if (upgrade == null) continue; // Пропускаем null элементы в базе

            GameObject iconGO = Instantiate(upgradeIconPrefab, upgradesGridContainer);
            UpgradeIconUI iconUI = iconGO.GetComponent<UpgradeIconUI>();
            if (iconUI != null)
            {
                iconUI.Setup(upgrade, this); // Передаем данные и ссылку на эту панель
                currentIcons.Add(iconUI);    // Добавляем в список для будущих обновлений
            }
            else
            {
                 Debug.LogError($"[UpgradePanelUI] Префаб иконки '{upgradeIconPrefab.name}' не содержит скрипт UpgradeIconUI!", upgradeIconPrefab);
                 Destroy(iconGO); // Удаляем некорректный объект
            }
        }
         Debug.Log($"[UpgradePanelUI] Панель обновлена. Отображено {currentIcons.Count} иконок апгрейдов.");
    }

    /// <summary>
    /// Показывает детальную информацию об апгрейде в попапе. Вызывается из UpgradeIconUI.
    /// </summary>
    public void ShowUpgradeDetails(UpgradeData upgradeData)
    {
        if (upgradePopup != null)
        {
            upgradePopup.ShowDetails(upgradeData);
        }
        else
        {
            Debug.LogError("[UpgradePanelUI] Ссылка на UpgradePopupUI не настроена!", this);
        }
    }

    // Этот Update() нужен, чтобы кнопки "Купить" становились активными,
    // как только у игрока хватает денег, без необходимости переоткрывать панель.
    void Update()
    {
        // Обновляем состояние каждой иконки (проверка денег, статуса)
        foreach(var icon in currentIcons)
        {
            if (icon != null) // Доп. проверка на случай уничтожения объекта
            {
                icon.UpdateVisualState();
            }
        }
    }
}