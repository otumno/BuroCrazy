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

    // --- УДАЛЕНО: Метод OnEnable() больше не нужен для обновления ---

    // --- НОВЫЙ МЕТОД: для показа панели ---
    /// <summary>
    /// Включает панель и принудительно обновляет список сотрудников.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        RefreshTeamList();
    }

    // --- НОВЫЙ МЕТОД: для скрытия панели ---
    /// <summary>
    /// Выключает панель.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void RefreshTeamList()
    {
        foreach (Transform child in teamListContent)
        {
            Destroy(child.gameObject);
        }
        activeCards.Clear();

        if (HiringManager.Instance == null) return;
        
        var allStaff = HiringManager.Instance.AllStaff;
        Debug.Log($"[HiringPanelUI] Обновление списка. Найдено сотрудников в менеджере: {allStaff.Count}");

        foreach (var staffMember in allStaff)
        {
            if (staffMember == null) continue;

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
                if (card != null)
                {
                    card.UpdateCard();
                }
            }
        }
    }
}