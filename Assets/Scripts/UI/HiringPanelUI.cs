using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Managers;

public class HiringPanelUI : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Префаб карточки для отображения одного сотрудника")]
    public GameObject teamMemberCardPrefab;
    [Tooltip("Контейнер, куда будут добавляться карточки")]
    public Transform teamListContent;
    [Tooltip("Префаб 'папки', который будет отображаться в конце списка")]
    public GameObject folderBottomPrefab;
    
    // --- ИСПРАВЛЕНИЕ: Ссылка на новую панель расписания (если нужна) ---
    [Header("Ссылки")]
    [SerializeField] private StaffSchedulePanelUI schedulePanel; 
    // ------------------------------------------------------------------

    private enum SortMode { ByName, ByRole, ByRank }
    private SortMode currentSortMode = SortMode.ByName;
    private bool isSortAscending = true; 
    
    private List<TeamMemberCardUI> activeCards = new List<TeamMemberCardUI>();

    public void Show()
    {
        var animator = GetComponent<UIWindowAnimator>();
        if (animator != null) animator.Open();
        else gameObject.SetActive(true);

        MainUIManager.Instance?.PushPause();

        // Переносим логику из OnEnable в конец Show
        currentSortMode = SortMode.ByName;
        isSortAscending = true;
        RefreshTeamList();
    }

    public void Hide()
    {
        var animator = GetComponent<UIWindowAnimator>();
        if (animator != null)
        {
            animator.Close();
        }
        else
        {
            gameObject.SetActive(false);
            MainUIManager.Instance?.PopPause();
        }
    }

    // Метод для открытия расписания (можно привязать к кнопке в инспекторе)
    public void OpenSchedule()
    {
        if (schedulePanel != null)
        {
            schedulePanel.Show();
        }
    }

    public void OnSortButtonClicked(int mode)
    {
        SortMode newMode = (SortMode)mode;

        if (newMode == currentSortMode)
        {
            isSortAscending = !isSortAscending;
        }
        else
        {
            currentSortMode = newMode;
            isSortAscending = true;
        }
        RefreshTeamList();
    }

    public void RefreshTeamList()
    {
        foreach (Transform child in teamListContent) Destroy(child.gameObject);
        activeCards.Clear();

        if (HiringManager.Instance == null) return;
        
        var allStaff = HiringManager.Instance.AllStaff;

        switch (currentSortMode)
        {
            case SortMode.ByName:
                allStaff = isSortAscending ? allStaff.OrderBy(s => s.characterName).ToList() : allStaff.OrderByDescending(s => s.characterName).ToList();
                break;
            case SortMode.ByRole:
                allStaff = isSortAscending ? allStaff.OrderBy(s => s.currentRole.ToString()).ToList() : allStaff.OrderByDescending(s => s.currentRole.ToString()).ToList();
                break;
            case SortMode.ByRank:
                allStaff = isSortAscending 
                    ? allStaff.OrderBy(s => s.currentRank != null ? s.currentRank.rankLevel : -1).ToList() 
                    : allStaff.OrderByDescending(s => s.currentRank != null ? s.currentRank.rankLevel : -1).ToList();
                break;
        }

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
        
        if (folderBottomPrefab != null)
        {
            bool alreadyHasFolder = false;
            foreach (Transform child in teamListContent) {
                if (child.gameObject.name.StartsWith(folderBottomPrefab.name)) { 
                    alreadyHasFolder = true;
                    break;
                }
            }
            if (!alreadyHasFolder) {
                Instantiate(folderBottomPrefab, teamListContent);
            }
        }
    }
    
    private float _updateTimer = 0f;
    void Update()
    {
        if (gameObject.activeInHierarchy)
        {
            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer >= 1f)
            {
                _updateTimer = 0f;
                foreach (var card in activeCards)
                {
                    if (card != null) card.UpdateCard();
                }
            }
        }
    }
}