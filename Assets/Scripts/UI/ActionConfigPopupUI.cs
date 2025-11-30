using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using Managers;

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

    [Header("Префабы")]
    [SerializeField] private GameObject actionIconPrefab;

    private StaffController currentStaff;
    private List<StaffAction> tempActiveActions = new List<StaffAction>();

    private void Awake()
    {
        saveButton.onClick.AddListener(() => StartCoroutine(OnSave()));
        cancelButton.onClick.AddListener(OnCancel);
        shiftDropdown.onValueChanged.AddListener(delegate { UpdateShiftInfoText(); });
        
        if (availableActionsDropZone != null) { availableActionsDropZone.popupController = this; availableActionsDropZone.type = ActionDropZone.ZoneType.Available; }
        if (activeActionsDropZone != null) { activeActionsDropZone.popupController = this; activeActionsDropZone.type = ActionDropZone.ZoneType.Active; }
    }

    public void OpenForStaff(StaffController staff)
    {
        currentStaff = staff;
        tempActiveActions = new List<StaffAction>(staff.activeActions ?? new List<StaffAction>());
        gameObject.SetActive(true);

        if (currentRoleText != null) currentRoleText.text = staff.currentRole.ToString();
        
        PopulateShiftDropdown();
        PopulateWorkstationDropdown(currentStaff.currentRole);
        PopulateActionLists();
    }

    private void PopulateShiftDropdown()
    {
        shiftDropdown.ClearOptions();
        
        // ИСПРАВЛЕНИЕ: Берем календарь из DayPeriodManager
        if (DayPeriodManager.Instance == null || DayPeriodManager.Instance.mainCalendarDay == null)
        {
            Debug.LogError("DayPeriodManager или календарь не найдены.");
            return;
        }

        var periods = DayPeriodManager.Instance.mainCalendarDay.periodSettings;
        var periodTypes = periods.Select(p => p.PeriodType).ToList();
        
        if (!periodTypes.Any()) return;

        shiftDropdown.AddOptions(periodTypes.Select(t => t.ToString()).ToList());

        // Находим текущую смену сотрудника
        int currentIndex = 0;
        for (int i = 0; i < periodTypes.Count; i++)
        {
            if (currentStaff.WorkShiftMask.HasFlag(periodTypes[i]))
            {
                currentIndex = i;
                break;
            }
        }
        shiftDropdown.SetValueWithoutNotify(currentIndex);
        UpdateShiftInfoText();
    }

    private void UpdateShiftInfoText()
    {
        if (shiftDurationText == null || DayPeriodManager.Instance?.mainCalendarDay == null) return;

        var periods = DayPeriodManager.Instance.mainCalendarDay.periodSettings.Select(p => p.PeriodType).ToList();
        if (!periods.Any()) return;

        int duration = (currentStaff.currentRank != null) ? currentStaff.currentRank.workPeriodsCount : 3;
        int startIndex = shiftDropdown.value;
        int endIndex = (startIndex + duration - 1) % periods.Count;

        shiftDurationText.text = $"Периодов: {duration}. С {periods[startIndex]} по {periods[endIndex]}";
    }

    private IEnumerator OnSave()
    {
        currentStaff.WorkShiftMask = 0;
        
        // ИСПРАВЛЕНИЕ: Берем календарь из DayPeriodManager
        if (DayPeriodManager.Instance != null && DayPeriodManager.Instance.mainCalendarDay != null)
        {
            var allPeriods = DayPeriodManager.Instance.mainCalendarDay.periodSettings.Select(p => p.PeriodType).ToList();
            int startIndex = shiftDropdown.value;
            int duration = (currentStaff.currentRank != null) ? currentStaff.currentRank.workPeriodsCount : 3;

            for (int i = 0; i < duration; i++)
            {
                int index = (startIndex + i) % allPeriods.Count;
                currentStaff.WorkShiftMask |= allPeriods[index];
            }
        }

        // Сохранение рабочего места
        if (AssignmentManager.Instance != null && workstationDropdown.value > 0)
        {
            string selectedName = workstationDropdown.options[workstationDropdown.value].text.Split('(')[0].Trim();
            var point = ScenePointsRegistry.Instance.allServicePoints.FirstOrDefault(p => (p.friendlyName == selectedName || p.name == selectedName));
            if (point != null) AssignmentManager.Instance.AssignStaffToWorkstation(currentStaff, point);
        }
        else if (AssignmentManager.Instance != null)
        {
            AssignmentManager.Instance.UnassignStaff(currentStaff);
        }

        // Сохранение действий через пересборку
        if (HiringManager.Instance != null)
        {
            yield return HiringManager.Instance.AssignNewRole_Immediate(currentStaff, currentStaff.currentRole, new List<StaffAction>(tempActiveActions));
        }

        gameObject.SetActive(false);
        HiringManager.Instance?.CheckAllStaffShiftsImmediately();
    }

    // ... Остальные методы (PopulateActionLists, CanAddAction, OnActionDropped, PopulateWorkstationDropdown, и т.д.) 
    // остаются без изменений логики, просто убедись, что они есть в файле. 
    // Я сократил ответ для удобства, но при копировании используй полную версию или аккуратно замени методы выше.
    
    // ВСТАВЬ СЮДА ОСТАЛЬНЫЕ МЕТОДЫ ИЗ ПРЕДЫДУЩЕГО ВАРИАНТА ФАЙЛА (PopulateActionLists и ниже)
    // ...
    
    private void PopulateActionLists()
    {
        foreach (Transform child in availableActionsContent) Destroy(child.gameObject);
        foreach (Transform child in activeActionsContent) Destroy(child.gameObject);

        if (currentStaff == null || ExperienceManager.Instance == null) return;

        var role = currentStaff.currentRole;
        int level = currentStaff.currentRank != null ? currentStaff.currentRank.rankLevel : -1;

        var possibleActions = ExperienceManager.Instance.rankDatabase
            .Where(r => r.associatedRole == role && r.rankLevel <= level)
            .SelectMany(r => r.unlockedActions)
            .Where(a => a.category == ActionCategory.Tactic)
            .Distinct()
            .ToList();

        tempActiveActions.RemoveAll(a => !possibleActions.Contains(a));

        foreach (var action in possibleActions)
        {
            Transform parent = tempActiveActions.Contains(action) ? activeActionsContent : availableActionsContent;
            var icon = Instantiate(actionIconPrefab, parent).GetComponent<ActionIconUI>();
            icon.Setup(action);
        }
        UpdateUIState();
    }

    private void UpdateUIState()
    {
        int max = currentStaff.currentRank != null ? currentStaff.currentRank.maxActions : 0;
        activeActionsHeaderText.text = $"Тактические действия ({tempActiveActions.Count}/{max})";
    }

    public bool CanAddAction()
    {
        int max = currentStaff.currentRank != null ? currentStaff.currentRank.maxActions : 0;
        return tempActiveActions.Count < max;
    }

    public void OnActionDropped(StaffAction action, ActionDropZone.ZoneType zone)
    {
        if (zone == ActionDropZone.ZoneType.Active && !tempActiveActions.Contains(action) && CanAddAction())
            tempActiveActions.Add(action);
        else if (zone == ActionDropZone.ZoneType.Available && tempActiveActions.Contains(action))
            tempActiveActions.Remove(action);
        
        PopulateActionLists();
    }

    public void OnCancel() => gameObject.SetActive(false);

    private void PopulateWorkstationDropdown(StaffController.Role role)
    {
        workstationDropdown.ClearOptions();
        List<string> options = new List<string> { "Не назначено" };
        
        if (ScenePointsRegistry.Instance != null)
        {
            var points = ScenePointsRegistry.Instance.allServicePoints.Where(p => GetRoleForDeskId(p.deskId) == role).ToList();
            if (points.Any())
            {
                workstationDropdown.gameObject.SetActive(true);
                foreach (var p in points)
                {
                    string name = !string.IsNullOrEmpty(p.friendlyName) ? p.friendlyName : p.name;
                    var owner = AssignmentManager.Instance?.GetAssignedStaff(p);
                    if (owner != null && owner != currentStaff) name += $" (Занят: {owner.characterName})";
                    options.Add(name);
                }
                workstationDropdown.AddOptions(options);
                
                int currentIdx = 0;
                if (currentStaff.assignedWorkstation != null)
                {
                    int found = points.IndexOf(currentStaff.assignedWorkstation);
                    if (found >= 0) currentIdx = found + 1;
                }
                workstationDropdown.SetValueWithoutNotify(currentIdx);
            }
            else
            {
                workstationDropdown.gameObject.SetActive(false);
            }
        }
    }

    private StaffController.Role GetRoleForDeskId(int id)
    {
        if (id == 0) return StaffController.Role.Registrar;
        if (id == -1 || id == 4) return StaffController.Role.Cashier;
        if (id == 1 || id == 2) return StaffController.Role.Clerk;
        if (id == 3) return StaffController.Role.Archivist;
        return StaffController.Role.Unassigned;
    }
}