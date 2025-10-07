// Файл: UI/ActionConfigPopupUI.cs --- АБСОЛЮТНО ПОЛНАЯ ВЕРСИЯ ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ActionConfigPopupUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private TMP_Dropdown roleDropdown;
    [SerializeField] private TMP_Dropdown shiftDropdown;
    [SerializeField] private TextMeshProUGUI shiftDurationText;
    [SerializeField] private Transform availableActionsContent;
    [SerializeField] private Transform activeActionsContent;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI activeActionsHeaderText;
    [SerializeField] private ActionDropZone availableActionsDropZone;
    [SerializeField] private ActionDropZone activeActionsDropZone;

    [Header("Префабы и данные")]
    [SerializeField] private GameObject actionIconPrefab;
    [SerializeField] private ActionDatabase actionDatabase;
    
    private StaffController currentStaff;
    private RankData currentRank;
    private List<StaffAction> tempActiveActions = new List<StaffAction>();

    private void Awake()
{
    // --- НОВАЯ "ЗАЩИТА ОТ ДУБЛИРОВАНИЯ" ---
    // Перед тем как добавить слушателя, мы удаляем ВСЕ предыдущие.
    // Это гарантирует, что у нас всегда будет только ОДНА подписка на событие.
    saveButton.onClick.RemoveAllListeners();
    saveButton.onClick.AddListener(OnSave);

    cancelButton.onClick.RemoveAllListeners();
    cancelButton.onClick.AddListener(OnCancel);

    shiftDropdown.onValueChanged.RemoveAllListeners();
    shiftDropdown.onValueChanged.AddListener(delegate { UpdateShiftInfoText(); });

    roleDropdown.onValueChanged.RemoveAllListeners();
    roleDropdown.onValueChanged.AddListener(delegate { OnRoleSelectionChanged(); });
    // --- КОНЕЦ ИЗМЕНЕНИЙ ---

        if (availableActionsDropZone != null) { availableActionsDropZone.popupController = this; availableActionsDropZone.type = ActionDropZone.ZoneType.Available; }
        if (activeActionsDropZone != null) { activeActionsDropZone.popupController = this; activeActionsDropZone.type = ActionDropZone.ZoneType.Active; }
    }

    public void OpenForStaff(StaffController staff)
    {
        this.currentStaff = staff;
        this.currentRank = ExperienceManager.Instance.GetRankByXP(staff.experiencePoints);
        this.tempActiveActions = new List<StaffAction>(staff.activeActions);
        gameObject.SetActive(true);

        PopulateRoleDropdown();
        PopulateShiftDropdown(); // <-- Метод теперь на своем месте
        PopulateActionLists();
    }


