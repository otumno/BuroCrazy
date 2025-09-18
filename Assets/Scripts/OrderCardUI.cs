using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OrderCardUI : MonoBehaviour
{
    [Header("Ссылки на UI элементы")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Image iconImage; // Необязательно, если нет иконок

    private DirectorOrder currentOrder;
    private OrderSelectionUI selectionManager;

    // Метод для настройки карточки данными из приказа
    public void Setup(DirectorOrder order, OrderSelectionUI manager)
    {
        currentOrder = order;
        selectionManager = manager;

        titleText.text = order.orderName;
        descriptionText.text = order.description;

        if (iconImage != null && order.icon != null)
        {
            iconImage.sprite = order.icon;
            iconImage.gameObject.SetActive(true);
        }
        else if (iconImage != null)
        {
            iconImage.gameObject.SetActive(false);
        }
        
        // Убедимся, что на кнопке нет старых действий и добавляем новое
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnCardSelected);
    }

    // Что происходит, когда игрок нажимает на кнопку
    private void OnCardSelected()
    {
        // Карточка сообщает главному менеджеру, что ее выбрали
        Debug.Log($"Выбран приказ: {currentOrder.orderName}");
        selectionManager.OnOrderSelected(currentOrder);
    }
}