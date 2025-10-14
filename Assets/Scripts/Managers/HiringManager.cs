// Файл: Assets/Scripts/Managers/HiringManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Collections;

[System.Serializable]
public class HiringManager : MonoBehaviour
{
    public static HiringManager Instance { get; set; }

    [Header("Префабы сотрудников")]
    public GameObject internPrefab;
    [Header("Базы данных")]
	public List<RoleData> allRoleData;
	
    public List<StaffController> AllStaff = new List<StaffController>();
    public List<StaffController> UnassignedStaff = new List<StaffController>();

    private List<Transform> unassignedStaffPoints = new List<Transform>();
    private Dictionary<Transform, StaffController> occupiedPoints = new Dictionary<Transform, StaffController>();
    [Header("Настройки генерации")]
    public int minCandidatesPerDay = 2;
    public int maxCandidatesPerDay = 4;
    public int baseCost = 100;
    public int costPerSkillPoint = 150;

    private List<string> firstNamesMale = new List<string> { "Виктор", "Иван", "Петр", "Семён", "Аркадий", "Борис", "Геннадий" };
    private List<string> firstNamesFemale = new List<string> { "Анна", "Мария", "Ольга", "Светлана", "Ирина", "Валентина", "Галина" };
    private List<string> lastNames = new List<string> { "Скрепкин", "Бланков", "Циркуляров", "Печаткин", "Архивариусов", "Формуляров" };
    private List<string> patronymicsMale = new List<string> { "Радеонович", "Петрович", "Иванович", "Семёнович", "Аркадьевич", "Борисович", "Геннадьевич" };
    private List<string> patronymicsFemale = new List<string> { "Радеоновна", "Петровна", "Ивановна", "Семёновна", "Аркадьевна", "Борисовна", "Геннадьевна" };

    public List<Candidate> AvailableCandidates { get; private set; } = new List<Candidate>();
	
	private List<StaffController> staffBeingModified = new List<StaffController>();

    void Awake()
    {
        if (Instance == null) { Instance = this; SceneManager.sceneLoaded += OnSceneLoaded; } 
        else if (Instance != this) { Destroy(gameObject); }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene")
        {
            FindSceneSpecificReferences();
            StartCoroutine(RegisterExistingStaffAndAssignDatabases());
        }
    }

    private IEnumerator RegisterExistingStaffAndAssignDatabases()
    {
        yield return new WaitForEndOfFrame(); 
        
        Debug.Log("[HiringManager] Запущена регистрация и назначение баз данных...");
        AllStaff.Clear();
        StaffController[] existingStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);

        ActionDatabase systemActions = null;
        var firstStaff = existingStaff.FirstOrDefault(s => s != null && !(s is DirectorAvatarController));
        if (firstStaff != null)
        {
            systemActions = firstStaff.systemActionDatabase;
        }

