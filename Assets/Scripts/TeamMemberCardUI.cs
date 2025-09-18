using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TeamMemberCardUI : MonoBehaviour
{
    [Header("Ссылки на UI элементы")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI stressText;
    [SerializeField] private Slider stressSlider;
    [SerializeField] private Button fireButton;

    private StaffController assignedStaff;

    /// <summary>
    /// Метод Setup стал проще. Ему нужен только сам сотрудник.
    /// </summary>
    public void Setup(StaffController staff)
    {
        this.assignedStaff = staff;
        UpdateCard();
    }

    /// <summary>
    /// Обновляет всю информацию на карточке.
    /// </summary>
    public void UpdateCard()
    {
        if (assignedStaff == null) return;

        nameText.text = assignedStaff.characterName;
        roleText.text = assignedStaff.currentRole.ToString();
        
        // Теперь мы просим ExperienceManager найти нужный ранг
        if (ExperienceManager.Instance != null)
        {
            RankData currentRank = ExperienceManager.Instance.GetRankByXP(assignedStaff.experiencePoints);
            if (currentRank != null)
            {
                rankText.text = currentRank.rankName;
            }
            else
            {
                rankText.text = "Без ранга";
            }
        }
        
        float currentStress = assignedStaff.GetStressValue();
        stressText.text = $"Стресс: {currentStress:F0}%";
        
        if (stressSlider != null)
        {
            stressSlider.value = currentStress / 100f;
        }
    }
}