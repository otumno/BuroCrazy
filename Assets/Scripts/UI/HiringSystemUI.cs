// Файл: Assets/Scripts/UI/HiringSystemUI.cs
using UnityEngine;
using UnityEngine.UI; 
using TMPro; 
using System.Text; 
using System.Collections; // <--- Нужно для Coroutines
using System.Collections.Generic; 
using System.Linq;
using Enums;
using Managers;
using UI;
using Utilities;

// Этот скрипт должен висеть на ГЛАВНОЙ ПАНЕЛИ найма.
public class HiringSystemUI : MonoBehaviour 
{
    [Header("Настройки доски")]
    [Tooltip("Префаб маленького 'листка' с именем кандидата")]
    [SerializeField] private GameObject resumePinPrefab;
    [Tooltip("Объект-контейнер, внутри которого будут размещаться 'листки'")]
    [SerializeField] private RectTransform pinContainer;

    [Header("Панель детального просмотра")]
    [SerializeField] private GameObject detailedViewPanel;
    [Tooltip("Изображение фона детальной панели (для окраски)")]
    [SerializeField] private Image detailedPanelBackground;
    [SerializeField] private TextMeshProUGUI detailedNameText;
    [SerializeField] private TextMeshProUGUI detailedBioText;
    [SerializeField] private TextMeshProUGUI detailedRoleRankText; 
    [SerializeField] private TextMeshProUGUI detailedSkillsText;
    [SerializeField] private TextMeshProUGUI detailedCostText;
    [SerializeField] private TextMeshProUGUI detailedUniqueSkillText;
    [SerializeField] private Button hireButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private SwitchToggle isTemporaryWorkerToggle;

    [Header("Общая информация")]
    [SerializeField] private TextMeshProUGUI playerMoneyText; 

    [Header("Данные")]
    [Tooltip("Перетащите сюда ассет RoleColorDatabase")]
    [SerializeField] private RoleColorDatabase roleColorDb; 

    // Внутренние переменные
    private Candidate currentlyViewedCandidate;
    private GameObject currentlyViewedPin; 
    private Dictionary<Candidate, GameObject> candidatePins = new Dictionary<Candidate, GameObject>();

    // --- ИСПРАВЛЕНИЕ: OnEnable теперь запускает процесс инициализации ---
    private void OnEnable()
    {
        // Запускаем корутину, которая дождется загрузки Менеджера
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += RefreshLocale;
        StartCoroutine(InitializeRoutine());
    }

    // --- НОВАЯ КОРУТИНА: Безопасная инициализация ---
    private IEnumerator InitializeRoutine()
    {
        // 1. Ждем, пока HiringManager появится и инициализируется
        yield return new WaitUntil(() => HiringManager.Instance != null);

        // 2. Ждем еще кадр на всякий случай, чтобы его списки наполнились
        yield return null;

        HiringManager.Instance.EnsureCandidateBoard();
        RemoveStalePins();
        RefreshCandidates();
        CloseDetailedView();
        UpdatePlayerMoneyDisplay();

        // Назначаем слушателей кнопок
        closeButton.onClick.RemoveAllListeners(); 
        closeButton.onClick.AddListener(CloseDetailedView);

        hireButton.onClick.RemoveAllListeners(); 
        hireButton.onClick.AddListener(OnHire);

        isTemporaryWorkerToggle.SetListener(TemporaryWorkerToggle_ValueChanged);
    }

    private void OnDisable()
    {
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= RefreshLocale;
        StopAllCoroutines(); // Останавливаем ожидание, если панель закрыли раньше времени
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
        if (hireButton != null) hireButton.onClick.RemoveAllListeners();
        if (isTemporaryWorkerToggle != null) isTemporaryWorkerToggle.SetListener(null);
    }

