using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PromotionOptionButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankNameText;
    [SerializeField] private TextMeshProUGUI costText;
    
    private RankData rankData;
    private PromotionPanelUI panelController;

    public void Setup(RankData data, PromotionPanelUI controller)
    {
        this.rankData = data;
        this.panelController = controller;

        rankNameText.text = data.rankName;
        costText.text = $"${data.promotionCost}";

        // Проверяем, хватает ли денег, и делаем кнопку неактивной, если не хватает
        bool canAfford = PlayerWallet.Instance.GetCurrentMoney() >= data.promotionCost;
        GetComponent<Button>().interactable = canAfford;
        
        // Добавляем слушатель на клик
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        // Сообщаем главной панели, какой вариант мы выбрали
        panelController.OnOptionSelected(rankData);
    }
}