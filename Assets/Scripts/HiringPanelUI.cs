// Файл: HiringPanelUI.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HiringPanelUI : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject teamMemberCardPrefab;
    public Transform teamListContent; // Перетащите сюда объект "Content" из ScrollView
    
    private List<TeamMemberCardUI> activeCards = new List<TeamMemberCardUI>();

    // Этот метод будет вызываться, когда панель становится видимой
    void OnEnable()
    {
        RefreshTeamList();
    }

    public void RefreshTeamList()
    {
        // Очищаем старые карточки
        foreach (Transform child in teamListContent)
        {
            Destroy(child.gameObject);
        }
        activeCards.Clear();

        // Находим всех сотрудников на сцене
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        var rankDb = ExperienceManager.Instance.rankDatabase; // Получаем базу данных рангов

        // Создаем новые карточки для каждого
        foreach (var staffMember in allStaff)
        {
            GameObject cardGO = Instantiate(teamMemberCardPrefab, teamListContent);
            TeamMemberCardUI cardUI = cardGO.GetComponent<TeamMemberCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(staffMember, rankDb);
                activeCards.Add(cardUI);
            }
        }
    }
    
    // Периодически обновляем информацию на карточках
    void Update()
    {
        if (gameObject.activeInHierarchy)
        {
            foreach (var card in activeCards)
            {
                card.UpdateCard();
            }
        }
    }
}