    /// <summary>
    /// Добавляет листки для кандидатов, у которых ещё нет представления на доске.
    /// </summary>
    void RefreshCandidates() 
    {
        if (resumePinPrefab == null)
        {
            Debug.LogError("[HiringSystemUI] Префаб 'Resume Pin Prefab' не назначен!");
            return;
        }

        if (roleColorDb == null) Debug.LogWarning("[HiringSystemUI] RoleColorDatabase не назначен!");

        // Создаем и размещаем новые "листки"
        foreach (var candidate in HiringManager.Instance.AvailableCandidates)
        {
            if (candidate == null || candidatePins.ContainsKey(candidate)) continue;

            GameObject pinGO = Instantiate(resumePinPrefab, pinContainer);
            RectTransform pinRect = pinGO.GetComponent<RectTransform>();

            // Задаем случайную позицию
            if (pinRect != null && pinContainer != null)
            {
                pinRect.anchorMin = new Vector2(0.5f, 0.5f);
                pinRect.anchorMax = new Vector2(0.5f, 0.5f);
                pinRect.pivot = new Vector2(0.5f, 0.5f);

                float halfWidth = pinContainer.rect.width / 2f;
                float halfHeight = pinContainer.rect.height / 2f;
                float paddingX = (pinRect.rect.width / 2f) * pinRect.localScale.x + 10f; 
                float paddingY = (pinRect.rect.height / 2f) * pinRect.localScale.y + 10f;

                float randomX = Random.Range(-halfWidth + paddingX, halfWidth - paddingX);
                float randomY = Random.Range(-halfHeight + paddingY, halfHeight - paddingY);

                randomX = Mathf.Clamp(randomX, -halfWidth + paddingX, halfWidth - paddingX);
                randomY = Mathf.Clamp(randomY, -halfHeight + paddingY, halfHeight - paddingY);

                pinRect.anchoredPosition = new Vector2(randomX, randomY);
                pinRect.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));
            }

            // Настраиваем сам "листок"
            ResumePin pinScript = pinGO.GetComponent<ResumePin>();
            if (pinScript != null)
            {
                 pinScript.Setup(candidate, this, roleColorDb); 
            }
            else
            {
                Debug.LogError($"[HiringSystemUI] Префаб 'Resume Pin Prefab' не содержит скрипт ResumePin!", resumePinPrefab);
            }

