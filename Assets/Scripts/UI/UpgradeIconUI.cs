// Файл: Assets/Scripts/UI/UpgradeIconUI.cs

using Managers;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeIconUI : MonoBehaviour
{
    [Header("Ссылки на компоненты префаба")]
    [SerializeField] private Image backgroundImage; // Основной фон/рамка
    [SerializeField] private Image iconImage;       // Иконка самого апгрейда
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private GameObject purchasedCheckmark;
    [SerializeField] private Button button;          // Сама кнопка

    private UpgradeData upgradeData;
    private UpgradePanelUI panelController; // Ссылка на главную панель

    /// <summary>
    /// Настраивает иконку данными апгрейда.
    /// </summary>
    public void Setup(UpgradeData data, UpgradePanelUI controller)
    {
        upgradeData = data;
        panelController = controller;

        if (upgradeData == null || panelController == null)
        {
            Debug.LogError("Ошибка Setup в UpgradeIconUI: UpgradeData или PanelController равен null!", gameObject);
            gameObject.SetActive(false);
            return;
        }

        // Устанавливаем иконку (пока без серого/цветного)
        if (iconImage != null)
        {
            // Используем цветную иконку, если есть, иначе серую
            iconImage.sprite = upgradeData.iconColor ?? upgradeData.iconGrayscale;
            iconImage.enabled = iconImage.sprite != null; // Выключаем Image, если спрайта нет
        }

        // Устанавливаем цену
        if (costText != null)
        {
            costText.text = $"${upgradeData.cost}";
        }

        // Назначаем действие на кнопку
        if (button != null)
        {
            button.onClick.RemoveAllListeners(); // Очищаем старые
            button.onClick.AddListener(OnClick);
        }

        // Обновляем визуальное состояние (куплен/доступен/заблокирован)
        UpdateVisualState();
    }

    /// <summary>
    /// Обновляет внешний вид иконки в зависимости от статуса апгрейда.
    /// </summary>
    public void UpdateVisualState()
    {
        if (upgradeData == null || UpgradeManager.Instance == null) return;

        UpgradeStatus status = UpgradeManager.Instance.GetUpgradeStatus(upgradeData);
        bool canAfford = PlayerWallet.Instance != null && PlayerWallet.Instance.GetCurrentMoney() >= upgradeData.cost;
        
        // --- НОВАЯ ПРОВЕРКА ---
        bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(upgradeData.name);
        // ----------------------

        // Иконка
        if (iconImage != null)
        {
            if (status == UpgradeStatus.Purchased)
                iconImage.sprite = upgradeData.iconColor ?? upgradeData.iconGrayscale;
            else
                iconImage.sprite = upgradeData.iconGrayscale ?? upgradeData.iconColor;
            
            iconImage.enabled = iconImage.sprite != null;
        }

        // Галочка
        if (purchasedCheckmark != null) purchasedCheckmark.SetActive(status == UpgradeStatus.Purchased);

        // Кнопка и Цена
        if (button != null)
        {
            if (isPending)
            {
                // Если в процессе - блокируем
                button.interactable = false;
                if (costText != null) costText.text = "В пути..."; // Или иконка часов
            }
            else
            {
                button.interactable = (status == UpgradeStatus.Available && canAfford);
                if (costText != null) costText.text = status == UpgradeStatus.Purchased ? "" : $"${upgradeData.cost}";
            }
        }
        
        if (costText != null) costText.gameObject.SetActive(status != UpgradeStatus.Purchased);

        // Фон
        if (backgroundImage != null)
        {
            // Затемняем если недоступно, нет денег ИЛИ уже в процессе
            bool isDark = (status == UpgradeStatus.Locked || (status == UpgradeStatus.Available && !canAfford) || isPending);
            backgroundImage.color = isDark ? new Color(0.5f, 0.5f, 0.5f, 0.7f) : Color.white;
        }
    }


    // Вызывается при клике на иконку
    private void OnClick()
    {
        // <<< ДОБАВЛЕН ЛОГ ДЛЯ ДИАГНОСТИКИ >>>
        Debug.Log($"<color=cyan>[UpgradeIconUI]</color> Клик! Показываем детали для: {upgradeData?.upgradeName}");

        // Сообщаем главной панели, что нужно показать детали для нашего апгрейда
        if (panelController != null && upgradeData != null)
        {
            panelController.ShowUpgradeDetails(upgradeData);
        }
        else
        {
             Debug.LogError($"[UpgradeIconUI] Невозможно открыть детальный вид: panelController или upgradeData == null!", gameObject);
        }
    }
}