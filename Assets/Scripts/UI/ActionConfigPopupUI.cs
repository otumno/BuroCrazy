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
    [SerializeField] private ActionDatabase actionDatabase; // Оставляем на случай, если он нужен для чего-то еще
    
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
        this.currentRank = staff.currentRank; 
        this.tempActiveActions = new List<StaffAction>(staff.activeActions);
        gameObject.SetActive(true);

        PopulateRoleDropdown();
        PopulateShiftDropdown();
        PopulateWorkstationDropdown(currentStaff.currentRole);
        PopulateActionLists();
    }

    // ----- ГЛАВНОЕ ИЗМЕНЕНИЕ ЗДЕСЬ -----
    private void PopulateActionLists()
    {
        foreach (Transform child in availableActionsContent) { Destroy(child.gameObject); }
        foreach (Transform child in activeActionsContent) { Destroy(child.gameObject); }

        if (currentStaff == null || currentRank == null || ExperienceManager.Instance == null) 
        {
            UpdateUIState();
            return;
        }
        
        // 1. Берем роль, выбранную в DROPDOWN
        string selectedRoleName = roleDropdown.options[roleDropdown.value].text;
        StaffController.Role roleToShow = GetRoleEnumFromRussian(selectedRoleName);

        // 2. Берем ТЕКУЩИЙ ранг сотрудника
        int currentLevel = currentStaff.currentRank.rankLevel;
        StaffController.Role staffsActualRole = currentStaff.currentRole;

        List<StaffAction> allAvailableActions = new List<StaffAction>();

        // 3. Если роль в дропдауне - это текущая роль сотрудника, показываем его реальный прогресс
        if (roleToShow == staffsActualRole)
        {
            allAvailableActions = ExperienceManager.Instance.rankDatabase
                .Where(rank => rank.associatedRole == staffsActualRole && // Совпадает ветка роли
                               rank.rankLevel <= currentLevel)     // Ранг ниже или равен текущему
                .SelectMany(rank => rank.unlockedActions) // Берем списки действий из ВСЕХ этих рангов
                .Where(action => action != null && action.category == ActionCategory.Tactic) // Только тактические
                .Distinct() // Убираем дубликаты
                .ToList();
        }
        // 4. Если игрок выбрал в дропдауне ДРУГУЮ роль (для просмотра)
        else
        {
            // Находим *базовый* ранг (уровень 0) для этой НОВОЙ роли
            RankData baseRankForNewRole = ExperienceManager.Instance.rankDatabase
                .FirstOrDefault(rank => rank.associatedRole == roleToShow && rank.rankLevel == 0);
            
            if (baseRankForNewRole != null)
            {
                // Показываем только те действия, которые даются на 1-м уровне этой новой роли
                allAvailableActions = baseRankForNewRole.unlockedActions
                    .Where(action => action != null && action.category == ActionCategory.Tactic)
                    .Distinct()
                    .ToList();
            }
            // Если базового ранга нет, 'allAvailableActions' останется пустым, что корректно
        }
        
        // 5. Очищаем временный список от действий, которые больше не доступны (из-за смены роли)
        tempActiveActions.RemoveAll(action => !allAvailableActions.Contains(action));

        // 6. Размещаем иконки в правильные списки
        foreach (var action in allAvailableActions)
        {
            if(tempActiveActions.Contains(action))
            {
                 InstantiateActionIcon(action, activeActionsContent);
            }
            else
            {
                InstantiateActionIcon(action, availableActionsContent);
            }
        }
        
        UpdateUIState();
    }
    // ----- КОНЕЦ ИЗМЕНЕНИЙ -----

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
        
        var actionNames = tempActiveActions.Select(a => a.actionType.ToString());
        Debug.Log($"<color=cyan>[ActionConfigPopupUI.OnSave]</color> Сохраняем для '{currentStaff.characterName}' следующие тактические действия: [{string.Join(", ", actionNames)}]");

        HiringManager.Instance.AssignNewRole_Immediate(currentStaff, newRole, new List<StaffAction>(tempActiveActions));
        
        gameObject.SetActive(false);
        FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
        HiringManager.Instance?.CheckAllStaffShiftsImmediately();
    }
    
    public bool CanAddAction()
    {
        if (currentRank == null) return false;
        return tempActiveActions.Count < currentRank.maxActions;
    }

    public void OnActionDropped(StaffAction action, ActionDropZone.ZoneType targetZoneType)
    {
        if (targetZoneType == ActionDropZone.ZoneType.Active)
        {
            if (!tempActiveActions.Contains(action) && CanAddAction())
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
        PopulateActionLists();
    }
    
    private void UpdateUIState()
    {
        if (currentRank != null && activeActionsHeaderText != null)
        {
            activeActionsHeaderText.text = $"Тактические действия ({tempActiveActions.Count}/{currentRank.maxActions})";
        }
        saveButton.interactable = true;
    }
    
    private void OnCancel()
    {
        gameObject.SetActive(false);
    }
    
    private void OnRoleSelectionChanged()
    {
        tempActiveActions.Clear();
        PopulateActionLists();
        
        string selectedRoleName = roleDropdown.options[roleDropdown.value].text;
        StaffController.Role newRole = GetRoleEnumFromRussian(selectedRoleName);
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
        roleDropdown.interactable = currentStaff.currentRank != null;
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

    private void UpdateShiftInfoText()
    {
        if (currentStaff == null || currentStaff.currentRank == null) return;
        
        int duration = currentRank.workPeriodsCount;
        List<string> allPeriods = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).ToList();
        int startIndex = shiftDropdown.value;
        
        string startPeriodName = allPeriods[startIndex];
        int endIndex = (startIndex + duration - 1) % allPeriods.Count;
        string endPeriodName = allPeriods[endIndex];
        
        shiftDurationText.text = $"Периодов: {duration}. С {startPeriodName} по {endPeriodName}";
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
        if (deskId == 1 || deskId == 2) return StaffController.Role.Clerk;
        if (deskId == 3) return StaffController.Role.Archivist;
        if (deskId == -1 || deskId == 4) return StaffController.Role.Cashier;
        return StaffController.Role.Unassigned;
    }
}