public bool CanAddAction()
{
    if (currentRank == null) return false;
    return tempActiveActions.Count < currentRank.maxActions;
}

    public void OnActionDropped(StaffAction action, ActionDropZone.ZoneType targetZoneType)
{
    // 1. Обновляем наш внутренний список-черновик
    if (targetZoneType == ActionDropZone.ZoneType.Active)
    {
        if (!tempActiveActions.Contains(action))
        {
            tempActiveActions.Add(action);
        }
    }
    else 
    {
        if (tempActiveActions.Contains(action))
        {
            tempActiveActions.Remove(action);
        }
    }
    
    // 2. Обновляем текст счетчика и состояние кнопки
    UpdateUIState();
}
    
    private void PopulateActionLists()
{
    // Очищаем старые иконки
    foreach (Transform child in availableActionsContent) { Destroy(child.gameObject); }
    foreach (Transform child in activeActionsContent) { Destroy(child.gameObject); }

    if (actionDatabase == null || currentStaff == null) return;

    // Определяем, для какой роли мы показываем действия
    string selectedRoleName = roleDropdown.options[roleDropdown.value].text;
    StaffController.Role roleToShow = GetRoleEnumFromRussian(selectedRoleName);
    
    // --- НАЧАЛО СТАНДАРТНОЙ ЛОГИКИ ФИЛЬТРАЦИИ ---

    // Получаем список всех теоретически доступных действий для выбранной роли, учитывая ранг
    List<StaffAction> allAvailableActions = actionDatabase.allActions
        .Where(action => action.minRankRequired <= currentStaff.rank &&
                         action.applicableRoles.Contains(roleToShow))
        .ToList();

    // --- КОНЕЦ СТАНДАРТНОЙ ЛОГИКИ ФИЛЬТРАЦИИ ---

    // Создаем иконки для уже активных действий
    foreach (var activeAction in tempActiveActions)
    {
        // Убедимся, что активное действие вообще доступно для этой роли
        if (allAvailableActions.Contains(activeAction))
        {
            InstantiateActionIcon(activeAction, activeActionsContent);
        }
    }

    // Создаем иконки для остальных доступных, но еще не активных действий
    foreach (var availableAction in allAvailableActions)
    {
        if (!tempActiveActions.Contains(availableAction))
        {
            InstantiateActionIcon(availableAction, availableActionsContent);
        }
    }
    
    UpdateUIState();
}
    
    private void UpdateUIState()
    {
        if (currentRank != null && activeActionsHeaderText != null)
        {
            activeActionsHeaderText.text = $"Активные действия ({tempActiveActions.Count}/{currentRank.maxActions})";
        }
        saveButton.interactable = tempActiveActions.Count > 0;
    }

    private void OnSave()
{
    // 1. Сохраняем новое расписание смен
    currentStaff.workPeriods.Clear();
    // --- ИСПРАВЛЕНИЕ: Берем периоды из календаря, а не из устаревшего массива ---
    List<string> allPeriods = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).ToList();
    int startIndex = shiftDropdown.value;

    RankData currentRankData = ExperienceManager.Instance.GetRankByXP(currentStaff.experiencePoints);
    int duration = (currentRankData != null) ? currentRankData.workPeriodsCount : 3;

    for (int i = 0; i < duration; i++)
    {
        int periodIndex = (startIndex + i) % allPeriods.Count;
        currentStaff.workPeriods.Add(allPeriods[periodIndex]);
    }
    Debug.Log($"Сохранено новое расписание для {currentStaff.characterName}: {string.Join(", ", currentStaff.workPeriods)}");

    // 2. Собираем НОВЫЙ список действий из UI
    List<StaffAction> newActionsToAssign = new List<StaffAction>();
    foreach (Transform iconTransform in activeActionsContent)
    {
        ActionIconUI iconUI = iconTransform.GetComponent<ActionIconUI>();
        if (iconUI != null)
        {
            newActionsToAssign.Add(iconUI.actionData);
        }
    }

    // 3. Определяем новую роль
    string selectedRoleName = roleDropdown.options[roleDropdown.value].text;
    StaffController.Role newRole = GetRoleEnumFromRussian(selectedRoleName);

    // 4. Вызываем синхронный метод смены роли
    HiringManager.Instance.AssignNewRole_Immediate(currentStaff, newRole, newActionsToAssign);

    // 5. Закрываем панель и обновляем список команды
    gameObject.SetActive(false);
    FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
}

    private void OnRoleSelectionChanged()
    {
        tempActiveActions.Clear();
        PopulateActionLists();
    }
    
    private void PopulateRoleDropdown()
    {
        roleDropdown.ClearOptions();
        
        List<StaffController.Role> allPossibleRoles = System.Enum.GetValues(typeof(StaffController.Role))
            .Cast<StaffController.Role>()
            .Where(role => role != StaffController.Role.Unassigned)
            .ToList();
        
        List<string> roleNames = allPossibleRoles.Select(role => GetRoleNameInRussian(role)).ToList();
        roleDropdown.AddOptions(roleNames);

        int currentIndex = allPossibleRoles.IndexOf(currentStaff.currentRole);
        if (currentIndex != -1)
        {
            roleDropdown.SetValueWithoutNotify(currentIndex);
        }

        roleDropdown.interactable = currentStaff.rank > 0;
    }

    private void PopulateShiftDropdown()
    {
        shiftDropdown.ClearOptions();
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.periods.Length == 0) return;

        List<string> periodNames = ClientSpawner.Instance.periods.Select(p => p.periodName).ToList();
        var optionDataList = new List<TMP_Dropdown.OptionData>();
        foreach (var name in periodNames)
        {
            optionDataList.Add(new TMP_Dropdown.OptionData(name));
        }
        shiftDropdown.options = optionDataList;

        if (currentStaff.workPeriods.Any())
        {
            int currentIndex = periodNames.IndexOf(currentStaff.workPeriods.First());
            shiftDropdown.value = Mathf.Max(0, currentIndex);
        }
        else
        {
            shiftDropdown.value = 0;
        }
        UpdateShiftInfoText();
    }

    private void OnCancel()
    {
        gameObject.SetActive(false);
    }
    
    private void InstantiateActionIcon(StaffAction action, Transform parent)
    {
        if (actionIconPrefab == null) return;
        GameObject iconGO = Instantiate(actionIconPrefab, parent);
        ActionIconUI iconUI = iconGO.GetComponent<ActionIconUI>();
        if (iconUI != null)
        {
            iconUI.Setup(action);
        }
    }

    private void UpdateShiftInfoText()
    {
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.periods.Length == 0 || ExperienceManager.Instance == null || currentStaff == null) return;

        RankData currentRankData = ExperienceManager.Instance.GetRankByXP(currentStaff.experiencePoints);
        int duration = 3; 

        if (currentRankData != null)
        {
            duration = currentRankData.workPeriodsCount;
        }

        List<string> allPeriods = ClientSpawner.Instance.periods.Select(p => p.periodName).ToList();
        int startIndex = shiftDropdown.value;
        
        string startPeriodName = allPeriods[startIndex];
        int endIndex = (startIndex + duration - 1) % allPeriods.Count;
        string endPeriodName = allPeriods[endIndex];
        
        shiftDurationText.text = $"Периодов: {duration}. С {startPeriodName} по {endPeriodName}";
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
}