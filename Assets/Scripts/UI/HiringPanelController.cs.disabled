// Файл: HiringPanelController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.Collections.Generic;
using System.Linq;

// Класс CandidateCardUI был заменен на отдельный скрипт ResumePin.cs

public class HiringPanelController : MonoBehaviour
{
    [Header("Настройки доски")]
    [Tooltip("Префаб маленького 'листка' с именем кандидата")]
    [SerializeField] private GameObject resumePinPrefab;
    [Tooltip("Объект-контейнер, внутри которого будут размещаться 'листки'")]
    [SerializeField] private RectTransform contentContainer;

    [Header("Панель детального просмотра")]
    [SerializeField] private GameObject detailedViewPanel;
    [SerializeField] private TextMeshProUGUI detailedNameText;
    [SerializeField] private TextMeshProUGUI detailedBioText;
    [SerializeField] private TextMeshProUGUI detailedSkillsText;
    [SerializeField] private TextMeshProUGUI detailedCostText;
    [SerializeField] private TextMeshProUGUI detailedUniqueSkillText;
    [SerializeField] private Button hireButton;
    [SerializeField] private Button closeButton;

    private Candidate currentlyViewedCandidate;
    private GameObject currentlyViewedPin;

    private void OnEnable()
    {
        RefreshCandidates();
        detailedViewPanel.SetActive(false);
        closeButton.onClick.AddListener(CloseDetailedView);
        hireButton.onClick.AddListener(OnHire);
    }

    public void RefreshCandidates()
    {
        foreach (Transform child in contentContainer) { Destroy(child.gameObject); }
        if (HiringManager.Instance == null) return;
        HiringManager.Instance.GenerateNewCandidates();

        foreach (var candidate in HiringManager.Instance.AvailableCandidates)
        {
            GameObject pinGO = Instantiate(resumePinPrefab, contentContainer);
            
            RectTransform pinRect = pinGO.GetComponent<RectTransform>();
            float randomX = Random.Range(-contentContainer.rect.width / 2, contentContainer.rect.width / 2);
            float randomY = Random.Range(-contentContainer.rect.height / 2, contentContainer.rect.height / 2);
            pinRect.anchoredPosition = new Vector2(randomX, randomY);
            pinRect.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));

            pinGO.GetComponent<ResumePin>()?.Setup(candidate, this);
        }
    }

    public void ShowDetailedView(Candidate candidate, GameObject pinObject)
    {
        currentlyViewedCandidate = candidate;
        currentlyViewedPin = pinObject;

        detailedNameText.text = candidate.Name;
        detailedBioText.text = candidate.Bio;
        detailedCostText.text = $"Стоимость: ${candidate.HiringCost}";
        DisplaySkills(candidate.Skills);

        if (candidate.UniqueActionsPool.Any())
        {
            detailedUniqueSkillText.gameObject.SetActive(true);
            detailedUniqueSkillText.text = $"Особый талант: {candidate.UniqueActionsPool.First().displayName}";
        }
        else
        {
            detailedUniqueSkillText.gameObject.SetActive(false);
        }
        
        detailedViewPanel.SetActive(true);
        if (currentlyViewedPin != null)
        {
            currentlyViewedPin.SetActive(false);
        }
    }

    private void DisplaySkills(CharacterSkills skills)
    {
        List<KeyValuePair<string, float>> allSkills = new List<KeyValuePair<string, float>>
        {
            new KeyValuePair<string, float>("Бюрократия", skills.paperworkMastery),
            new KeyValuePair<string, float>("Усидчивость", skills.sedentaryResilience),
            new KeyValuePair<string, float>("Педантичность", skills.pedantry),
            new KeyValuePair<string, float>("Коммуникация", skills.softSkills)
        };
        System.Random rng = new System.Random();
        allSkills = allSkills.OrderBy(a => rng.Next()).ToList();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{allSkills[0].Key}: {allSkills[0].Value:P0}");
        sb.AppendLine("Скрытый навык: ???");
        sb.AppendLine("Скрытый навык: ???");
        sb.AppendLine("Скрытый навык: ???");
        detailedSkillsText.text = sb.ToString();
    }

    private void CloseDetailedView()
    {
        detailedViewPanel.SetActive(false);
        if (currentlyViewedPin != null)
        {
            currentlyViewedPin.SetActive(true);
        }
    }

    private void OnHire()
    {
        if (currentlyViewedCandidate == null) return;
        Hire(currentlyViewedCandidate);
    }

    public void Hire(Candidate candidate)
    {
        bool success = HiringManager.Instance.HireCandidate(candidate);
        if (success)
        {
            CloseDetailedView();
            RefreshCandidates();
            FindFirstObjectByType<HiringPanelUI>()?.RefreshTeamList();
        }
        else
        {
            Debug.LogWarning($"Не удалось нанять {candidate.Name}.");
        }
    }
}