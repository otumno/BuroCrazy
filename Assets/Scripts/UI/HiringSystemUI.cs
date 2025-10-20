// Файл: Assets/Scripts/UI/HiringSystemUI.cs
using UnityEngine;
using UnityEngine.UI; // Required for Button
using TMPro; // Required for TextMeshProUGUI
using System.Text; // Required for StringBuilder
using System.Collections.Generic; // Required for List<>
using System.Linq; // Required for Linq methods like Any()

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
    [SerializeField] private TextMeshProUGUI detailedRoleRankText; // Поле для Роли/Ранга
    [SerializeField] private TextMeshProUGUI detailedSkillsText;
    [SerializeField] private TextMeshProUGUI detailedCostText;
    [SerializeField] private TextMeshProUGUI detailedUniqueSkillText;
    [SerializeField] private Button hireButton;
    [SerializeField] private Button closeButton;

    [Header("Общая информация")]
    [SerializeField] private TextMeshProUGUI playerMoneyText; // Поле для денег игрока

    // Внутренние переменные
    private Candidate currentlyViewedCandidate;
    private GameObject currentlyViewedPin; // Ссылка на GameObject "листка" для скрытия/показа
    private int lastGeneratedDay = -1; // Отслеживаем день генерации
    // Словарь для хранения связи "кандидат -> его листок на доске"
    private Dictionary<Candidate, GameObject> candidatePins = new Dictionary<Candidate, GameObject>();

    // OnEnable вызывается каждый раз, когда панель становится активной
    private void OnEnable()
    {
        // Безопасно получаем текущий день
        int currentDay = ClientSpawner.Instance != null ? ClientSpawner.Instance.GetCurrentDay() : 1;

        // Генерируем новых кандидатов ТОЛЬКО если это новый день
        if (currentDay != lastGeneratedDay)
        {
            RefreshCandidates(); // Обновляем список кандидатов и "листки"
            lastGeneratedDay = currentDay; // Запоминаем день
        }

        UpdatePlayerMoneyDisplay(); // Обновляем отображение денег игрока
        detailedViewPanel.SetActive(false); // Убеждаемся, что детальная панель скрыта

        // Назначаем слушателей кнопок (добавляем проверки на null)
        if (closeButton != null) closeButton.onClick.AddListener(CloseDetailedView);
        if (hireButton != null) hireButton.onClick.AddListener(OnHire);
    }

    // OnDisable вызывается, когда панель выключается
    private void OnDisable()
    {
        // Убираем слушателей, чтобы избежать ошибок и утечек памяти
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
        if (hireButton != null) hireButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Очищает доску, генерирует новых кандидатов у HiringManager и создает для них "листки".
    /// </summary>
    public void RefreshCandidates()
    {
        // Уничтожаем старые "листки"
        if (pinContainer != null)
        {
            foreach (Transform child in pinContainer)
            {
                Destroy(child.gameObject);
            }
        }
        candidatePins.Clear(); // Очищаем словарь

        // Проверяем наличие HiringManager
        if (HiringManager.Instance == null)
        {
            Debug.LogError("[HiringSystemUI] HiringManager не найден! Не могу сгенерировать кандидатов.");
            return;
        }

        // Запускаем генерацию новых кандидатов
        HiringManager.Instance.GenerateNewCandidates();

        // Проверяем наличие префаба "листка"
        if (resumePinPrefab == null)
        {
            Debug.LogError("[HiringSystemUI] Префаб 'Resume Pin Prefab' не назначен! Не могу создать 'листки'.");
            return;
        }


        // Создаем и размещаем новые "листки" для сгенерированных кандидатов
        foreach (var candidate in HiringManager.Instance.AvailableCandidates)
        {
            if (candidate == null) continue; // Пропускаем null кандидатов

            GameObject pinGO = Instantiate(resumePinPrefab, pinContainer);
            RectTransform pinRect = pinGO.GetComponent<RectTransform>();

            // Задаем случайную позицию и поворот для "листка"
            if (pinRect != null && pinContainer != null)
            {
                // Задаем случайную позицию внутри контейнера
                float randomX = Random.Range(-pinContainer.rect.width / 2f, pinContainer.rect.width / 2f);
                float randomY = Random.Range(-pinContainer.rect.height / 2f, pinContainer.rect.height / 2f);
                pinRect.anchoredPosition = new Vector2(randomX, randomY);
                // Задаем случайный поворот
                pinRect.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));
            }

            // Настраиваем сам "листок", передавая данные кандидата и ссылку на этот UI
            ResumePin pinScript = pinGO.GetComponent<ResumePin>();
            if (pinScript != null)
            {
                pinScript.Setup(candidate, this);
            }
            else
            {
                Debug.LogError($"[HiringSystemUI] Префаб 'Resume Pin Prefab' не содержит скрипт ResumePin!", resumePinPrefab);
            }

            // Запоминаем, какой листок соответствует какому кандидату
            candidatePins[candidate] = pinGO;
        }
         Debug.Log($"[HiringSystemUI] Создано {candidatePins.Count} 'листков' кандидатов на доске.");
    }

    /// <summary>
    /// Показывает панель с детальной информацией о выбранном кандидате.
    /// </summary>
    /// <param name="candidate">Кандидат для отображения.</param>
    /// <param name="pinObject">GameObject "листка", который был нажат.</param>
    public void ShowDetailedView(Candidate candidate, GameObject pinObject)
    {
        // Проверяем входные данные
        if (candidate == null || detailedViewPanel == null)
        {
            Debug.LogError("[HiringSystemUI] Ошибка: Кандидат или панель детального просмотра не найдены.");
            return;
        }


        currentlyViewedCandidate = candidate;
        currentlyViewedPin = pinObject;

        // Заполняем поля на детальной панели
        if (detailedNameText != null) detailedNameText.text = candidate.Name;
        if (detailedBioText != null) detailedBioText.text = candidate.Bio;
        if (detailedCostText != null) detailedCostText.text = $"Стоимость: ${candidate.HiringCost}";

        // Отображаем Роль и Ранг
        if (detailedRoleRankText != null)
        {
            string roleName = GetRoleNameInRussian(candidate.Role);
            string rankName = candidate.Rank != null ? candidate.Rank.rankName : "Начальный ранг";
            detailedRoleRankText.text = $"{roleName}\n({rankName})"; // Используем \n для переноса строки
        }

        // Отображаем навыки
        if (candidate.Skills != null) DisplaySkills(candidate.Skills);
        else if (detailedSkillsText != null) detailedSkillsText.text = "Навыки: Неизвестно";


        // Показываем уникальный навык, если он есть
        bool hasUnique = candidate.UniqueActionsPool != null && candidate.UniqueActionsPool.Any(a => a != null);
        if (detailedUniqueSkillText != null)
        {
            if (hasUnique)
            {
                detailedUniqueSkillText.gameObject.SetActive(true);
                // Берем имя первого не-null уникального действия
                detailedUniqueSkillText.text = $"Особый талант: {candidate.UniqueActionsPool.First(a => a != null).displayName}";
            }
            else
            {
                detailedUniqueSkillText.gameObject.SetActive(false);
            }
        }


        // Обновляем отображение денег игрока (на всякий случай)
        UpdatePlayerMoneyDisplay();

        // Проверяем, хватает ли денег на найм, и управляем кнопкой
        if (hireButton != null)
        {
            bool canAfford = PlayerWallet.Instance != null && PlayerWallet.Instance.GetCurrentMoney() >= candidate.HiringCost;
            hireButton.interactable = canAfford;
        }

        // Показываем саму панель и прячем "листок"
        detailedViewPanel.SetActive(true);
        if (currentlyViewedPin != null)
        {
            currentlyViewedPin.SetActive(false); // Прячем "листок", пока открыта детальная панель
        }
    }

    /// <summary>
    /// Формирует строку с одним видимым и несколькими скрытыми навыками для отображения.
    /// </summary>
    /// <param name="skills">Навыки кандидата.</param>
    private void DisplaySkills(CharacterSkills skills)
    {
         if (detailedSkillsText == null) return; // Выходим, если текстовое поле не назначено

         if (skills == null) {
              detailedSkillsText.text = "Навыки: Неизвестно";
              return;
         }

        // Собираем список пар "Название навыка" - "Значение"
        List<KeyValuePair<string, float>> allSkills = new List<KeyValuePair<string, float>>
        {
            new KeyValuePair<string, float>("Бюрократия", skills.paperworkMastery),
            new KeyValuePair<string, float>("Усидчивость", skills.sedentaryResilience),
            new KeyValuePair<string, float>("Педантичность", skills.pedantry),
            new KeyValuePair<string, float>("Коммуникация", skills.softSkills)
            // Коррупцию не показываем явно
        };

        // Перемешиваем список случайным образом
        System.Random rng = new System.Random();
        allSkills = allSkills.OrderBy(a => rng.Next()).ToList();

        // Формируем текст: первый навык показываем, остальные скрываем
        StringBuilder sb = new StringBuilder();
        // Показываем первый навык из перемешанного списка
        sb.AppendLine($"{allSkills[0].Key}: {allSkills[0].Value:P0}"); // P0 = проценты без знаков после запятой
        // Добавляем строки для скрытых навыков
        for (int i = 1; i < allSkills.Count; i++) {
             sb.AppendLine("Скрытый навык: ???");
        }

        detailedSkillsText.text = sb.ToString();
    }

    /// <summary>
    /// Закрывает панель детального просмотра и показывает "листок" обратно.
    /// </summary>
    private void CloseDetailedView()
    {
        if (detailedViewPanel != null) detailedViewPanel.SetActive(false);
        // Показываем "листок" обратно, если он был скрыт
        if (currentlyViewedPin != null)
        {
            currentlyViewedPin.SetActive(true);
        }
        currentlyViewedCandidate = null; // Сбрасываем выбранного кандидата
        currentlyViewedPin = null; // Сбрасываем ссылку на "листок"
    }

    /// <summary>
    /// Вызывается при нажатии на кнопку "Нанять" в детальном окне.
    /// </summary>
    private void OnHire()
    {
        if (currentlyViewedCandidate == null)
        {
            Debug.LogError("[HiringSystemUI] OnHire вызван, но currentlyViewedCandidate == null!");
            return;
        }
        Hire(currentlyViewedCandidate); // Вызываем основную логику найма
    }

    /// <summary>
    /// Выполняет найм кандидата через HiringManager, обновляет UI.
    /// </summary>
    /// <param name="candidate">Кандидат для найма.</param>
    public void Hire(Candidate candidate)
    {
         if (candidate == null) {
              Debug.LogError("[HiringSystemUI] Попытка найма null кандидата!");
              return;
         }
         if (HiringManager.Instance == null) {
              Debug.LogError("[HiringSystemUI] HiringManager не найден! Найм невозможен.");
              return;
         }


        // Пытаемся нанять кандидата
        bool success = HiringManager.Instance.HireCandidate(candidate);

        if (success)
        {
            Debug.Log($"[HiringSystemUI] Успешно нанят: {candidate.Name}");
            CloseDetailedView(); // Закрываем детальное окно

            // Удаляем "листок" только что нанятого кандидата с доски
            if (candidatePins.ContainsKey(candidate))
            {
                 GameObject pinToDestroy = candidatePins[candidate];
                 if (pinToDestroy != null) Destroy(pinToDestroy); // Уничтожаем GameObject "листка"
                candidatePins.Remove(candidate); // Удаляем из словаря
            }

            // Обновляем отображение денег игрока (т.к. они были списаны)
            UpdatePlayerMoneyDisplay();

            // Обновляем список сотрудников на основной панели отдела кадров (если она есть)
            FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
        }
        else
        {
            Debug.LogWarning($"[HiringSystemUI] Не удалось нанять {candidate.Name}. Возможно, не хватило денег или нет места.");
            // Обновляем состояние кнопки найма на случай, если проблема была в деньгах
             if (currentlyViewedCandidate == candidate && hireButton != null && PlayerWallet.Instance != null) {
                 hireButton.interactable = PlayerWallet.Instance.GetCurrentMoney() >= candidate.HiringCost;
             }
        }
    }

    /// <summary>
    /// Обновляет текстовое поле с текущим количеством денег игрока.
    /// </summary>
    private void UpdatePlayerMoneyDisplay()
    {
        if (playerMoneyText != null)
        {
            if (PlayerWallet.Instance != null)
            {
                playerMoneyText.text = $"Ваш счет: ${PlayerWallet.Instance.GetCurrentMoney()}";
            }
            else
            {
                playerMoneyText.text = "Счет: Ошибка"; // Если кошелек не найден
                 Debug.LogError("[HiringSystemUI] PlayerWallet не найден! Не могу отобразить счет.");
            }
        }
         // else: Если playerMoneyText не назначен в инспекторе, ничего не делаем
    }


    /// <summary>
    /// Вспомогательный метод для получения русского названия роли.
    /// </summary>
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
            case StaffController.Role.Unassigned: return "Без роли";
            default: return role.ToString(); // Возвращаем системное имя, если перевод не найден
        }
    }

} // Конец класса HiringSystemUI