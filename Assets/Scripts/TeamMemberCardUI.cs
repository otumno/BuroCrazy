// Файл: TeamMemberCardUI.cs - ФИНАЛЬНАЯ ВЕРСИЯ
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class TeamMemberCardUI : MonoBehaviour
{
    [Header("UI Элементы")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI salaryText;
    public Image xpFillImage;
    public Button promoteButton;
    public Button fireButton; // <-- Новая ссылка
    public TextMeshProUGUI genderText; // <-- Новая ссылка
    public TextMeshProUGUI skillsText; // <-- Новая ссылка

    private StaffController linkedStaff;
    private RankData nextRankData;

    public void Setup(StaffController staff, List<RankData> rankDatabase)
    {
        linkedStaff = staff;
        
        if (rankDatabase != null)
        {
            nextRankData = rankDatabase.Find(r => r.rankLevel == staff.rank + 1);
        }

        UpdateCard();

        promoteButton.onClick.RemoveAllListeners();
        promoteButton.onClick.AddListener(OnPromoteClicked);
        
        // Настраиваем новую кнопку "Уволить"
        fireButton.onClick.RemoveAllListeners();
        fireButton.onClick.AddListener(OnFireClicked);
    }

    public void UpdateCard()
    {
        if (linkedStaff == null)
        {
            Destroy(gameObject);
            return;
        }

        nameText.text = linkedStaff.gameObject.name;
        roleText.text = $"{linkedStaff.currentRole} (Ранг {linkedStaff.rank})";
        salaryText.text = $"З/П: ${linkedStaff.salaryPerPeriod} / период";
        genderText.text = $"Пол: {linkedStaff.gender}"; // Отображаем пол

        // Обновляем полоску опыта
        if (nextRankData != null)
        {
            xpFillImage.fillAmount = (float)linkedStaff.experiencePoints / nextRankData.xpToNextRank;
        }
        else
        {
            xpFillImage.fillAmount = 1f;
        }

        // Обновляем текстовое описание навыков
        UpdateSkillsText();

        promoteButton.interactable = linkedStaff.isReadyForPromotion;
    }

    private void UpdateSkillsText()
    {
        if (linkedStaff.skills == null)
        {
            skillsText.text = "Навыки не определены";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>НАВЫКИ:</b>");
        
        sb.AppendLine($"<color=#add8e6>Канцелярит:</color> {SkillDescriptor.GetDescriptionForSkill(SkillType.PaperworkMastery, linkedStaff.skills.paperworkMastery)}");
        sb.AppendLine($"<color=#add8e6>Сидячесть:</color> {SkillDescriptor.GetDescriptionForSkill(SkillType.SedentaryResilience, linkedStaff.skills.sedentaryResilience)}");
        sb.AppendLine($"<color=#add8e6>Душность:</color> {SkillDescriptor.GetDescriptionForSkill(SkillType.Pedantry, linkedStaff.skills.pedantry)}");
        sb.AppendLine($"<color=#add8e6>Софт-скиллы:</color> {SkillDescriptor.GetDescriptionForSkill(SkillType.SoftSkills, linkedStaff.skills.softSkills)}");
        // sb.AppendLine($"<color=#add8e6>Коррупция:</color> {SkillDescriptor.GetDescriptionForSkill(SkillType.Corruption, linkedStaff.skills.corruption)}"); // Можно скрыть, если нужно

        skillsText.text = sb.ToString();
    }

    private void OnPromoteClicked()
    {
        Debug.Log($"Попытка повысить {linkedStaff.name}...");
        UpdateCard();
    }

    private void OnFireClicked()
    {
        // Вызываем метод из менеджера, передавая ему ссылку на сотрудника для увольнения
        if (HiringManager.Instance != null)
        {
            HiringManager.Instance.FireStaff(linkedStaff);
            // Карточка сама уничтожится вместе с объектом сотрудника, но можно и сразу
            // Destroy(gameObject);
        }
    }
}