using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class OrderCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button selectButton;

    private int cardIndex;
    public event Action<int> OnOrderSelected;

    private void Awake()
    {
        selectButton.onClick.AddListener(HandleClick);
    }

    public void Setup(DirectorOrder order, int index)
    {
        this.cardIndex = index;
        titleText.text = order.orderName;
        descriptionText.text = order.orderDescription;
        if(order.icon != null) iconImage.sprite = order.icon;
    }

    private void HandleClick()
    {
        OnOrderSelected?.Invoke(cardIndex);
    }
}