using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

    [Header("Префабы и данные")]
    [SerializeField] private GameObject actionIconPrefab;
    [SerializeField] private ActionDatabase actionDatabase;

    private StaffController currentStaff;

    private void Awake()
    {
        saveButton.onClick.AddListener(OnSave);
        cancelButton.onClick.AddListener(OnCancel);
        shiftDropdown.onValueChanged.AddListener(delegate { UpdateShiftInfoText(); });
    }

    public void OpenForStaff(StaffController staff)
    {
        this.currentStaff = staff;
        gameObject.SetActive(true);

        PopulateRoleDropdown();
        PopulateShiftDropdown();
        PopulateActionLists();
    }

    // --- ВОЗВРАЩЕННЫЙ МЕТОД ---
    private void PopulateRoleDropdown()
    {
        roleDropdown.ClearOptions();

        if (currentStaff.rank == 0)
        {
            roleDropdown.AddOptions(new List<string> { GetRoleNameInRussian(currentStaff.currentRole) });
            roleDropdown.value = 0;
            roleDropdown.interactable = false;
        }
        else
        {
            List<StaffController.Role> assignableRolesEnum = System.Enum.GetValues(typeof(StaffController.Role))
                .Cast<StaffController.Role>()
                .Where(role => role != StaffController.Role.Unassigned && role != StaffController.Role.Intern)
                .ToList();
            
            List<string> assignableRolesRussian = assignableRolesEnum.Select(role => GetRoleNameInRussian(role)).ToList();
            roleDropdown.AddOptions(assignableRolesRussian);

            int currentIndex = assignableRolesEnum.IndexOf(currentStaff.currentRole);
            roleDropdown.value = Mathf.Max(0, currentIndex);
            roleDropdown.interactable = true;
        }
    }

    // --- ВОЗВРАЩЕННЫЙ МЕТОД ---
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

    private void PopulateShiftDropdown()
    {
        shiftDropdown.ClearOptions();
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.periods.Length == 0) return;

        List<string> periodNames = ClientSpawner.Instance.periods.Select(p => p.periodName).ToList();
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
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.periods.Length == 0) return;

        List<string> allPeriods = ClientSpawner.Instance.periods.Select(p => p.periodName).ToList();
        int startIndex = shiftDropdown.value;
        int duration = GetShiftDurationByRank(currentStaff.rank);
        string startPeriodName = allPeriods[startIndex];
        int endIndex = (startIndex + duration - 1) % allPeriods.Count;
        string endPeriodName = allPeriods[endIndex];
        shiftDurationText.text = $"Периодов: {duration}. С {startPeriodName} по {endPeriodName}";
    }
    
    // --- ВОЗВРАЩЕННЫЙ МЕТОД ---
    private int GetShiftDurationByRank(int rank)
    {
        if (rank == 0) return 3;
        if (rank == 1) return 4;
        if (rank == 2) return 5;
        return 6;
    }

    // --- ВОЗВРАЩЕННЫЙ МЕТОД ---
    private void PopulateActionLists()
    {
        foreach (Transform child in availableActionsContent) { Destroy(child.gameObject); }
        foreach (Transform child in activeActionsContent) { Destroy(child.gameObject); }

        if (actionDatabase == null || currentStaff == null) return;

        foreach (var activeAction in currentStaff.activeActions)
        {
            InstantiateActionIcon(activeAction, activeActionsContent);
        }

        List<StaffAction> allAvailableActions = actionDatabase.allActions
            .Where(action => action.minRankRequired <= currentStaff.rank &&
                             action.applicableRoles.Contains(currentStaff.currentRole))
            .ToList();

        foreach (var availableAction in allAvailableActions)
        {
            if (!currentStaff.activeActions.Contains(availableAction))
            {
                InstantiateActionIcon(availableAction, availableActionsContent);
            }
        }
    }

    // --- ВОЗВРАЩЕННЫЙ МЕТОД ---
    private void InstantiateActionIcon(StaffAction action, Transform parent)
    {
        GameObject iconGO = Instantiate(actionIconPrefab, parent);
        ActionIconUI iconUI = iconGO.GetComponent<ActionIconUI>();
        if (iconUI != null)
        {
            iconUI.Setup(action);
        }
    }

    private void OnSave()
{
    // --- Часть 1: Смена роли (без изменений) ---
    string selectedRoleName = roleDropdown.options[roleDropdown.value].text;
    StaffController.Role newRole = (StaffController.Role)System.Enum.Parse(typeof(StaffController.Role), selectedRoleName);

    if (newRole != currentStaff.currentRole)
    {
        HiringManager.Instance.AssignNewRole(currentStaff, newRole);
    }
    
    // --- Часть 2: ДОБАВЛЕНО - Сохранение нового списка активных действий ---
        if (ClientSpawner.Instance != null && ClientSpawner.Instance.periods.Length > 0)
        {
            currentStaff.workPeriods.Clear();
            List<string> allPeriods = ClientSpawner.Instance.periods.Select(p => p.periodName).ToList();
            int startIndex = shiftDropdown.value;
            int duration = GetShiftDurationByRank(currentStaff.rank);

            for (int i = 0; i < duration; i++)
            {
                // Добавляем периоды по кругу, чтобы смена "переносилась" на следующий день
                int periodIndex = (startIndex + i) % allPeriods.Count;
                currentStaff.workPeriods.Add(allPeriods[periodIndex]);
            }
        }
    
    // Проходим по всем иконкам, которые игрок перетащил в контейнер активных действий
    foreach (Transform iconTransform in activeActionsContent)
    {
        ActionIconUI iconUI = iconTransform.GetComponent<ActionIconUI>();
        if (iconUI != null)
        {
            // Добавляем "паспорт" действия из иконки в "память" сотрудника
            currentStaff.activeActions.Add(iconUI.actionData);
        }
    }
    Debug.Log($"Сохранен новый список из {currentStaff.activeActions.Count} действий для {currentStaff.characterName}.");

    // --- Часть 3: Закрытие и обновление (без изменений) ---
    gameObject.SetActive(false);
    FindFirstObjectByType<HiringPanelUI>()?.RefreshTeamList();
}

    private void OnCancel()
    {
        gameObject.SetActive(false);
    }
}