            candidatePins[candidate] = pinGO;
        }
         Debug.Log($"[HiringSystemUI] Создано {candidatePins.Count} 'листков' кандидатов на доске.");
    }

    public void ShowDetailedView(Candidate candidate, GameObject pinObject) 
    {
        if (candidate == null || detailedViewPanel == null) return;

        currentlyViewedCandidate = candidate;
        currentlyViewedPin = pinObject;
        
        RefreshCandidateText(candidate);

        Color bgColor = Color.grey; 
        if (roleColorDb != null)
        {
            bgColor = roleColorDb.GetColorForRole(candidate.Role, Color.grey); 
            Color.RGBToHSV(bgColor, out float H, out float S, out float V);
            bgColor = Color.HSVToRGB(H, S * 0.7f, V * 0.9f);
        }

        if (detailedPanelBackground != null) detailedPanelBackground.color = bgColor;

        UpdatePlayerMoneyDisplay();
        SetupEmploymentToggle(candidate);
        UpdateHiringCostDisplay();

        detailedViewPanel.SetActive(true);
        if (currentlyViewedPin != null) currentlyViewedPin.SetActive(false); 
    }

    private void RefreshLocale(UnityEngine.Localization.Locale locale)
    {
        UpdatePlayerMoneyDisplay();
        if (currentlyViewedCandidate == null) return;
        RefreshCandidateText(currentlyViewedCandidate);
        UpdateHiringCostDisplay();
    }

    private void RefreshCandidateText(Candidate candidate)
    {
        if (detailedNameText != null) detailedNameText.text = candidate.Name ?? GameText.Get("hiring.unnamed");
        if (detailedBioText != null) detailedBioText.text = candidate.GeneratedBio?.Localize() ?? candidate.Bio ?? GameText.Get("hiring.no_bio");
        if (detailedRoleRankText != null)
        {
            var rank = candidate.Rank != null ? candidate.Rank.LocalizedName : GameText.Get("hiring.entry_rank");
            detailedRoleRankText.text = $"{StaffRoleNames.GetDisplayName(candidate.Role)}\n({rank})";
        }
        DisplaySkills(candidate.Skills);
        if (detailedUniqueSkillText == null) return;
        var talent = candidate.UniqueActionsPool?.FirstOrDefault(a => a != null);
        detailedUniqueSkillText.gameObject.SetActive(talent != null);
        if (talent != null) detailedUniqueSkillText.text = GameText.Get("hiring.talent", talent.LocalizedDisplayName);
    }

    void DisplaySkills(CharacterSkills skills) 
    {
         if (detailedSkillsText == null) return; 

         if (skills == null) {
              detailedSkillsText.text = GameText.Get("hiring.skills_unknown");
              return;
         }

        List<KeyValuePair<string, float>> allSkills = new List<KeyValuePair<string, float>>
        {
            new KeyValuePair<string, float>(CharacterSkills.GetSkillString(SkillType.PaperworkMastery), skills.paperworkMastery),
            new KeyValuePair<string, float>(CharacterSkills.GetSkillString(SkillType.SedentaryResilience), skills.sedentaryResilience),
            new KeyValuePair<string, float>(CharacterSkills.GetSkillString(SkillType.Pedantry), skills.pedantry),
            new KeyValuePair<string, float>(CharacterSkills.GetSkillString(SkillType.SoftSkills), skills.softSkills)
        };

        System.Random rng = new System.Random(currentlyViewedCandidate.Name?.GetHashCode() ?? 0);
        allSkills = allSkills.OrderBy(a => rng.Next()).ToList();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(GameText.Get("hiring.skills_heading"));
        if (allSkills.Count > 0) { 
             sb.AppendLine($"{allSkills[0].Key}: {allSkills[0].Value:P0}"); 
        }
        for (int i = 1; i < 4; i++) { 
             sb.AppendLine(GameText.Get("hiring.hidden_skill"));
        }

        detailedSkillsText.text = sb.ToString();
    }

    public void CloseDetailedView() 
    {
        if (detailedViewPanel != null) detailedViewPanel.SetActive(false); 

        if (currentlyViewedPin != null)
        {
            if (currentlyViewedCandidate != null && candidatePins.ContainsKey(currentlyViewedCandidate) && candidatePins[currentlyViewedCandidate] == currentlyViewedPin)
            {
                 currentlyViewedPin.SetActive(true);
            } 
        }
        currentlyViewedCandidate = null;
        currentlyViewedPin = null;
    }

    void OnHire() 
    {
        if (currentlyViewedCandidate == null) return;
        Hire(currentlyViewedCandidate); 
    }

    void Hire(Candidate candidate) 
    {
         if (candidate == null || HiringManager.Instance == null) return;

        bool success = HiringManager.Instance.HireCandidate(candidate, GetSelectedEmploymentType());

        if (success)
        {
            if (candidatePins.ContainsKey(candidate))
            {
                 GameObject pinToDestroy = candidatePins[candidate];
                 if (pinToDestroy != null) Destroy(pinToDestroy);
                candidatePins.Remove(candidate);
            }

             if (currentlyViewedCandidate == candidate) CloseDetailedView();

            UpdatePlayerMoneyDisplay();

             HiringPanelUI hiringPanel = FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include);
             if (hiringPanel != null) hiringPanel.RefreshTeamList();
             FindFirstObjectByType<StaffSchedulePanelUI>(FindObjectsInactive.Include)?.RefreshTable();
        }
        else
        {
            if (currentlyViewedCandidate == candidate) UpdateHiringCostDisplay();
        }
    }

    private EmploymentType GetSelectedEmploymentType()
    {
        bool isTemporary = isTemporaryWorkerToggle != null && isTemporaryWorkerToggle.IsOn;
        return isTemporary ? EmploymentType.Temporary : EmploymentType.Permanent;
    }

    // Переключатель «временно» блокируется, если кандидата можно нанять только на одних условиях
    // (например, сюжетного — см. Candidate.Availability).
    private void SetupEmploymentToggle(Candidate candidate)
    {
        if (isTemporaryWorkerToggle == null || HiringManager.Instance == null) return;

        bool canHirePermanent = HiringManager.Instance.CanHireAs(candidate, EmploymentType.Permanent);
        bool canHireTemporary = HiringManager.Instance.CanHireAs(candidate, EmploymentType.Temporary);

        // По умолчанию — полная ставка; «временно» включаем, только если иначе нанять нельзя.
        isTemporaryWorkerToggle.SetState(!canHirePermanent && canHireTemporary, silent: true);
        isTemporaryWorkerToggle.SetInteractable(canHirePermanent && canHireTemporary);
    }

    private void TemporaryWorkerToggle_ValueChanged(bool isTemporary)
    {
        UpdateHiringCostDisplay();
    }

    // Временному показываем, из чего сложилась сумма: он платит вперёд и за найм, и за всю смену.
    private static string GetHiringCostText(Candidate candidate, EmploymentType employmentType, int hiringCost, int periodsCount)
    {
        if (employmentType != EmploymentType.Temporary) return GameText.Get("hiring.cost", hiringCost);

        int salaryPerPeriod = HiringManager.Instance.GetSalaryPerPeriod(candidate.Rank);
        int hiringPart = hiringCost - periodsCount * salaryPerPeriod;

        return GameText.Get("hiring.temp_cost", hiringCost, hiringPart, periodsCount, salaryPerPeriod);
    }

    private void UpdateHiringCostDisplay()
    {
        if (currentlyViewedCandidate == null || HiringManager.Instance == null) return;

        EmploymentType employmentType = GetSelectedEmploymentType();
        var employmentLabel = isTemporaryWorkerToggle != null ? isTemporaryWorkerToggle.GetComponentInChildren<LocalizedText>(true) : null;
        if (employmentLabel != null)
            employmentLabel.SetReference(GameText.Table, employmentType == EmploymentType.Temporary ? "hiring.current_day" : "hiring.full_time");
        int hiringCost = HiringManager.Instance.GetHiringCost(currentlyViewedCandidate, employmentType, out int paidPeriods);

        if (detailedCostText != null) detailedCostText.text = GetHiringCostText(currentlyViewedCandidate, employmentType, hiringCost, paidPeriods);

        if (hireButton != null)
        {
            bool canAfford = PlayerWallet.Instance != null && PlayerWallet.Instance.GetCurrentMoney() >= hiringCost;
            hireButton.interactable = canAfford && HiringManager.Instance.CanHireAs(currentlyViewedCandidate, employmentType) &&
                HiringManager.GetHiringShift(currentlyViewedCandidate.Rank, employmentType) != Data.Calendar.CalendarDayPeriodType.None;
        }
    }

    void UpdatePlayerMoneyDisplay() 
    {
        if (playerMoneyText != null && PlayerWallet.Instance != null)
        {
            playerMoneyText.text = GameText.Get("hiring.balance", PlayerWallet.Instance.GetCurrentMoney());
        }
    }

    void RemoveStalePins() 
    {
        if (HiringManager.Instance == null) return;

        List<Candidate> currentCandidates = HiringManager.Instance.AvailableCandidates;
        List<Candidate> pinsToRemove = new List<Candidate>();

        foreach(var pair in candidatePins) {
            if (!currentCandidates.Contains(pair.Key) || pair.Value == null) {
                 pinsToRemove.Add(pair.Key);
                 if (pair.Value != null) Destroy(pair.Value); 
            }
        }

        foreach(var candidateToRemove in pinsToRemove) {
            candidatePins.Remove(candidateToRemove);
        }
    }
}