        foreach (var staffMember in existingStaff)
        {
            if (staffMember is DirectorAvatarController) continue;
            
            AllStaff.Add(staffMember);
            
            if (staffMember.systemActionDatabase == null && systemActions != null)
            {
                staffMember.systemActionDatabase = systemActions;
                Debug.Log($"Назначена база системных действий для {staffMember.characterName}");
            }
        }
        Debug.Log($"[HiringManager] Регистрация завершена. Найдено и учтено: {AllStaff.Count} сотрудников.");
    }

    public void AssignNewRole_Immediate(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
    {
        if (staff == null) return;
        
        Debug.Log($"<color=yellow>ОПЕРАЦИЯ:</color> Начинаем смену роли для {staff.characterName} с {staff.currentRole} на {newRole}");

        staff.activeActions = newActions;
        
        System.Type requiredControllerType = GetControllerTypeForRole(newRole);
        System.Type currentControllerType = staff.GetType();

        if (requiredControllerType == currentControllerType)
        {
            staff.currentRole = newRole;
            if (staff is ClerkController clerk)
            {
                clerk.role = GetClerkRoleFromStaffRole(newRole);
            }
            Debug.Log($"<color=green>ОПЕРАЦИЯ УСПЕШНА:</color> Роль для {staff.characterName} обновлена без пересоздания компонента.");
        }
        else
        {
            StartCoroutine(RebuildControllerComponent(staff, newRole, newActions));
        }
    }

    private IEnumerator RebuildControllerComponent(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
    {
        if (staffBeingModified.Contains(staff))
        {
            yield break;
        }
        staffBeingModified.Add(staff);

        try
        {
            GameObject staffGO = staff.gameObject;
            Debug.Log($"<color=orange>ПЕРЕСБОРКА КОМПОНЕНТА:</color> для {staff.characterName}");
            
            string savedName = staff.characterName;
            Gender savedGender = staff.gender;
            CharacterSkills savedSkills = staff.skills;
            int savedRank = staff.rank;
            int savedXP = staff.experiencePoints;
            int savedSalary = staff.salaryPerPeriod;
            bool savedPromoStatus = staff.isReadyForPromotion;
            int savedUnpaidPeriods = staff.unpaidPeriods;
            int savedMissedPayments = staff.missedPaymentCount;
            List<string> savedWorkPeriods = new List<string>(staff.workPeriods);
            ServicePoint savedWorkstation = staff.assignedWorkstation;
            
            yield return new WaitForEndOfFrame();

            Destroy(staff);

            StaffController newControllerReference = null;
            System.Type newControllerType = GetControllerTypeForRole(newRole);
            if(newControllerType != null)
            {
                newControllerReference = staffGO.AddComponent(newControllerType) as StaffController;
            }

            if (newControllerReference == null)
            {
                Debug.LogError($"<color=red>КРИТИЧЕСКАЯ ОШИБКА:</color> Не удалось добавить новый компонент-контроллер для роли {newRole}!");
                yield break;
            }
            
            newControllerReference.characterName = savedName;
            newControllerReference.gender = savedGender;
            newControllerReference.skills = savedSkills;
            newControllerReference.rank = savedRank;
            newControllerReference.experiencePoints = savedXP;
            newControllerReference.salaryPerPeriod = savedSalary;
            newControllerReference.isReadyForPromotion = savedPromoStatus;
            newControllerReference.unpaidPeriods = savedUnpaidPeriods;
            newControllerReference.missedPaymentCount = savedMissedPayments;
            newControllerReference.workPeriods = savedWorkPeriods;
            newControllerReference.activeActions = newActions;
            if (savedWorkstation != null)
            {
                AssignmentManager.Instance.AssignStaffToWorkstation(newControllerReference, savedWorkstation);
            }

            newControllerReference.ForceInitializeBaseComponents(staffGO.GetComponent<AgentMover>(), staffGO.GetComponent<CharacterVisuals>(), staffGO.GetComponent<CharacterStateLogger>());
            RoleData dataForNewRole = allRoleData.FirstOrDefault(data => data.roleType == newRole);
            if (dataForNewRole != null)
            {
                newControllerReference.Initialize(dataForNewRole);
                if (newControllerReference is GuardMovement newGuard) newGuard.InitializeFromData(dataForNewRole);
                if (newControllerReference is ServiceWorkerController newWorker) newWorker.InitializeFromData(dataForNewRole);
                if (newControllerReference is InternController newIntern) newIntern.InitializeFromData(dataForNewRole);
                if (newControllerReference is ClerkController newClerk)
                {
                    newClerk.role = GetClerkRoleFromStaffRole(newRole);
                    newClerk.allRoleData = this.allRoleData;
                }
            }
            
            int staffIndex = AllStaff.FindIndex(s => s == null || s.gameObject == staffGO);
            if (staffIndex != -1)
            {
                AllStaff[staffIndex] = newControllerReference;
            }
            
            Debug.Log($"<color=green>ПЕРЕСБОРКА УСПЕШНА:</color> Роль для {newControllerReference.characterName} полностью изменена.");
        }
        finally
        {
            staffBeingModified.Remove(staff);
        }
    }

    private System.Type GetControllerTypeForRole(StaffController.Role role)
    {
        switch (role)
        {
            case StaffController.Role.Guard: return typeof(GuardMovement);
            case StaffController.Role.Clerk:
            case StaffController.Role.Registrar:
            case StaffController.Role.Cashier:
            case StaffController.Role.Archivist: return typeof(ClerkController);
            case StaffController.Role.Janitor: return typeof(ServiceWorkerController);
            case StaffController.Role.Intern: return typeof(InternController);
            default: return null;
        }
    }

    private ClerkController.ClerkRole GetClerkRoleFromStaffRole(StaffController.Role role)
    {
        switch (role)
        {
            case StaffController.Role.Registrar: return ClerkController.ClerkRole.Registrar;
            case StaffController.Role.Cashier: return ClerkController.ClerkRole.Cashier;
            case StaffController.Role.Archivist: return ClerkController.ClerkRole.Archivist;
            default: return ClerkController.ClerkRole.Regular;
        }
    }

    public void ActivateAllScheduledStaff()
    {
        string currentPeriod = ClientSpawner.CurrentPeriodName;
        if (string.IsNullOrEmpty(currentPeriod)) return;
        Debug.Log($"<color=orange>ЗАПУСК AI:</color> Активация всех сотрудников, работающих в период '{currentPeriod}'...");
        foreach (var staff in AllStaff)
        {
            if (staff.workPeriods.Contains(currentPeriod) && !staff.IsOnDuty())
            {
                staff.StartShift();
            }
        }
    }

    public void PromoteStaff(StaffController staff)
    {
        if (staff == null || !staff.isReadyForPromotion) return;

        RankData nextRankData = ExperienceManager.Instance.rankDatabase
            .FirstOrDefault(r => r.rankLevel == staff.rank + 1);
        if (nextRankData == null) return;
    
        if (PlayerWallet.Instance.GetCurrentMoney() < nextRankData.promotionCost) return;

        PlayerWallet.Instance.AddMoney(-nextRankData.promotionCost, Vector3.zero);
        staff.rank = nextRankData.rankLevel;
        staff.salaryPerPeriod = (int)(staff.salaryPerPeriod * nextRankData.salaryMultiplier);
        staff.isReadyForPromotion = false;
    }

    public void ResetState()
    {
        AvailableCandidates.Clear();
        occupiedPoints.Clear();
        UnassignedStaff.Clear();
        AllStaff.Clear();
    }

    private void FindSceneSpecificReferences()
    {
        unassignedStaffPoints.Clear();
        occupiedPoints.Clear();
        InternPointsRegistry registry = FindFirstObjectByType<InternPointsRegistry>();
        if (registry != null)
        {
            unassignedStaffPoints = registry.points;
            Debug.Log($"[HiringManager] Успешно найдено {unassignedStaffPoints.Count} точек для стажеров.");
        }
        else
        {
            Debug.LogError("[HiringManager] НЕ УДАЛОСЬ найти InternPointsRegistry на сцене GameScene!");
        }
    }

    public void GenerateNewCandidates()
    {
        AvailableCandidates.Clear();
        int count = Random.Range(minCandidatesPerDay, maxCandidatesPerDay + 1);
        for (int i = 0; i < count; i++)
        {
            AvailableCandidates.Add(CreateRandomCandidate());
        }
        Debug.Log($"[HiringManager] Сгенерировано {AvailableCandidates.Count} новых кандидатов.");
    }

    private Candidate CreateRandomCandidate()
    {
        Candidate candidate = new Candidate();
        candidate.Gender = (Random.value > 0.5f) ? Gender.Male : Gender.Female;
        
        if (candidate.Gender == Gender.Male)
        {
            candidate.Name = $"{lastNames[Random.Range(0, lastNames.Count)]} {firstNamesMale[Random.Range(0, firstNamesMale.Count)]} {patronymicsMale[Random.Range(0, patronymicsMale.Count)]}";
        }
        else
        {
            candidate.Name = $"{lastNames[Random.Range(0, lastNames.Count)]}а {firstNamesFemale[Random.Range(0, firstNamesFemale.Count)]} {patronymicsFemale[Random.Range(0, patronymicsFemale.Count)]}";
        }

        candidate.Skills = ScriptableObject.CreateInstance<CharacterSkills>();
        
        candidate.Skills.paperworkMastery = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        candidate.Skills.sedentaryResilience = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        candidate.Skills.pedantry = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        candidate.Skills.softSkills = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        candidate.Skills.corruption = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;

        float totalSkillPoints = candidate.Skills.paperworkMastery + candidate.Skills.sedentaryResilience +
                                 candidate.Skills.pedantry + candidate.Skills.softSkills;
        candidate.HiringCost = baseCost + (int)(totalSkillPoints * costPerSkillPoint);
		
        candidate.Bio = ResumeGenerator.GenerateBio();
        if (Random.value < 0.2f)
        {
            Debug.Log($"Сгенерирован кандидат {candidate.Name} с потенциальным уникальным навыком!");
        }

        return candidate;
    }

    public bool HireCandidate(Candidate candidate)
    {
        if (PlayerWallet.Instance.GetCurrentMoney() < candidate.HiringCost) return false;
        
        Transform freePoint = unassignedStaffPoints.FirstOrDefault(p => !occupiedPoints.ContainsKey(p));
        if (freePoint == null) return false;

        RoleData internRoleData = allRoleData.FirstOrDefault(data => data.roleType == StaffController.Role.Intern);
        if (internRoleData == null) return false;

        PlayerWallet.Instance.AddMoney(-candidate.HiringCost, $"Найм: {candidate.Name}");
        
        GameObject newStaffGO = Instantiate(internPrefab, freePoint.position, Quaternion.identity);
        InternController internController = newStaffGO.GetComponent<InternController>();

        if (internController != null)
        {
            internController.characterName = candidate.Name;
            internController.skills = candidate.Skills;
            internController.gender = candidate.Gender;
            
            internController.Initialize(internRoleData);

            // 1. Копируем ссылку на базу системных действий от первого попавшегося сотрудника на сцене.
            var existingStaff = AllStaff.FirstOrDefault(s => s != null && s.systemActionDatabase != null);
            if (existingStaff != null)
            {
                internController.systemActionDatabase = existingStaff.systemActionDatabase;
            }

            // 2. Назначаем расписание на все периоды по умолчанию
            if (ClientSpawner.Instance != null && ClientSpawner.Instance.mainCalendar != null)
            {
                internController.workPeriods = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).ToList();
            }

            AllStaff.Add(internController);
            UnassignedStaff.Add(internController);
            newStaffGO.name = candidate.Name;
            occupiedPoints.Add(freePoint, internController);
            AvailableCandidates.Remove(candidate);
            
            internController.StartShift();
            
            Debug.Log($"Нанят сотрудник {candidate.Name}. Он немедленно приступает к работе.");
            return true;
        }

        Destroy(newStaffGO);
        PlayerWallet.Instance.AddMoney(candidate.HiringCost, Vector3.zero);
        return false;
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

    public void FireStaff(StaffController staffToFire)
    {
        if (staffToFire == null) return;
        Debug.Log($"Отдана команда на увольнение сотрудника {staffToFire.characterName}!");

        if(AllStaff.Contains(staffToFire)) AllStaff.Remove(staffToFire);
        if(UnassignedStaff.Contains(staffToFire)) UnassignedStaff.Remove(staffToFire);
        if (occupiedPoints.ContainsValue(staffToFire))
        {
            Transform pointToFree = occupiedPoints.FirstOrDefault(kvp => kvp.Value == staffToFire).Key;
            if (pointToFree != null)
            {
                occupiedPoints.Remove(pointToFree);
            }
        }
        
        staffToFire.FireAndGoHome();
    }
	
	public void CheckAllStaffShiftsImmediately()
    {
        string currentPeriod = ClientSpawner.CurrentPeriodName;
        if (string.IsNullOrEmpty(currentPeriod)) return;
        Debug.Log($"<color=orange>ПРОВЕРКА СМЕН:</color> Проверка расписания для периода '{currentPeriod}'...");
        foreach (var staff in AllStaff.ToList()) // ToList() creates a copy to allow modification during iteration
        {
            if (staff == null) continue;
            bool isScheduledNow = staff.workPeriods.Contains(currentPeriod);
            bool isOnDuty = staff.IsOnDuty();

            if (!isScheduledNow && isOnDuty)
            {
                Debug.Log($"{staff.characterName} больше не числится в смене. Отправляем домой.");
                staff.EndShift();
            }
        }
    }
}