using System;
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
    private Action<DirectorOrder> _onClick;

    // Метод для настройки карточки данными из приказа
    public void Setup(DirectorOrder order, Action<DirectorOrder> onClick)
    {
        currentOrder = order;
        _onClick = onClick;

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

    // если в менюшку с выбором карточки, как-то попал приказ, который не может быть выбран, то надо кидать ошибку
    private void OnCardSelected()
    {
        // Карточка сообщает главному менеджеру, что ее выбрали
        Debug.Log($"Выбран приказ: {currentOrder.orderName}");
        _onClick?.Invoke(currentOrder);
    }

    private void OnDestroy()
    {
        _onClick = null;
    }
}