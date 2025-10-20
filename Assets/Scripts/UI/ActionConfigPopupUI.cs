// Файл: Assets/Scripts/UI/ActionConfigPopupUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ActionConfigPopupUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private TextMeshProUGUI currentRoleText;
    [SerializeField] private TMP_Dropdown shiftDropdown;
    [SerializeField] private TextMeshProUGUI shiftDurationText;
    [SerializeField] private Transform availableActionsContent;
    [SerializeField] private Transform activeActionsContent;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Dropdown workstationDropdown;
    [SerializeField] private TextMeshProUGUI activeActionsHeaderText;
    [SerializeField] private ActionDropZone availableActionsDropZone;
    [SerializeField] private ActionDropZone activeActionsDropZone;

    [Header("Префабы и данные")]
    [SerializeField] private GameObject actionIconPrefab;
    [SerializeField] private ActionDatabase actionDatabase; // Оставляем на случай, если он нужен для чего-то еще

    private StaffController currentStaff;
    private RankData currentRank;
    private List<StaffAction> tempActiveActions = new List<StaffAction>();

    private void Awake()
    {
        // --- ИЗМЕНЕНИЕ НАЧАЛО (Вызываем OnSave через StartCoroutine) ---
        saveButton.onClick.AddListener(() => StartCoroutine(OnSave()));
        // --- ИЗМЕНЕНИЕ КОНЕЦ ---
        cancelButton.onClick.AddListener(OnCancel);
        shiftDropdown.onValueChanged.AddListener(delegate { UpdateShiftInfoText(); });
        //roleDropdown.onValueChanged.AddListener(delegate { OnRoleSelectionChanged(); });
        if (availableActionsDropZone != null) { availableActionsDropZone.popupController = this; availableActionsDropZone.type = ActionDropZone.ZoneType.Available; }
        if (activeActionsDropZone != null) { activeActionsDropZone.popupController = this; activeActionsDropZone.type = ActionDropZone.ZoneType.Active; }
    }

    public void OpenForStaff(StaffController staff)
    {
        this.currentStaff = staff;
        this.currentRank = staff.currentRank;
        // Клонируем список, чтобы изменения были временными до сохранения
        this.tempActiveActions = new List<StaffAction>(staff.activeActions ?? new List<StaffAction>());
        gameObject.SetActive(true);

        //PopulateRoleDropdown();
		
		if (currentRoleText != null)
        {
             currentRoleText.text = GetRoleNameInRussian(staff.currentRole);
        }
         else { Debug.LogError("CurrentRoleText не назначен в инспекторе ActionConfigPopupUI!"); }
		
        PopulateShiftDropdown();
        PopulateWorkstationDropdown(currentStaff.currentRole);
        PopulateActionLists(); // Первичное заполнение списков действий
    }

    private void PopulateActionLists()
    {
        // Очищаем контейнеры перед заполнением
        foreach (Transform child in availableActionsContent) { Destroy(child.gameObject); }
        foreach (Transform child in activeActionsContent) { Destroy(child.gameObject); }

        if (currentStaff == null || ExperienceManager.Instance == null || ExperienceManager.Instance.rankDatabase == null)
        {
            Debug.LogError("Не удалось обновить списки действий: Staff, ExperienceManager или RankDatabase не найдены.");
            UpdateUIState(); // Обновляем UI, даже если есть ошибка (покажет 0/0)
            return;
        }

        // --- Используем реальную роль сотрудника ---
        StaffController.Role roleToShow = currentStaff.currentRole; // <<<< ИСПОЛЬЗУЕМ ЭТО
        // ---

        RankData staffCurrentRank = currentStaff.currentRank;
        int currentLevel = staffCurrentRank != null ? staffCurrentRank.rankLevel : -1;

        List<StaffAction> allPossibleActionsForSelectedRole = new List<StaffAction>();

        // --- Собираем ВСЕ тактические действия, доступные для ТЕКУЩЕЙ роли ДО текущего уровня сотрудника ---
        allPossibleActionsForSelectedRole = ExperienceManager.Instance.rankDatabase
            .Where(rank => rank != null && rank.associatedRole == roleToShow && rank.rankLevel <= currentLevel)
            .SelectMany(rank => rank.unlockedActions ?? new List<StaffAction>())
            .Where(action => action != null && action.category == ActionCategory.Tactic)
            .Distinct()
            .ToList();
        // ---

        // Очищаем временный список от действий, которые больше НЕ доступны
        tempActiveActions.RemoveAll(action => !allPossibleActionsForSelectedRole.Contains(action));

        // Размещаем иконки в правильные списки
        foreach (var action in allPossibleActionsForSelectedRole)
        {
            if(tempActiveActions.Contains(action)) { InstantiateActionIcon(action, activeActionsContent); }
            else { InstantiateActionIcon(action, availableActionsContent); }
        }

        UpdateUIState();
    }


    private IEnumerator OnSave()
    {
        // Сохраняем периоды работы
        currentStaff.workPeriods.Clear();
        if (ClientSpawner.Instance != null && ClientSpawner.Instance.mainCalendar != null && ClientSpawner.Instance.mainCalendar.periodSettings != null)
        {
            List<string> allPeriods = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).Where(n => !string.IsNullOrEmpty(n)).ToList();
            if (allPeriods.Any())
            {
                int startIndex = shiftDropdown.value;
                int duration = (currentStaff.currentRank != null) ? currentStaff.currentRank.workPeriodsCount : 3;
                for (int i = 0; i < duration; i++)
                {
                    int periodIndex = (startIndex + i) % allPeriods.Count;
                    currentStaff.workPeriods.Add(allPeriods[periodIndex]);
                }
            } else { Debug.LogError("Не удалось сохранить расписание: список периодов в календаре пуст или некорректен."); }
        } else { Debug.LogError("Не удалось сохранить расписание: ClientSpawner или его календарь не найдены."); }

        // --- Используем текущую роль ---
        StaffController.Role currentRole = currentStaff.currentRole;
        // ---

        // Назначаем рабочее место
        if (AssignmentManager.Instance != null && ScenePointsRegistry.Instance != null)
        {
            if (workstationDropdown.gameObject.activeSelf && workstationDropdown.value > 0)
            {
                string selectedOptionText = workstationDropdown.options[workstationDropdown.value].text;
                string friendlyNameFromDropdown = selectedOptionText.Split('(')[0].Trim();
                var selectedPoint = ScenePointsRegistry.Instance.allServicePoints?
                    .FirstOrDefault(p => p != null && GetWorkstationFriendlyName(p) == friendlyNameFromDropdown);
                if (selectedPoint != null) { AssignmentManager.Instance.AssignStaffToWorkstation(currentStaff, selectedPoint); }
                else { AssignmentManager.Instance.UnassignStaff(currentStaff); }
            }
            else { AssignmentManager.Instance.UnassignStaff(currentStaff); }
        } else { Debug.LogError("Не удалось назначить рабочее место: AssignmentManager или ScenePointsRegistry не найдены."); }

        // Логируем сохраняемые действия
        var actionNames = tempActiveActions.Select(a => a.actionType.ToString());
        Debug.Log($"<color=cyan>[ActionConfigPopupUI.OnSave]</color> Сохраняем для '{currentStaff.characterName}' следующие тактические действия: [{string.Join(", ", actionNames)}]");

        // --- Вызываем AssignNewRole_Immediate с ТЕКУЩЕЙ ролью ---
        Coroutine rebuildCoroutine = null;
        if (HiringManager.Instance != null)
        {
             rebuildCoroutine = HiringManager.Instance.AssignNewRole_Immediate(currentStaff, currentRole, new List<StaffAction>(tempActiveActions));
        } else { Debug.LogError("Не удалось сохранить роль/действия: HiringManager не найден."); }

        // Ожидание завершения (если нужно)
         if (rebuildCoroutine != null)
         {
             Debug.Log($"[ActionConfigPopupUI.OnSave] Ожидание завершения RebuildControllerComponent для {currentStaff?.characterName}...");
             yield return rebuildCoroutine;
              if (currentStaff != null)
              {
                  currentStaff = currentStaff.gameObject.GetComponent<StaffController>(); // Обновляем ссылку
                  if (currentStaff == null) { Debug.LogError("Не удалось найти StaffController после пересборки!"); }
                  else { Debug.Log($"[ActionConfigPopupUI.OnSave] RebuildControllerComponent для {currentStaff.characterName} завершен."); }
              } else { Debug.LogError("Ссылка на currentStaff потеряна после пересборки!"); }
         }
         else { yield return null; }

        gameObject.SetActive(false); // Прячем панель

        // Обновляем HiringPanelUI
        HiringPanelUI hiringPanel = FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include);
        if (hiringPanel != null)
        {
            hiringPanel.RefreshTeamList();
            Debug.Log($"[ActionConfigPopupUI.OnSave] Обновление HiringPanelUI вызвано.");
        } else { Debug.LogWarning($"[ActionConfigPopupUI.OnSave] HiringPanelUI не найден для обновления."); }

        // Проверяем смены немедленно
        HiringManager.Instance?.CheckAllStaffShiftsImmediately();
    }

    public bool CanAddAction()
    {
        // Используем РЕАЛЬНЫЙ ранг сотрудника для проверки лимита
        if (currentStaff == null || currentStaff.currentRank == null) return false;
        return tempActiveActions.Count < currentStaff.currentRank.maxActions;
    }


    public void OnActionDropped(StaffAction action, ActionDropZone.ZoneType targetZoneType)
    {
        // Перемещаем действие между временными списками
        if (targetZoneType == ActionDropZone.ZoneType.Active)
        {
            // Добавляем в активные, только если есть место и его там еще нет
            if (!tempActiveActions.Contains(action) && CanAddAction())
            {
                tempActiveActions.Add(action);
            }
        }
        else // targetZoneType == ActionDropZone.ZoneType.Available
        {
            // Удаляем из активных, если он там был
            if (tempActiveActions.Contains(action))
            {
                tempActiveActions.Remove(action);
            }
        }
        // Перерисовываем списки на основе обновленного tempActiveActions
        PopulateActionLists();
    }


    private void UpdateUIState()
    {
        // Используем РЕАЛЬНЫЙ ранг сотрудника для отображения лимита
        RankData rankForLimit = currentStaff?.currentRank;

        // --- ИЗМЕНЕНИЕ НАЧАЛО (Добавлена проверка на null) ---
        if (activeActionsHeaderText != null)
        {
            if (rankForLimit != null)
            {
                activeActionsHeaderText.text = $"Тактические действия ({tempActiveActions.Count}/{rankForLimit.maxActions})";
            }
            else
            {
                // Если ранг не определен, показываем только текущее количество
                activeActionsHeaderText.text = $"Тактические действия ({tempActiveActions.Count}/?)";
                 Debug.LogWarning("UpdateUIState: currentRank is null!");
            }
        } else {
             Debug.LogWarning("UpdateUIState: activeActionsHeaderText is null!");
        }
        // --- ИЗМЕНЕНИЕ КОНЕЦ ---
        saveButton.interactable = true; // Кнопка Save всегда активна, если панель открыта
    }


    private void OnCancel()
    {
        gameObject.SetActive(false);
        // При отмене не нужно обновлять HiringPanelUI, так как изменения не применяются
    }


    private void PopulateShiftDropdown()
    {
        shiftDropdown.ClearOptions();
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.mainCalendar == null || ClientSpawner.Instance.mainCalendar.periodSettings == null)
        {
            Debug.LogError("Невозможно заполнить список смен: ClientSpawner или календарь не найдены.");
            return;
        }


        // Получаем имена всех периодов из календаря
        List<string> periodNames = ClientSpawner.Instance.mainCalendar.periodSettings
                                        .Select(p => p.periodName)
                                        .Where(n => !string.IsNullOrEmpty(n)) // Исключаем пустые имена
                                        .ToList();

        if (!periodNames.Any())
        {
             Debug.LogError("Невозможно заполнить список смен: в календаре нет периодов с именами.");
             return;
        }

        shiftDropdown.AddOptions(periodNames);

        // Устанавливаем текущий первый рабочий период сотрудника как выбранный
        int currentIndex = 0; // По умолчанию - первый период
        if (currentStaff != null && currentStaff.workPeriods.Any()) // Проверка на null и наличие периодов
        {
            string firstWorkPeriod = currentStaff.workPeriods.First();
            int foundIndex = periodNames.IndexOf(firstWorkPeriod);
            if (foundIndex != -1) { // Если период найден в общем списке
                 currentIndex = foundIndex;
            }
        }
        shiftDropdown.SetValueWithoutNotify(currentIndex); // Устанавливаем значение без вызова события

        UpdateShiftInfoText(); // Обновляем текст с длительностью
    }


    private void UpdateShiftInfoText()
    {
        // --- ИЗМЕНЕНИЕ НАЧАЛО (Добавлена проверка на null) ---
        if (shiftDurationText != null)
        {
            // Используем РЕАЛЬНЫЙ ранг сотрудника
            RankData staffRank = currentStaff?.currentRank;

            if (staffRank == null || ClientSpawner.Instance?.mainCalendar?.periodSettings == null) // Проверка
            {
                 shiftDurationText.text = "Периодов: N/A";
                 // Debug.LogWarning("UpdateShiftInfoText: currentStaff, currentRank или календарь null!");
                 return; // Выходим, если данных нет
            }
            // --- ИЗМЕНЕНИЕ КОНЕЦ ---

            int duration = staffRank.workPeriodsCount;
            List<string> allPeriods = ClientSpawner.Instance.mainCalendar.periodSettings
                                        .Select(p => p.periodName)
                                        .Where(n => !string.IsNullOrEmpty(n))
                                        .ToList();

            if (!allPeriods.Any()) { // Проверка на пустой список периодов
                 shiftDurationText.text = "Периодов: N/A (ошибка)";
                 return;
            }

            int startIndex = shiftDropdown.value; // Индекс выбранного начального периода

            // Проверка, что startIndex в пределах списка
            if (startIndex < 0 || startIndex >= allPeriods.Count) {
                 startIndex = 0; // Сбрасываем на первый, если индекс некорректен
            }


            string startPeriodName = allPeriods[startIndex];
            // Вычисляем индекс последнего периода с учетом зацикливания
            int endIndex = (startIndex + duration - 1 + allPeriods.Count) % allPeriods.Count; // Добавлено + allPeriods.Count для корректной работы с отрицательными остатками
            string endPeriodName = allPeriods[endIndex];

            shiftDurationText.text = $"Периодов: {duration}. С {startPeriodName} по {endPeriodName}";
        } else {
             Debug.LogWarning("UpdateShiftInfoText: shiftDurationText is null!");
        }
    }


    private void PopulateWorkstationDropdown(StaffController.Role role)
    {
        workstationDropdown.ClearOptions();
        List<string> options = new List<string> { "Не назначено" }; // Опция по умолчанию

        // Проверяем наличие менеджеров
        if (ScenePointsRegistry.Instance == null || ScenePointsRegistry.Instance.allServicePoints == null || AssignmentManager.Instance == null)
        {
            Debug.LogError("Невозможно заполнить список рабочих мест: ScenePointsRegistry или AssignmentManager не найдены.");
            workstationDropdown.gameObject.SetActive(false); // Прячем дропдаун, если нет данных
            return;
        }

        var allPoints = ScenePointsRegistry.Instance.allServicePoints;
        // Находим все точки, подходящие для ВЫБРАННОЙ РОЛИ
        var suitablePoints = allPoints.Where(p => p != null && GetRoleForDeskId(p.deskId) == role).ToList();

        if (suitablePoints.Any())
        {
            workstationDropdown.gameObject.SetActive(true); // Показываем дропдаун
            foreach (var point in suitablePoints)
            {
                var assignedStaff = AssignmentManager.Instance.GetAssignedStaff(point);
                string optionText = GetWorkstationFriendlyName(point); // Получаем имя точки
                // Если точка занята ДРУГИМ сотрудником, добавляем информацию об этом
                if (assignedStaff != null && assignedStaff != currentStaff)
                {
                    // Собираем строку с периодами работы занявшего сотрудника
                    string periods = (assignedStaff.workPeriods != null && assignedStaff.workPeriods.Any())
                                     ? string.Join(", ", assignedStaff.workPeriods)
                                     : "нет";
                    optionText += $" (Занят: {assignedStaff.characterName} - {periods})";
                }
                options.Add(optionText); // Добавляем опцию в список
            }

            workstationDropdown.AddOptions(options); // Заполняем дропдаун

            // Устанавливаем текущее назначенное место сотрудника как выбранное
            int currentWorkstationIndex = 0; // По умолчанию "Не назначено"
            if (currentStaff != null && currentStaff.assignedWorkstation != null && suitablePoints.Contains(currentStaff.assignedWorkstation))
            {
                // Находим индекс текущего рабочего места в списке подходящих и добавляем 1 (т.к. "Не назначено" на 0)
                int foundIndex = suitablePoints.FindIndex(p => p == currentStaff.assignedWorkstation);
                if (foundIndex != -1) {
                    currentWorkstationIndex = foundIndex + 1;
                }
            }
             workstationDropdown.SetValueWithoutNotify(currentWorkstationIndex); // Устанавливаем без вызова события

        }
        else
        {
            // Если для этой роли нет подходящих рабочих мест, прячем дропдаун
            workstationDropdown.gameObject.SetActive(false);
        }
    }


    private void InstantiateActionIcon(StaffAction action, Transform parent)
    {
        if (actionIconPrefab == null || action == null) return; // Проверка на null
        GameObject iconGO = Instantiate(actionIconPrefab, parent);
        ActionIconUI iconUI = iconGO.GetComponent<ActionIconUI>();
        if (iconUI != null)
        {
            iconUI.Setup(action);
        } else {
             Debug.LogError($"Префаб ActionIcon не содержит скрипт ActionIconUI!", actionIconPrefab);
        }
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

    private StaffController.Role GetRoleEnumFromRussian(string russianName)
    {
        switch (russianName)
        {
            case "Стажёр": return StaffController.Role.Intern;
            case "Клерк": return StaffController.Role.Clerk;
            case "Регистратор": return StaffController.Role.Registrar;
            case "Кассир": return StaffController.Role.Cashier;
            case "Архивариус": return StaffController.Role.Archivist;
            case "Охранник": return StaffController.Role.Guard;
            case "Уборщик": return StaffController.Role.Janitor;
            default: return StaffController.Role.Unassigned;
        }
    }

    private string GetWorkstationFriendlyName(ServicePoint point)
    {
        if (point == null) return "Неизвестно";
        // Используем friendlyName если оно есть, иначе имя GameObject'а
        return !string.IsNullOrEmpty(point.friendlyName) ? point.friendlyName : point.name;
    }


    private StaffController.Role GetRoleForDeskId(int deskId)
    {
        // Эта функция должна соответствовать логике в HiringManager
        if (deskId == 0) return StaffController.Role.Registrar;
        if (deskId == 1 || deskId == 2) return StaffController.Role.Clerk;
        if (deskId == 3) return StaffController.Role.Archivist;
        if (deskId == -1 || deskId == 4) return StaffController.Role.Cashier; // ID кассы и бухгалтерии
        // Добавить другие ID по необходимости
        return StaffController.Role.Unassigned;
    }

}