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

    [Header("Портрет (Фото)")]
    [SerializeField] private Image portraitBody;
    [SerializeField] private Image portraitOutfit;
    [SerializeField] private Image portraitHair;
    [SerializeField] private Image portraitFace;
    [SerializeField] private Image portraitAccessory;
    [SerializeField] private Material grayscaleMaterial;

    [Header("Посещаемость")]
    [SerializeField] private TextMeshProUGUI attendanceText;

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
        
        // Отображение особенности (трейта)
        if (assignedStaff.permanentTrait != StaffController.TraitType.None)
        {
            var traitInfo = StaffController.TraitLibrary[assignedStaff.permanentTrait];
            skillsText.text += $"\n<color=yellow><b>Особенность:</b> {traitInfo.Name}</color>";
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
        
        UpdatePortrait(assignedStaff);
        UpdateAttendance(assignedStaff);
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

    private void UpdateAttendance(StaffController staff)
    {
        if (attendanceText != null)
        {
            attendanceText.text = $"<color=#FFAA00>Опозданий:</color> {staff.totalLatenessCount}\n" +
                                  $"<color=#FF5555>Больничных:</color> {staff.sickDaysCount}";
        }
    }

    private void UpdatePortrait(StaffController staff)
    {
        if (portraitBody == null || staff.visuals == null || staff.roleData == null) return;

        // 1. Copy body, outfit, and hair directly from the staff's actual current visual state in the world
        var bodyRen = staff.visuals.GetBodyRenderer();
        var outfitRen = staff.visuals.GetOutfitRenderer();
        var hairRen = staff.visuals.GetHairRenderer();
        var accRen = staff.visuals.GetAccessoryRenderer();

        SetPortraitLayer(portraitBody, bodyRen);
        SetPortraitLayer(portraitOutfit, outfitRen);
        SetPortraitLayer(portraitHair, hairRen);
        SetPortraitLayer(portraitAccessory, accRen);

        // 2. Calculate Dominant Emotion based on skills
        Emotion dominantEmotion = Emotion.Neutral;
        float maxSkill = -1f;
        var s = staff.skills;
        
        if (s.softSkills > maxSkill) { maxSkill = s.softSkills; dominantEmotion = Emotion.Happy; }
        if (s.corruption > maxSkill) { maxSkill = s.corruption; dominantEmotion = Emotion.Sly; }
        if (s.paperworkMastery > maxSkill) { maxSkill = s.paperworkMastery; dominantEmotion = Emotion.Working; }
        if (s.pedantry > maxSkill) { maxSkill = s.pedantry; dominantEmotion = Emotion.Thinking; }
        if (s.sedentaryResilience > maxSkill) { maxSkill = s.sedentaryResilience; dominantEmotion = Emotion.Relaxed; }

        // 3. Set Face using the collection
        if (portraitFace != null && staff.visuals.currentSpriteCollection != null)
        {
            portraitFace.sprite = staff.visuals.currentSpriteCollection.GetFaceSprite(dominantEmotion, staff.gender);
            portraitFace.color = Color.white;
            portraitFace.enabled = portraitFace.sprite != null;
            if (grayscaleMaterial != null) portraitFace.material = grayscaleMaterial;
        }
    }

    private void SetPortraitLayer(Image uiImage, SpriteRenderer sourceRenderer)
    {
        if (uiImage == null) return;
        
        if (sourceRenderer != null && sourceRenderer.sprite != null)
        {
            uiImage.sprite = sourceRenderer.sprite;
            uiImage.color = sourceRenderer.color; // Keep the dynamic colors!
            uiImage.enabled = true;
            if (grayscaleMaterial != null) uiImage.material = grayscaleMaterial;
        }
        else
        {
            uiImage.enabled = false;
        }
    }
}