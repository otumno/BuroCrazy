using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HiringPanelUI : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject teamMemberCardPrefab;
    public Transform teamListContent;
    
    private List<TeamMemberCardUI> activeCards = new List<TeamMemberCardUI>();

    void OnEnable()
    {
        RefreshTeamList();
    }

    public void RefreshTeamList()
    {
        foreach (Transform child in teamListContent)
        {
            Destroy(child.gameObject);
        }
        activeCards.Clear();

        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        
        foreach (var staffMember in allStaff)
        {
            GameObject cardGO = Instantiate(teamMemberCardPrefab, teamListContent);
            TeamMemberCardUI cardUI = cardGO.GetComponent<TeamMemberCardUI>();
            if (cardUI != null)
            {
                // <<< ИЗМЕНЕНИЕ: Вызываем упрощенный Setup >>>
                cardUI.Setup(staffMember);
                activeCards.Add(cardUI);
            }
        }
    }
    
    void Update()
    {
        if (gameObject.activeInHierarchy)
        {
            foreach (var card in activeCards)
            {
                if (card != null) card.UpdateCard();
            }
        }
    }
}