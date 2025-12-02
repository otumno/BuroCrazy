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
        
        // --- ИСПРАВЛЕНИЕ: Заменили DayPeriodManager на TimeManager ---
        if (TimeManager.Instance == null || TimeManager.Instance.mainCalendarDay == null)
        {
            return;
        }

        var currentCalendarDay = TimeManager.Instance.mainCalendarDay.periodSettings;
        var periodTypes = currentCalendarDay.Select(p => p.PeriodType).ToList();
        
        if (!periodTypes.Any()) return;

        var periodNames = periodTypes.Select(t => t.ToString()).ToList();
        shiftDropdown.AddOptions(periodNames);

        var currentIndex = 0;
        if (currentStaff != null && currentStaff.WorkShiftMask != 0)
        {
            for (int i = 0; i < periodTypes.Count; i++)
            {
                if ((currentStaff.WorkShiftMask & periodTypes[i]) != 0)
                {
                    currentIndex = i;
                    break;
                }
            }
        }
        shiftDropdown.SetValueWithoutNotify(currentIndex);
        UpdateShiftInfoText();
    }

    private void UpdateShiftInfoText()
    {
        if (shiftDurationText == null) return;

        // --- ИСПРАВЛЕНИЕ: Заменили DayPeriodManager на TimeManager ---
        if (currentStaff == null || TimeManager.Instance == null || TimeManager.Instance.mainCalendarDay == null)
        {
             shiftDurationText.text = "Периодов: N/A";
             return;
        }
        
        int duration = (currentStaff.currentRank != null) ? currentStaff.currentRank.workPeriodsCount : 3;
        
        var periodSettings = TimeManager.Instance.mainCalendarDay.periodSettings;
        var currentDayPeriods = periodSettings.Select(t => t.PeriodType).ToList();

        if (currentDayPeriods.Count == 0) return;

        int startIndex = shiftDropdown.value;
        if (startIndex < 0 || startIndex >= currentDayPeriods.Count) startIndex = 0;

        var startPeriodName = currentDayPeriods[startIndex];
        
        // --- ИСПРАВЛЕНИЕ ОШИБКИ С % ---
        // Сохраняем количество в переменную int, чтобы компилятор не путался
        int totalCount = currentDayPeriods.Count;
        
        // Теперь математика работает с чистыми числами
        int endIndex = (startIndex + duration - 1 + totalCount) % totalCount;
        
        var endPeriodName = currentDayPeriods[endIndex];

        shiftDurationText.text = $"Периодов: {duration}. С {startPeriodName} по {endPeriodName}";
    }

    private IEnumerator OnSave()
    {
        // 1. Сбрасываем маску смен перед записью новой
        currentStaff.WorkShiftMask = 0; 
        
        // --- ИСПРАВЛЕНИЕ: Используем TimeManager вместо DayPeriodManager ---
        if (Managers.TimeManager.Instance != null && Managers.TimeManager.Instance.mainCalendarDay != null)
        {
            var allPeriods = Managers.TimeManager.Instance.mainCalendarDay.periodSettings
                                .Select(p => p.PeriodType).ToList();
            
            if (allPeriods.Any())
            {
                int startIndex = shiftDropdown.value;
                // Берем длительность смены из ранга или дефолт (3)
                int duration = (currentStaff.currentRank != null) ? currentStaff.currentRank.workPeriodsCount : 3;
                
                for (int i = 0; i < duration; i++)
                {
                    int index = (startIndex + i) % allPeriods.Count;
                    // Добавляем период в маску через побитовое ИЛИ
                    currentStaff.WorkShiftMask |= allPeriods[index];
                }
            }
        }
        else
        {
            Debug.LogError("Не удалось сохранить расписание: TimeManager или календарь не найдены.");
        }
        // ------------------------------------------------------------------

        // Далее идет логика сохранения роли и рабочего места (оставляем как было)
        StaffController.Role currentRole = currentStaff.currentRole;

        if (Managers.AssignmentManager.Instance != null && Managers.ScenePointsRegistry.Instance != null)
        {
            if (workstationDropdown.gameObject.activeSelf && workstationDropdown.value > 0)
            {
                string selectedOptionText = workstationDropdown.options[workstationDropdown.value].text;
                // Отрезаем лишнюю инфу в скобках, если она есть
                string friendlyNameFromDropdown = selectedOptionText.Split('(')[0].Trim();
                
                var selectedPoint = Managers.ScenePointsRegistry.Instance.allServicePoints?
                    .FirstOrDefault(p => p != null && GetWorkstationFriendlyName(p) == friendlyNameFromDropdown);
                
                if (selectedPoint != null) 
                { 
                    Managers.AssignmentManager.Instance.AssignStaffToWorkstation(currentStaff, selectedPoint); 
                }
                else 
                { 
                    Managers.AssignmentManager.Instance.UnassignStaff(currentStaff); 
                }
            }
            else 
            { 
                Managers.AssignmentManager.Instance.UnassignStaff(currentStaff); 
            }
        } 

        // Пересборка компонента (Rebuild) через HiringManager
        Coroutine rebuildCoroutine = null;
        if (Managers.HiringManager.Instance != null)
        {
            // Важно: передаем копию списка действий
            rebuildCoroutine = Managers.HiringManager.Instance.AssignNewRole_Immediate(
                currentStaff, 
                currentRole, 
                new System.Collections.Generic.List<StaffAction>(tempActiveActions)
            );
        }

        if (rebuildCoroutine != null)
        {
            yield return rebuildCoroutine;
            // После пересборки ссылка currentStaff может устареть, обновляем её (хотя панель все равно закрывается)
            if (currentStaff != null)
            {
                currentStaff = currentStaff.gameObject.GetComponent<StaffController>(); 
            } 
        }
        else 
        { 
            yield return null; 
        }

        gameObject.SetActive(false); // Закрываем панель

        // Обновляем список в отделе кадров
        HiringPanelUI hiringPanel = FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include);
        if (hiringPanel != null) hiringPanel.RefreshTeamList();

        // Проверяем смены немедленно
        Managers.HiringManager.Instance?.CheckAllStaffShiftsImmediately();
    }


    
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
	
	private string GetWorkstationFriendlyName(ServicePoint point)
    {
        if (point == null) return "Неизвестно";
        // Используем friendlyName если оно есть, иначе имя GameObject'а
        return !string.IsNullOrEmpty(point.friendlyName) ? point.friendlyName : point.name;
    }
	
}