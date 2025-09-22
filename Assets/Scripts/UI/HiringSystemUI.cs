// Файл: HiringSystemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.Collections.Generic;
using System.Linq;

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
    [SerializeField] private TextMeshProUGUI detailedNameText;
    [SerializeField] private TextMeshProUGUI detailedBioText;
    [SerializeField] private TextMeshProUGUI detailedSkillsText;
    [SerializeField] private TextMeshProUGUI detailedCostText;
    [SerializeField] private TextMeshProUGUI detailedUniqueSkillText;
    [SerializeField] private Button hireButton;
    [SerializeField] private Button closeButton;

    private Candidate currentlyViewedCandidate;
    private GameObject currentlyViewedPin; // Ссылка на "листок", который мы просматриваем

    // OnEnable вызывается каждый раз, когда панель становится активной
    private void OnEnable()
    {
        RefreshCandidates();
        detailedViewPanel.SetActive(false); // Убеждаемся, что детальная панель всегда скрыта при открытии
        
        // Назначаем слушателей на кнопки
        closeButton.onClick.AddListener(CloseDetailedView);
        hireButton.onClick.AddListener(OnHire);
    }

    // OnDisable вызывается, когда панель выключается
    private void OnDisable()
    {
        // Убираем слушателей, чтобы избежать ошибок
        closeButton.onClick.RemoveAllListeners();
        hireButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Очищает доску и генерирует новых кандидатов.
    /// </summary>
    public void RefreshCandidates()
    {
        // Уничтожаем старые "листки"
        foreach (Transform child in pinContainer)
        {
            Destroy(child.gameObject);
        }

        if (HiringManager.Instance == null) return;
        HiringManager.Instance.GenerateNewCandidates(); // Просим сгенерировать свежий список

        // Создаем и размещаем новые "листки"
        foreach (var candidate in HiringManager.Instance.AvailableCandidates)
        {
            GameObject pinGO = Instantiate(resumePinPrefab, pinContainer);
            
            RectTransform pinRect = pinGO.GetComponent<RectTransform>();
            if (pinRect != null)
            {
                // Задаем случайную позицию внутри контейнера
                float randomX = Random.Range(-pinContainer.rect.width / 2, pinContainer.rect.width / 2);
                float randomY = Random.Range(-pinContainer.rect.height / 2, pinContainer.rect.height / 2);
                pinRect.anchoredPosition = new Vector2(randomX, randomY);

                // Задаем случайный поворот для "живости"
                pinRect.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));
            }

            // Настраиваем сам "листок"
            pinGO.GetComponent<ResumePin>()?.Setup(candidate, this);
        }
    }

    /// <summary>
    /// Показывает детальную информацию о выбранном кандидате.
    /// </summary>
    public void ShowDetailedView(Candidate candidate, GameObject pinObject)
    {
        currentlyViewedCandidate = candidate;
        currentlyViewedPin = pinObject;

        // Заполняем все поля на детальной панели
        detailedNameText.text = candidate.Name;
        detailedBioText.text = candidate.Bio;
        detailedCostText.text = $"Стоимость: ${candidate.HiringCost}";
        DisplaySkills(candidate.Skills); // Отображаем навыки по "скрытой" логике

        // Показываем уникальный навык, если он есть
        if (candidate.UniqueActionsPool.Any())
        {
            detailedUniqueSkillText.gameObject.SetActive(true);
            detailedUniqueSkillText.text = $"Особый талант: {candidate.UniqueActionsPool.First().displayName}";
        }
        else
        {
            detailedUniqueSkillText.gameObject.SetActive(false);
        }
        
        // Показываем саму панель и прячем "листок"
        detailedViewPanel.SetActive(true);
        if (currentlyViewedPin != null)
        {
            currentlyViewedPin.SetActive(false);
        }
    }

    /// <summary>
    /// Формирует строку с одним видимым и несколькими скрытыми навыками.
    /// </summary>
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

    /// <summary>
    /// Закрывает панель детального просмотра.
    /// </summary>
    private void CloseDetailedView()
    {
        detailedViewPanel.SetActive(false);
        if (currentlyViewedPin != null)
        {
            currentlyViewedPin.SetActive(true); // Показываем "листок" обратно на доску
        }
    }

    /// <summary>
    /// Вызывается при нажатии на кнопку "Нанять".
    /// </summary>
    private void OnHire()
    {
        if (currentlyViewedCandidate == null) return;
        Hire(currentlyViewedCandidate);
    }

    /// <summary>
    /// Основная логика найма.
    /// </summary>
    public void Hire(Candidate candidate)
    {
        bool success = HiringManager.Instance.HireCandidate(candidate);
        if (success)
        {
            Debug.Log($"Успешно нанят: {candidate.Name}");
            CloseDetailedView();
            RefreshCandidates(); // Обновляем доску (чтобы убрать нанятого)
            FindFirstObjectByType<HiringPanelUI>()?.RefreshTeamList(); // Обновляем список команды
        }
        else
        {
            Debug.LogWarning($"Не удалось нанять {candidate.Name}.");
            // Здесь можно показать игроку всплывающее сообщение
        }
    }
}