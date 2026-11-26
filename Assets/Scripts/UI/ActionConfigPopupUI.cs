using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
    private readonly List<ServicePoint> workstationOptions = new();
    private MainUIManager pauseOwner;
    private List<StaffAction> tempActiveActions = new List<StaffAction>();

    private void Awake()
    {
        saveButton.onClick.AddListener(OnSave);
        cancelButton.onClick.AddListener(OnCancel);
        // shiftDropdown.onValueChanged... // УДАЛЕНО
        
        if (availableActionsDropZone != null) { availableActionsDropZone.popupController = this; availableActionsDropZone.type = ActionDropZone.ZoneType.Available; }
        if (activeActionsDropZone != null) { activeActionsDropZone.popupController = this; activeActionsDropZone.type = ActionDropZone.ZoneType.Active; }
    }

    public void OpenForStaff(StaffController staff)
    {
        if (staff == null) return;
        if (pauseOwner == null && MainUIManager.Instance != null)
        {
            pauseOwner = MainUIManager.Instance;
            pauseOwner.PushPause();
        }
        currentStaff = staff;
        tempActiveActions = new List<StaffAction>(staff.activeActions ?? new List<StaffAction>());
        gameObject.SetActive(true);

        if (currentRoleText != null) currentRoleText.text = UI.StaffRoleNames.GetDisplayName(staff.currentRole);
        
        PopulateWorkstationDropdown(currentStaff.assignedWorkstation);
        PopulateActionLists();
    }

    // Методы PopulateShiftDropdown и UpdateShiftInfoText УДАЛЕНЫ, так как UI элементы удалены

    private void OnEnable() => UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += RefreshLocale;

    private void OnDisable()
    {
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= RefreshLocale;
        if (pauseOwner != null) pauseOwner.PopPause();
        pauseOwner = null;
    }

    private ServicePoint SelectedWorkstation =>
        workstationDropdown != null && workstationDropdown.value > 0 && workstationDropdown.value <= workstationOptions.Count
            ? workstationOptions[workstationDropdown.value - 1] : null;

    private void RefreshLocale(UnityEngine.Localization.Locale locale)
    {
        if (currentStaff == null) return;
        if (currentRoleText != null) currentRoleText.text = UI.StaffRoleNames.GetDisplayName(currentStaff.currentRole);
        PopulateWorkstationDropdown(SelectedWorkstation);
        UpdateUIState();
    }

    private void OnSave()
    {
        if (currentStaff == null) { OnCancel(); return; }
        currentStaff.activeActions = new List<StaffAction>(tempActiveActions);
        var assignments = AssignmentManager.Instance;
        if (workstationDropdown != null && workstationDropdown.gameObject.activeSelf && assignments != null &&
            currentStaff.assignedWorkstation != SelectedWorkstation)
        {
            if (SelectedWorkstation != null) assignments.AssignStaffToWorkstation(currentStaff, SelectedWorkstation);
            else assignments.UnassignStaff(currentStaff);
            currentStaff.ReturnToNewAssignment();
        }
        OnCancel();
        schedulePanel?.RefreshTable();
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
        if (activeActionsHeaderText != null) activeActionsHeaderText.text = UI.GameText.Get("tactics.count", tempActiveActions.Count, max);
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

    public void OnCancel()
    {
        gameObject.SetActive(false);
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
        OnCancel();
    }

    private void PopulateWorkstationDropdown(ServicePoint selected)
    {
        if (workstationDropdown == null) return;
        workstationOptions.Clear();
        workstationDropdown.ClearOptions();
        var registry = ScenePointsRegistry.Instance;
        if (registry != null)
            workstationOptions.AddRange(registry.allServicePoints.Where(p => p == currentStaff.assignedWorkstation && p != null || AssignmentManager.CanAssignTo(currentStaff, p)));
        workstationDropdown.gameObject.SetActive(workstationOptions.Count > 0);
        var options = new List<string> { UI.GameText.Get("tactics.unassigned") };
        foreach (var point in workstationOptions)
        {
            string name = CharacterActivityOverlay.DeskName(point);
            var owner = AssignmentManager.Instance?.GetAssignedStaff(point);
            options.Add(owner != null && owner != currentStaff ? UI.GameText.Get("tactics.occupied", name, owner.characterName) : name);
        }
        workstationDropdown.AddOptions(options);
        workstationDropdown.SetValueWithoutNotify(workstationOptions.IndexOf(selected) + 1);
    }
}
