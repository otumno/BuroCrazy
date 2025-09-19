// Файл: HiringPanelUI.cs

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

        // --- ИЗМЕНЕНИЕ: Берем список напрямую из менеджера ---
        if (HiringManager.Instance == null) return;
        
        var allStaff = HiringManager.Instance.AllStaff;

        foreach (var staffMember in allStaff)
        {
            GameObject cardGO = Instantiate(teamMemberCardPrefab, teamListContent);
            TeamMemberCardUI cardUI = cardGO.GetComponent<TeamMemberCardUI>();
            if (cardUI != null)
            {
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