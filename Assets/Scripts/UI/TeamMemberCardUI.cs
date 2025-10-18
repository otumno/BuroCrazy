using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using System.Collections.Generic;

public class TeamMemberCardUI : MonoBehaviour
{
    [Header("Ссылки на UI элементы")]
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI stressText;
    [SerializeField] private TextMeshProUGUI salaryText;
    [SerializeField] private TextMeshProUGUI genderText;
    [SerializeField] private TextMeshProUGUI skillsText;

    [Header("Кнопки")]
    [SerializeField] private Button fireButton;
    [SerializeField] private Button promoteButton;
    [SerializeField] private Button changeRoleButton;

    [Header("XP Bar")]
    [SerializeField] private Image xpBarFill;
    [SerializeField] private TextMeshProUGUI xpText;

    [Header("Спрайты фонов для ролей")]
    public Sprite internBackground;
    public Sprite clerkBackground;
    public Sprite guardBackground;
    public Sprite janitorBackground;
    
    private StaffController assignedStaff;

    public void Setup(StaffController staff)
    {
        this.assignedStaff = staff;
        
        if (fireButton != null)
        {
            fireButton.onClick.RemoveAllListeners();
            fireButton.onClick.AddListener(OnFireButtonClicked);
        }
        if (promoteButton != null)
        {
            promoteButton.onClick.RemoveAllListeners();
            promoteButton.onClick.AddListener(OnPromoteButtonClicked);
        }
        if (changeRoleButton != null)
        {
            changeRoleButton.onClick.RemoveAllListeners();
            changeRoleButton.onClick.AddListener(OnChangeRoleButtonClicked);
        }

        UpdateCard();
    }

    public void UpdateCard()
    {
        if (assignedStaff == null) 
        {
            gameObject.SetActive(false);
            return;
        }

        if (nameText != null) nameText.text = assignedStaff.characterName;
        if (roleText != null) roleText.text = GetRoleNameInRussian(assignedStaff.currentRole);
        if (stressText != null) stressText.text = $"Стресс: {assignedStaff.frustration:P0}";
        if (salaryText != null) salaryText.text = $"З/П: ${assignedStaff.salaryPerPeriod} / период";
        if (genderText != null) genderText.text = assignedStaff.gender == Gender.Male ? "Пол: М" : "Пол: Ж";
        if (skillsText != null && assignedStaff.skills != null) { /* ... */ }

        if (assignedStaff.currentRank != null)
        {
            if (rankText != null) rankText.text = assignedStaff.currentRank.rankName;

            RankData nextRankData = assignedStaff.currentRank.possiblePromotions.FirstOrDefault();
            if (nextRankData != null)
            {
                int xpForCurrentRank = assignedStaff.currentRank.experienceRequired;
                int xpForNextRank = nextRankData.experienceRequired;
                int totalXpForLevel = xpForNextRank - xpForCurrentRank;
                int currentXpInLevel = assignedStaff.experiencePoints - xpForCurrentRank;
                if (xpBarFill != null) xpBarFill.fillAmount = totalXpForLevel > 0 ? (float)currentXpInLevel / totalXpForLevel : 1f;
                if (xpText != null) xpText.text = $"XP: {currentXpInLevel} / {totalXpForLevel}";
            }
            else
            {
                if (xpBarFill != null) xpBarFill.fillAmount = 1f;
                if (xpText != null) xpText.text = "МАКС. РАНГ";
            }
        }
        else
        {
            if (rankText != null) rankText.text = "Без ранга";
        }
        
        if (promoteButton != null)
        {
            bool canBePromoted = false;
            if (assignedStaff.currentRank != null && assignedStaff.currentRank.possiblePromotions.Any())
            {
                canBePromoted = assignedStaff.currentRank.possiblePromotions.Any(rank => assignedStaff.experiencePoints >= rank.experienceRequired);
            }
            promoteButton.gameObject.SetActive(canBePromoted);
        }
        
        if (background != null) background.sprite = GetBackgroundForRole(assignedStaff.currentRole);
    }
    
    private Sprite GetBackgroundForRole(StaffController.Role role)
    {
        switch (role)
        {
            case StaffController.Role.Intern:
                return internBackground;
            case StaffController.Role.Clerk:
            case StaffController.Role.Registrar:
            case StaffController.Role.Cashier:
            case StaffController.Role.Archivist:
                return clerkBackground;
            case StaffController.Role.Guard:
                return guardBackground;
            case StaffController.Role.Janitor:
                return janitorBackground;
            default:
                return null;
        }
    }
    
    private void OnFireButtonClicked()
    {
        if (assignedStaff != null && HiringManager.Instance != null)
        {
            HiringManager.Instance.FireStaff(assignedStaff);
            GetComponentInParent<HiringPanelUI>()?.RefreshTeamList();
        }
    }

    private void OnPromoteButtonClicked()
{
    if (assignedStaff != null)
    {
        // THE FIX: Instead of promoting directly, we open the selection panel.
        PromotionPanelUI.Instance.ShowForStaff(assignedStaff);
    }
}

    private void OnChangeRoleButtonClicked()
    {
        FindFirstObjectByType<ActionConfigPopupUI>(FindObjectsInactive.Include)?.OpenForStaff(assignedStaff);
    }
	
	private string GetRoleNameInRussian(StaffController.Role role)
{
    switch (role)
    {
        case StaffController.Role.Intern: return "Стажёр";
        case StaffController.Role.Clerk: return "Клерк";
        case StaffController.Role.Registrar: return "Регистратор";
        case StaffController.Role.Cashier: return "Кассир";
        case StaffController.Role.Archivist: return "Архивариус";
        case StaffController.Role.Guard: return "Охранник";
        case StaffController.Role.Janitor: return "Уборщик";
        default: return "Не назначено";
    }
	
}
}