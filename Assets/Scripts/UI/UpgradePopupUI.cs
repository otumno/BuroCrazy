// Файл: Assets/Scripts/UI/UpgradePopupUI.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class UpgradePopupUI : MonoBehaviour
{
    // ... (все поля [SerializeField] остаются без изменений) ...
    [SerializeField] private Image detailIconImage;
    [SerializeField] private TextMeshProUGUI detailNameText;
    [SerializeField] private TextMeshProUGUI detailDescriptionText;
    [SerializeField] private TextMeshProUGUI detailCostText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button cancelButton;

    private UpgradeData currentUpgrade;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuyClicked);
        }
        if (cancelButton != null)
        {
            // <<< ИЗМЕНЕНИЕ: Убедимся, что кнопка "Отмена" тоже вызывает ClosePopup >>>
            cancelButton.onClick.RemoveAllListeners(); // На всякий случай
            cancelButton.onClick.AddListener(ClosePopup);
        }

        // Убедимся, что попап изначально скрыт (через SetActive)
        gameObject.SetActive(false); // <<< ИЗМЕНЕНИЕ: Используем SetActive(false) вместо ClosePopup()
    }

    /// <summary>
    /// Показывает попап с деталями указанного апгрейда.
    /// </summary>
    public void ShowDetails(UpgradeData upgradeData)
    {
        currentUpgrade = upgradeData;
        if (currentUpgrade == null)
        {
            Debug.LogError("[UpgradePopupUI] Попытка показать детали для null апгрейда!");
            ClosePopup();
            return;
        }

        // --- Показываем попап ---
        // <<< ИЗМЕНЕНИЕ: Сначала активируем GameObject >>>
        gameObject.SetActive(true);
        SetPopupVisibility(true); // Управляем CanvasGroup
        // -------------------------

        // --- Заполняем UI элементы ---
        if (detailNameText != null)
        {
            detailNameText.text = currentUpgrade.upgradeName;
        }
        // ... (остальная часть метода ShowDetails без изменений) ...
        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = currentUpgrade.description;
        }
        if (detailCostText != null)
        {
            detailCostText.text = $"Стоимость: ${currentUpgrade.cost}";
        }
        if (detailIconImage != null)
        {
            detailIconImage.sprite = currentUpgrade.iconColor ?? currentUpgrade.iconGrayscale;
            detailIconImage.enabled = detailIconImage.sprite != null;
        }

        if (buyButton != null)
        {
            bool canAfford = PlayerWallet.Instance != null && PlayerWallet.Instance.GetCurrentMoney() >= currentUpgrade.cost;
            bool isAvailable = UpgradeManager.Instance != null && UpgradeManager.Instance.GetUpgradeStatus(currentUpgrade) == UpgradeStatus.Available;

            buyButton.interactable = isAvailable && canAfford;
        }
    }

    /// <summary>
    /// Вызывается при нажатии кнопки "Купить".
    /// </summary>
    private void OnBuyClicked()
    {
        if (currentUpgrade != null && UpgradeManager.Instance != null)
        {
            bool success = UpgradeManager.Instance.PurchaseUpgrade(currentUpgrade);
            if (success)
            {
                ClosePopup();
            }
            else
            {
                ShowDetails(currentUpgrade);
            }
        }
    }

    /// <summary>
    /// Закрывает попап.
    /// </summary>
    public void ClosePopup()
    {
        // <<< ИЗМЕНЕНИЕ: Выключаем GameObject >>>
        gameObject.SetActive(false); 
        // ---------------------------------
        // SetPopupVisibility(false); // Управление CanvasGroup теперь менее важно, но оставим на всякий случай
        currentUpgrade = null;
    }

    /// <summary>
    /// Управляет видимостью и интерактивностью попапа через CanvasGroup.
    /// </summary>
    private void SetPopupVisibility(bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}