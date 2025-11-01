// Файл: Assets/Scripts/UI/HiringSystemUI.cs
using UnityEngine;
using UnityEngine.UI; // Required for Button, Image
using TMPro; // Required for TextMeshProUGUI
using System.Text; // Required for StringBuilder
using System.Collections.Generic; // Required for List<>, Dictionary<>
using System.Linq; // Required for Linq methods like Any(), FirstOrDefault()

// Этот скрипт должен висеть на ГЛАВНОЙ ПАНЕЛИ найма.
public class HiringSystemUI : MonoBehaviour // This line is correct
{
    [Header("Настройки доски")]
    [Tooltip("Префаб маленького 'листка' с именем кандидата")]
    [SerializeField] private GameObject resumePinPrefab;
    [Tooltip("Объект-контейнер, внутри которого будут размещаться 'листки'")]
    [SerializeField] private RectTransform pinContainer;

    [Header("Панель детального просмотра")]
    [SerializeField] private GameObject detailedViewPanel;
    [Tooltip("Изображение фона детальной панели (для окраски)")]
    [SerializeField] private Image detailedPanelBackground; // Фон для окраски
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

    [Header("Данные")]
    [Tooltip("Перетащите сюда ассет RoleColorDatabase")]
    [SerializeField] private RoleColorDatabase roleColorDb; // Ссылка на базу данных цветов

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
        // Debug.Log($"[HiringSystemUI] OnEnable. Current Day: {currentDay}, Last Generated Day: {lastGeneratedDay}"); // Отладка

        // Генерируем новых кандидатов ТОЛЬКО если это новый день
        // Или если список кандидатов пуст (на случай первого открытия или ошибки)
        if (currentDay != lastGeneratedDay || (HiringManager.Instance != null && !HiringManager.Instance.AvailableCandidates.Any()))
        {
            Debug.Log("[HiringSystemUI] Обновление списка кандидатов...");
            RefreshCandidates(); // Обновляем список кандидатов и "листки"
            lastGeneratedDay = currentDay; // Запоминаем день
        } else {
             Debug.Log("[HiringSystemUI] Список кандидатов для этого дня уже сгенерирован.");
             // Можно добавить проверку и удаление "протухших" пинов, если кандидаты могли быть удалены из менеджера
             RemoveStalePins(); // <<< Вызываем очистку устаревших пинов
        }


        UpdatePlayerMoneyDisplay(); // Обновляем отображение денег игрока
        if(detailedViewPanel != null) detailedViewPanel.SetActive(false); // Убеждаемся, что детальная панель скрыта

        // Назначаем слушателей кнопок (добавляем проверки на null)
        if (closeButton != null) {
             closeButton.onClick.RemoveAllListeners(); // Очищаем старые перед добавлением
             closeButton.onClick.AddListener(CloseDetailedView);
        } else Debug.LogError("Close Button не назначен в HiringSystemUI!");

