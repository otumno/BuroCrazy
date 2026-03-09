using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

public class ActionConfigPopupUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private TextMeshProUGUI currentRoleText;
    // [SerializeField] private TMP_Dropdown shiftDropdown; // УДАЛЕНО
    // [SerializeField] private TextMeshProUGUI shiftDurationText; // УДАЛЕНО
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
    
    // --- ИСПРАВЛЕНИЕ: Ссылка на новую панель расписания (для обновления) ---
    [Header("Связи")]
    [SerializeField] private StaffSchedulePanelUI schedulePanel; 
    // ----------------------------------------------------------------------

    private StaffController currentStaff;
    private List<StaffAction> tempActiveActions = new List<StaffAction>();

    private void Awake()
    {
        saveButton.onClick.AddListener(() => StartCoroutine(OnSave()));
        cancelButton.onClick.AddListener(OnCancel);
        // shiftDropdown.onValueChanged... // УДАЛЕНО
        
        if (availableActionsDropZone != null) { availableActionsDropZone.popupController = this; availableActionsDropZone.type = ActionDropZone.ZoneType.Available; }
        if (activeActionsDropZone != null) { activeActionsDropZone.popupController = this; activeActionsDropZone.type = ActionDropZone.ZoneType.Active; }
    }

    public void OpenForStaff(StaffController staff)
    {
        currentStaff = staff;
        tempActiveActions = new List<StaffAction>(staff.activeActions ?? new List<StaffAction>());
        gameObject.SetActive(true);

        if (currentRoleText != null) currentRoleText.text = staff.currentRole.ToString();
        
        PopulateWorkstationDropdown(currentStaff.currentRole);
        PopulateActionLists();
    }

    // Методы PopulateShiftDropdown и UpdateShiftInfoText УДАЛЕНЫ, так как UI элементы удалены

    private IEnumerator OnSave()
    {
        // Логику смен (WorkShiftMask) здесь больше НЕ трогаем, 
        // она управляется напрямую через клики в StaffSchedulePanelUI.

        // Сохранение роли и рабочего места
        StaffController.Role currentRole = currentStaff.currentRole;

        if (AssignmentManager.Instance != null && ScenePointsRegistry.Instance != null)
        {
            if (workstationDropdown.gameObject.activeSelf && workstationDropdown.value > 0)
            {
                string selectedOptionText = workstationDropdown.options[workstationDropdown.value].text;
                string friendlyNameFromDropdown = selectedOptionText.Split('(')[0].Trim();
                
                var selectedPoint = ScenePointsRegistry.Instance.allServicePoints?
                    .FirstOrDefault(p => p != null && GetWorkstationFriendlyName(p) == friendlyNameFromDropdown);
                
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
        } 

        // Пересборка компонента (Rebuild) через HiringManager
        Coroutine rebuildCoroutine = null;
        if (HiringManager.Instance != null)
        {
            rebuildCoroutine = HiringManager.Instance.AssignNewRole_Immediate(
                currentStaff, 
                currentRole, 
                new List<StaffAction>(tempActiveActions)
            );
        }

        if (rebuildCoroutine != null)
        {
            yield return rebuildCoroutine;
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
        MainUIManager.Instance.PopPause();

        HiringPanelUI hiringPanel = FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include);
        if (hiringPanel != null) hiringPanel.RefreshTeamList();
        
        // Обновляем панель расписания, если она есть, чтобы отразить новые роли/имена
        if (schedulePanel != null) schedulePanel.RebuildTable();

        HiringManager.Instance?.CheckAllStaffShiftsImmediately();
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
            
            // Показываем кнопку принуждения только для активных действий (нижняя панель)
            bool showForceButton = (parent == activeActionsContent);
            System.Action<StaffAction> forceCallback = showForceButton ? OnForceActionClicked : null;
            
            icon.Setup(action, showForceButton, forceCallback);
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

    private bool _isClosing = false;

    public void OnCancel() {
        if (_isClosing || !gameObject.activeInHierarchy) return;
        _isClosing = true;
        
        gameObject.SetActive(false);
        MainUIManager.Instance.PopPause();
        
        _isClosing = false;
    }

    /// <summary>
    /// Обработчик нажатия на кнопку принуждения (ручной приказ)
    /// </summary>
    private void OnForceActionClicked(StaffAction action)
    {
        if (DirectorAvatarController.Instance != null && currentStaff != null)
        {
            DirectorAvatarController.Instance.GiveOrder(currentStaff, action);
        }
        gameObject.SetActive(false);
        MainUIManager.Instance.PopPause();
    }

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
        return !string.IsNullOrEmpty(point.friendlyName) ? point.friendlyName : point.name;
    }
}