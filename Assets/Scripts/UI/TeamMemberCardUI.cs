using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using Managers;
using Utilities;
using Enums;

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
        assignedStaff = staff;
        
        fireButton.onClick.RemoveAllListeners();
        fireButton.onClick.AddListener(OnFireButtonClicked);

        promoteButton.onClick.RemoveAllListeners();
        promoteButton.onClick.AddListener(OnPromoteButtonClicked);

        changeRoleButton.onClick.RemoveAllListeners();
        changeRoleButton.onClick.AddListener(OnChangeRoleButtonClicked);

        UpdateCard();
    }

    public void UpdateCard()
    {
        if (assignedStaff == null) 
        {
            gameObject.SetActive(false);
            return;
        }

        nameText.text = assignedStaff.characterName;
        roleText.text = GetRoleNameInRussian(assignedStaff.currentRole);
        stressText.text = $"Стресс: {assignedStaff.frustration.ToString("0")}";
        salaryText.text = $"З/П: ${assignedStaff.salaryPerPeriod.ToString()} / период";
        genderText.text = assignedStaff.gender == Gender.Male ? "Пол: М" : "Пол: Ж";
        
        if (assignedStaff.skills == null)
        {
            skillsText.text = "Нет особых навыков";
        }
        else
        {
            var stringBuilder = new StringBuilder();
            var skillTypes = Enum.GetValues(typeof(SkillType)).Cast<SkillType>();
            foreach (var skillType in skillTypes)
            {
                var line = assignedStaff.skills.GetSkillShortText(skillType);
                stringBuilder.Append(line + "\n");
            }
            skillsText.text = stringBuilder.ToString();
        }

        if (assignedStaff.currentRank != null)
        {
            if (rankText != null) rankText.text = assignedStaff.currentRank.rankName;

            RankData nextRankData = assignedStaff.currentRank.possiblePromotions.FirstOrDefault();
            if (nextRankData != null)
            {
                int xpForCurrentRank = assignedStaff.currentRank.experienceRequired;
                int xpForNextRank = nextRankData.experienceRequired;
                int totalXpForLevel = xpForNextRank - xpForCurrentRank;
                int currentXpInLevel = (int)assignedStaff.experiencePoints - xpForCurrentRank;
                xpBarFill.fillAmount = totalXpForLevel > 0 ? (float)currentXpInLevel / totalXpForLevel : 1f;
                xpText.text = $"XP: {currentXpInLevel} / {totalXpForLevel}";
            }
            else
            {
                xpBarFill.fillAmount = 1f;
                xpText.text = "МАКС. РАНГ";
            }
        }
        else
        {
            rankText.text = "Без ранга";
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
        
        ApplyRoleColor(assignedStaff.currentRole);
        background.sprite = GetBackgroundForRole(assignedStaff.currentRole);
    }
    
    private void ApplyRoleColor(StaffController.Role role)
    {
        if (background == null) return;
        
        Color roleColor = GetRoleColor(role);
        background.color = roleColor;
    }
    
    private Color GetRoleColor(StaffController.Role role)
    {
        switch (role)
        {
            case StaffController.Role.Intern: return new Color(0.6f, 0.8f, 0.4f, 1f);
            case StaffController.Role.Clerk: return new Color(0.4f, 0.6f, 0.8f, 1f);
            case StaffController.Role.Registrar: return new Color(0.7f, 0.5f, 0.8f, 1f);
            case StaffController.Role.Cashier: return new Color(0.9f, 0.7f, 0.2f, 1f);
            case StaffController.Role.Archivist: return new Color(0.6f, 0.5f, 0.4f, 1f);
            case StaffController.Role.Guard: return new Color(0.3f, 0.3f, 0.5f, 1f);
            case StaffController.Role.Janitor: return new Color(0.5f, 0.5f, 0.5f, 1f);
            case StaffController.Role.OfficeManager: return new Color(0.9f, 0.4f, 0.4f, 1f);
            case StaffController.Role.Accountant: return new Color(0.2f, 0.6f, 0.3f, 1f);
            case StaffController.Role.ServiceWorker: return new Color(0.8f, 0.5f, 0.3f, 1f);
            default: return Color.white;
        }
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
        var actionConfigPopupUi = FindFirstObjectByType<ActionConfigPopupUI>(FindObjectsInactive.Include);
        if (actionConfigPopupUi == null)
        {
            Debug.LogError($"На сцене отсутствует {nameof(ActionConfigPopupUI)}");
            return;
        }
        
        actionConfigPopupUi.OpenForStaff(assignedStaff);
    }

    private static string GetRoleNameInRussian(StaffController.Role role) =>
        role switch
        {
            StaffController.Role.Intern => "Стажёр",
            StaffController.Role.Clerk => "Клерк",
            StaffController.Role.Registrar => "Регистратор",
            StaffController.Role.Cashier => "Кассир",
            StaffController.Role.Archivist => "Архивариус",
            StaffController.Role.Guard => "Охранник",
            StaffController.Role.Janitor => "Уборщик",
            _ => "Не назначено"
        };
}