        if (hireButton != null) {
             hireButton.onClick.RemoveAllListeners(); // Очищаем старые перед добавлением
             hireButton.onClick.AddListener(OnHire);
        } else Debug.LogError("Hire Button не назначен в HiringSystemUI!");
    }

    // OnDisable вызывается, когда панель выключается
    private void OnDisable()
    {
        // Убираем слушателей, чтобы избежать ошибок и утечек памяти
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
        if (hireButton != null) hireButton.onClick.RemoveAllListeners();
         // Debug.Log("[HiringSystemUI] OnDisable. Слушатели кнопок удалены."); // Отладка
    }

    /// <summary>
    /// Очищает доску, генерирует новых кандидатов у HiringManager и создает для них "листки".
    /// </summary>
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'public' ---
    /*public*/ void RefreshCandidates() // Line 122 - Removed 'public'
    {
        // Уничтожаем старые "листки"
        if (pinContainer != null)
        {
            // Iterate backwards to safely destroy children
            for (int i = pinContainer.childCount - 1; i >= 0; i--) {
                 // Добавим проверку на null на всякий случай
                 if (pinContainer.GetChild(i) != null) {
                     Destroy(pinContainer.GetChild(i).gameObject);
                 }
            }
             // Debug.Log($"[HiringSystemUI] Старые 'листки' ({pinContainer.childCount} шт.) удалены."); // Неверно показывает 0 после цикла
        } else {
             Debug.LogError("[HiringSystemUI] Pin Container не назначен! Не могу очистить старые листки.");
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

        // Проверяем наличие базы данных цветов
        if (roleColorDb == null) {
            Debug.LogWarning("[HiringSystemUI] RoleColorDatabase не назначен! 'Листки' будут белыми.");
        }


        // Создаем и размещаем новые "листки" для сгенерированных кандидатов
        foreach (var candidate in HiringManager.Instance.AvailableCandidates)
        {
            if (candidate == null)
            {
                Debug.LogWarning("[HiringSystemUI] Обнаружен null кандидат в списке AvailableCandidates при создании 'листков'.");
                continue; // Пропускаем null кандидатов
            }

            GameObject pinGO = Instantiate(resumePinPrefab, pinContainer);
            RectTransform pinRect = pinGO.GetComponent<RectTransform>();

            // Задаем случайную позицию и поворот для "листка"
            if (pinRect != null && pinContainer != null)
            {
                pinRect.anchorMin = new Vector2(0.5f, 0.5f);
                pinRect.anchorMax = new Vector2(0.5f, 0.5f);
                pinRect.pivot = new Vector2(0.5f, 0.5f);

                float halfWidth = pinContainer.rect.width / 2f;
                float halfHeight = pinContainer.rect.height / 2f;
                // Use RectTransform dimensions for padding calculation
                float paddingX = (pinRect.rect.width / 2f) * pinRect.localScale.x + 10f; // Учитываем scale
                float paddingY = (pinRect.rect.height / 2f) * pinRect.localScale.y + 10f;


                float randomX = Random.Range(-halfWidth + paddingX, halfWidth - paddingX);
                float randomY = Random.Range(-halfHeight + paddingY, halfHeight - paddingY);


                // Ограничиваем координаты, чтобы точно не вылезали за пределы
                randomX = Mathf.Clamp(randomX, -halfWidth + paddingX, halfWidth - paddingX);
                randomY = Mathf.Clamp(randomY, -halfHeight + paddingY, halfHeight - paddingY);

                pinRect.anchoredPosition = new Vector2(randomX, randomY);
                pinRect.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));
            } else if (pinContainer == null) {
                 Debug.LogError("Pin Container не назначен!");
            }


            // Настраиваем сам "листок", передавая данные кандидата, ссылку на этот UI и базу цветов
            ResumePin pinScript = pinGO.GetComponent<ResumePin>();
            if (pinScript != null)
            {
                pinScript.Setup(candidate, this, roleColorDb); // Передаем roleColorDb
            }
            else
            {
                Debug.LogError($"[HiringSystemUI] Префаб 'Resume Pin Prefab' ({resumePinPrefab.name}) не содержит скрипт ResumePin!", resumePinPrefab);
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
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'public' ---
    public void ShowDetailedView(Candidate candidate, GameObject pinObject) // Line 222 - Removed 'public'
    {
        // Проверяем входные данные
        if (candidate == null || detailedViewPanel == null)
        {
            Debug.LogError($"[HiringSystemUI] Ошибка ShowDetailedView: Candidate is {(candidate == null ? "NULL" : "OK")}, detailedViewPanel is {(detailedViewPanel == null ? "NULL" : "OK")}");
            return;
        }


        currentlyViewedCandidate = candidate;
        currentlyViewedPin = pinObject;
         Debug.Log($"[HiringSystemUI] Показ детальной информации для: {candidate.Name}");


        // Заполняем поля на детальной панели
        if (detailedNameText != null) detailedNameText.text = candidate.Name ?? "Безымянный";
        else Debug.LogWarning("Detailed Name Text не назначен.");

        if (detailedBioText != null) detailedBioText.text = candidate.Bio ?? "Биография отсутствует.";
        else Debug.LogWarning("Detailed Bio Text не назначен.");

        if (detailedCostText != null) detailedCostText.text = $"Стоимость: ${candidate.HiringCost}";
        else Debug.LogWarning("Detailed Cost Text не назначен.");


        // Отображаем Роль и Ранг
        if (detailedRoleRankText != null)
        {
            string roleName = GetRoleNameInRussian(candidate.Role);
            string rankName = candidate.Rank != null ? candidate.Rank.rankName : "Начальный ранг";
            detailedRoleRankText.text = $"{roleName}\n({rankName})"; // Используем \n для переноса строки
        } else Debug.LogWarning("Detailed Role Rank Text не назначен.");


        // Отображаем навыки
        if (candidate.Skills != null && detailedSkillsText != null) DisplaySkills(candidate.Skills);
        else if (detailedSkillsText != null) detailedSkillsText.text = "Навыки: Неизвестно";
        else Debug.LogWarning("Detailed Skills Text не назначен.");


        // Показываем уникальный навык, если он есть
        bool hasUnique = candidate.UniqueActionsPool != null && candidate.UniqueActionsPool.Any(a => a != null);
        if (detailedUniqueSkillText != null)
        {
            if (hasUnique)
            {
                detailedUniqueSkillText.gameObject.SetActive(true);
                // Берем имя первого не-null уникального действия
                try { // Добавим try-catch на случай пустого списка после фильтрации
                    StaffAction firstUnique = candidate.UniqueActionsPool.FirstOrDefault(a => a != null);
                    detailedUniqueSkillText.text = $"Особый талант: {firstUnique?.displayName ?? "Ошибка"}";
                } catch (System.Exception ex){
                     detailedUniqueSkillText.text = "Особый талант: Ошибка";
                     Debug.LogError($"Ошибка при получении имени уникального таланта для {candidate.Name}: {ex.Message}");
                }
            }
            else
            {
                detailedUniqueSkillText.gameObject.SetActive(false);
            }
        } else Debug.LogWarning("Detailed Unique Skill Text не назначен.");



        // Устанавливаем цвет фона детальной панели
        Color bgColor = Color.grey; // Цвет по умолчанию для панели
        if (roleColorDb != null)
        {
            // Используем цвет из базы, но делаем его чуть темнее/менее насыщенным для фона
            bgColor = roleColorDb.GetColorForRole(candidate.Role, Color.grey); // Получаем цвет
            // Пример небольшой модификации цвета для фона:
            Color.RGBToHSV(bgColor, out float H, out float S, out float V);
            // Уменьшаем насыщенность (до 70%) и яркость (до 90%), чтобы текст был читаем
            bgColor = Color.HSVToRGB(H, S * 0.7f, V * 0.9f);
        }
         else { Debug.LogWarning("[HiringSystemUI] RoleColorDatabase не назначен! Фон детальной панели будет серым."); }

        if (detailedPanelBackground != null)
        {
            detailedPanelBackground.color = bgColor;
        } else {
             Debug.LogWarning("Detailed Panel Background (Image) не назначен в HiringSystemUI!");
        }


        // Обновляем отображение денег игрока
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
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'private' ---
    /*private*/ void DisplaySkills(CharacterSkills skills) // Line 329 - Removed 'private'
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
        System.Random rng = new System.Random(currentlyViewedCandidate.Name.GetHashCode()); // Используем хэш имени ТЕКУЩЕГО кандидата
        allSkills = allSkills.OrderBy(a => rng.Next()).ToList();


        // Формируем текст: первый навык показываем, остальные скрываем
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Навыки:</b>"); // Добавляем заголовок
        // Показываем первый навык из перемешанного списка
        if (allSkills.Count > 0) { // Проверка на пустой список
             sb.AppendLine($"{allSkills[0].Key}: {allSkills[0].Value:P0}"); // P0 = проценты без знаков после запятой
        }
        // Добавляем строки для скрытых навыков
        for (int i = 1; i < 4; i++) { // Всегда добавляем 3 строки "???"
             sb.AppendLine("Скрытый навык: ???");
        }


        detailedSkillsText.text = sb.ToString();
    }

    /// <summary>
    /// Закрывает панель детального просмотра и показывает "листок" обратно.
    /// </summary>
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'private' ---
    public void CloseDetailedView() // Line 371 - Removed 'private' in previous fix, should be private unless called from outside
    {
         Debug.Log("[HiringSystemUI] Закрытие детального вида..."); // Отладочное сообщение
        // Скрываем панель деталей
        if (detailedViewPanel != null)
        {
             detailedViewPanel.SetActive(false);
        } else {
             Debug.LogError("Detailed View Panel не назначена!");
        }

        // Показываем "листок" обратно, если он был скрыт и все еще существует в словаре
        if (currentlyViewedPin != null)
        {
            // Проверка на случай, если кандидат был нанят и листок удален
            // Also check if the candidate reference itself is still valid
            if (currentlyViewedCandidate != null && candidatePins.ContainsKey(currentlyViewedCandidate) && candidatePins[currentlyViewedCandidate] == currentlyViewedPin)
            {
                 currentlyViewedPin.SetActive(true);
            } else {
                 Debug.Log("Листок не был показан обратно (вероятно, кандидат был нанят или ссылка устарела).");
            }
        }
        // Сбрасываем ссылки на текущего кандидата и его пин
        currentlyViewedCandidate = null;
        currentlyViewedPin = null;
    } // Конец CloseDetailedView

    /// <summary>
    /// Вызывается при нажатии на кнопку "Нанять" в детальном окне.
    /// </summary>
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'private' ---
    /*private*/ void OnHire() // Line 392 - Removed 'private'
    {
        if (currentlyViewedCandidate == null)
        {
            Debug.LogError("[HiringSystemUI] OnHire вызван, но currentlyViewedCandidate == null!");
            return;
        }
         Debug.Log($"[HiringSystemUI] Нажата кнопка 'Нанять' для {currentlyViewedCandidate.Name}.");
        Hire(currentlyViewedCandidate); // Вызываем основную логику найма
    }

    /// <summary>
    /// Выполняет найм кандидата через HiringManager, обновляет UI.
    /// </summary>
    /// <param name="candidate">Кандидат для найма.</param>
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'public' ---
    /*public*/ void Hire(Candidate candidate) // Line 407 - Removed 'public'
    {
         if (candidate == null) {
              Debug.LogError("[HiringSystemUI] Попытка найма null кандидата!");
              return;
         }
         if (HiringManager.Instance == null) {
              Debug.LogError("[HiringSystemUI] HiringManager не найден! Найм невозможен.");
              return;
         }
          Debug.Log($"[HiringSystemUI] Попытка найма кандидата: {candidate.Name}");


        // Пытаемся нанять кандидата
        bool success = HiringManager.Instance.HireCandidate(candidate);

        if (success)
        {
            Debug.Log($"[HiringSystemUI] Успешно нанят: {candidate.Name}");

            // Удаляем "листок" только что нанятого кандидата с доски
            if (candidatePins.ContainsKey(candidate))
            {
                 GameObject pinToDestroy = candidatePins[candidate];
                  Debug.Log($" -> Уничтожение листка {pinToDestroy?.name}");
                 if (pinToDestroy != null) Destroy(pinToDestroy);
                candidatePins.Remove(candidate);
                 Debug.Log($" -> Запись удалена из candidatePins.");
            } else {
                 Debug.LogWarning($" -> Листок для нанятого кандидата {candidate.Name} не найден в candidatePins.");
            }

            // Закрываем детальное окно, если оно было открыто для этого кандидата
             if (currentlyViewedCandidate == candidate) {
                  Debug.Log(" -> Закрытие детального окна...");
                 CloseDetailedView();
             }


            // Обновляем отображение денег игрока (т.к. они были списаны)
            UpdatePlayerMoneyDisplay();

            // Обновляем список сотрудников на основной панели отдела кадров (если она есть)
             HiringPanelUI hiringPanel = FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include);
             if (hiringPanel != null) {
                 Debug.Log(" -> Обновление HiringPanelUI...");
                 hiringPanel.RefreshTeamList();
             } else {
                  Debug.LogWarning(" -> HiringPanelUI не найден для обновления.");
             }
        }
        else // Если найм не удался
        {
            Debug.LogWarning($"[HiringSystemUI] Не удалось нанять {candidate.Name}. Возможно, не хватило денег или нет места.");
            // Обновляем состояние кнопки найма на случай, если проблема была в деньгах и панель осталась открыта
             if (currentlyViewedCandidate == candidate && hireButton != null && PlayerWallet.Instance != null) {
                 hireButton.interactable = PlayerWallet.Instance.GetCurrentMoney() >= candidate.HiringCost;
             }
        }
    }

    /// <summary>
    /// Обновляет текстовое поле с текущим количеством денег игрока.
    /// </summary>
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'private' ---
    /*private*/ void UpdatePlayerMoneyDisplay() // Line 471 - Removed 'private'
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
                // Debug.LogError("[HiringSystemUI] PlayerWallet не найден! Не могу отобразить счет."); // Можно раскомментировать для отладки
            }
        }
         // else: Если playerMoneyText не назначен в инспекторе, ничего не делаем
         // else Debug.LogWarning("Player Money Text не назначен в HiringSystemUI!");
    }


    /// <summary>
    /// Вспомогательный метод для получения русского названия роли.
    /// </summary>
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'private' ---
    /*private*/ string GetRoleNameInRussian(StaffController.Role role) // Line 493 - Removed 'private'
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

    // Метод для удаления "устаревших" пинов, если список кандидатов в менеджере изменился
    // --- ОШИБКА БЫЛА ЗДЕСЬ: убрали 'private' ---
    /*private*/ void RemoveStalePins() // Line 510 - Removed 'private'
    {
        if (HiringManager.Instance == null) return;

        List<Candidate> currentCandidates = HiringManager.Instance.AvailableCandidates;
        List<Candidate> pinsToRemove = new List<Candidate>();

        // Check pins against the current candidate list
        foreach(var pair in candidatePins) {
            // If the pin's candidate is no longer in the manager's list OR the pin's GameObject was destroyed
            if (!currentCandidates.Contains(pair.Key) || pair.Value == null) {
                pinsToRemove.Add(pair.Key);
                 if (pair.Value != null) Destroy(pair.Value); // Destroy the pin GameObject if it still exists
            }
        }

        // Remove the stale entries from the dictionary
        foreach(var candidateToRemove in pinsToRemove) {
            candidatePins.Remove(candidateToRemove);
        }
         if (pinsToRemove.Count > 0) Debug.Log($"[HiringSystemUI] Удалено {pinsToRemove.Count} устаревших 'листков'.");
    }

// --- ОШИБКА БЫЛА ЗДЕСЬ: Добавили недостающую скобку ---
} // <<< Line 538 - Added missing closing brace for the class
// ---