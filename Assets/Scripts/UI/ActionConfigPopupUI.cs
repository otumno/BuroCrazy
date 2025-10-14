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
    [SerializeField] private TMP_Dropdown roleDropdown;
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
    [SerializeField] private ActionDatabase actionDatabase;
    
    private StaffController currentStaff;
    private RankData currentRank;
    private List<StaffAction> tempActiveActions = new List<StaffAction>();

    private void Awake()
    {
        saveButton.onClick.AddListener(OnSave);
        cancelButton.onClick.AddListener(OnCancel);
        shiftDropdown.onValueChanged.AddListener(delegate { UpdateShiftInfoText(); });
        roleDropdown.onValueChanged.AddListener(delegate { OnRoleSelectionChanged(); });
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
        PopulateShiftDropdown();
        PopulateWorkstationDropdown(currentStaff.currentRole);
        PopulateActionLists();
    }

    private void PopulateActionLists()
    {
        foreach (Transform child in availableActionsContent) { Destroy(child.gameObject); }
        foreach (Transform child in activeActionsContent) { Destroy(child.gameObject); }

        if (actionDatabase == null || currentStaff == null) return;
        string selectedRoleName = roleDropdown.options[roleDropdown.value].text;
        StaffController.Role roleToShow = GetRoleEnumFromRussian(selectedRoleName);
        
        // ----- ИЗМЕНЕНИЕ ЗДЕСЬ -----
        // Теперь мы выбираем только действия с категорией Tactic
        List<StaffAction> allAvailableActions = actionDatabase.allActions
            .Where(action => action.category == ActionCategory.Tactic && // <--- ВОТ НОВЫЙ ФИЛЬТР
                             action.minRankRequired <= currentStaff.rank && 
                             action.applicableRoles.Contains(roleToShow))
            .ToList();
        
        foreach (var activeAction in tempActiveActions)
        {
            if (allAvailableActions.Contains(activeAction))
            {
                InstantiateActionIcon(activeAction, activeActionsContent);
            }
        }

        foreach (var availableAction in allAvailableActions)
        {
            if (!tempActiveActions.Contains(availableAction))
            {
                InstantiateActionIcon(availableAction, availableActionsContent);
            }
        }
        
        UpdateUIState();
    }

    // (Остальная часть скрипта остается без изменений)
    #region Unchanged Methods
    public bool CanAddAction()
    {
        if (currentRank == null) return false;
        return tempActiveActions.Count < currentRank.maxActions;
    }

    public void OnActionDropped(StaffAction action, ActionDropZone.ZoneType targetZoneType)
    {
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
        currentStaff.workPeriods.Clear();
        List<string> allPeriods = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).ToList();
        int startIndex = shiftDropdown.value;
        int duration = (currentRank != null) ? currentRank.workPeriodsCount : 3;
        for (int i = 0; i < duration; i++)
        {
            int periodIndex = (startIndex + i) % allPeriods.Count;
            currentStaff.workPeriods.Add(allPeriods[periodIndex]);
        }
        
        string selectedRoleName = roleDropdown.options[roleDropdown.value].text;
        StaffController.Role newRole = GetRoleEnumFromRussian(selectedRoleName);

        if (workstationDropdown.gameObject.activeSelf && workstationDropdown.value > 0)
        {
            string selectedOptionText = workstationDropdown.options[workstationDropdown.value].text;
            string friendlyNameFromDropdown = selectedOptionText.Split('(')[0].Trim();

            var selectedPoint = ScenePointsRegistry.Instance.allServicePoints
                .FirstOrDefault(p => GetWorkstationFriendlyName(p) == friendlyNameFromDropdown);

            if (selectedPoint != null)
            {
                AssignmentManager.Instance.AssignStaffToWorkstation(currentStaff, selectedPoint);
            }
            else
            {
                AssignmentManager.Instance.UnassignStaff(currentStaff);
            }
        }
        else
        {
            AssignmentManager.Instance.UnassignStaff(currentStaff);
        }

        List<StaffAction> newActionsToAssign = new List<StaffAction>();
    foreach (Transform iconTransform in activeActionsContent)
    {
        ActionIconUI iconUI = iconTransform.GetComponent<ActionIconUI>();
        if (iconUI != null) { newActionsToAssign.Add(iconUI.actionData); }
    }

    // ВЫВОДИМ В КОНСОЛЬ РЕЗУЛЬТАТ СБОРА ДЕЙСТВИЙ
    var actionNames = newActionsToAssign.Select(a => a.actionType.ToString());
    Debug.Log($"<color=cyan>[ActionConfigPopupUI.OnSave]</color> Сохраняем для '{currentStaff.characterName}' следующие тактические действия: [{string.Join(", ", actionNames)}]");
    
    // Проверяем, есть ли среди них искомое действие
    bool hasBookkeeping = newActionsToAssign.Any(a => a.actionType == ActionType.DoBookkeeping);
    Debug.Log($"<color={(hasBookkeeping ? "green" : "red")}>[ActionConfigPopupUI.OnSave]</color> Наличие действия 'DoBookkeeping' в списке: {hasBookkeeping}");

    // ----- КОНЕЦ ИЗМЕНЕНИЙ -----
        
        HiringManager.Instance.AssignNewRole_Immediate(currentStaff, newRole, newActionsToAssign);
        gameObject.SetActive(false);
        FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
        HiringManager.Instance?.CheckAllStaffShiftsImmediately();
    }

    private void OnCancel()
    {
        gameObject.SetActive(false);
    }
    
    private void OnRoleSelectionChanged()
    {
        string selectedRoleName = roleDropdown.options[roleDropdown.value].text;
        StaffController.Role newRole = GetRoleEnumFromRussian(selectedRoleName);

        tempActiveActions.Clear();
        PopulateActionLists();
        PopulateWorkstationDropdown(newRole);
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
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.mainCalendar == null) return;

        List<string> periodNames = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).ToList();
        shiftDropdown.AddOptions(periodNames);
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

    private void PopulateWorkstationDropdown(StaffController.Role role)
    {
        workstationDropdown.ClearOptions();
        List<string> options = new List<string> { "Не назначено" };

        var allPoints = ScenePointsRegistry.Instance.allServicePoints;
        var suitablePoints = allPoints.Where(p => GetRoleForDeskId(p.deskId) == role).ToList();

        if (suitablePoints.Any())
        {
            workstationDropdown.gameObject.SetActive(true);

            foreach (var point in suitablePoints)
            {
                var assignedStaff = AssignmentManager.Instance.GetAssignedStaff(point);
                string optionText = GetWorkstationFriendlyName(point);

                if (assignedStaff != null && assignedStaff != currentStaff)
                {
                    string periods = string.Join(", ", assignedStaff.workPeriods);
                    optionText += $" (Занят: {assignedStaff.characterName} - {periods})";
                }
                options.Add(optionText);
            }
            
            workstationDropdown.AddOptions(options);

            if (currentStaff.assignedWorkstation != null && suitablePoints.Contains(currentStaff.assignedWorkstation))
            {
                int index = suitablePoints.FindIndex(p => p == currentStaff.assignedWorkstation) + 1;
                if (index > 0)
                {
                    workstationDropdown.SetValueWithoutNotify(index);
                }
            }
            else
            {
                workstationDropdown.SetValueWithoutNotify(0);
            }
        }
        else
        {
            workstationDropdown.gameObject.SetActive(false);
        }
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
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.mainCalendar == null || ExperienceManager.Instance == null || currentStaff == null) return;
        RankData currentRankData = ExperienceManager.Instance.GetRankByXP(currentStaff.experiencePoints);
        int duration = (currentRankData != null) ? currentRankData.workPeriodsCount : 3;

        List<string> allPeriods = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).ToList();
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

    private string GetWorkstationFriendlyName(ServicePoint point)
    {
        if (point == null) return "Неизвестно";
        if (!string.IsNullOrEmpty(point.friendlyName)) return point.friendlyName;
        return point.name;
    }
    
    private StaffController.Role GetRoleForDeskId(int deskId)
    {
        if (deskId == 0) return StaffController.Role.Registrar;
        if (deskId == -1) return StaffController.Role.Cashier;
        if (deskId == 1 || deskId == 2) return StaffController.Role.Clerk;
        if (deskId == 3) return StaffController.Role.Archivist;
        if (deskId == 4) return StaffController.Role.Cashier;
        return StaffController.Role.Unassigned;
    }
    #endregion
}