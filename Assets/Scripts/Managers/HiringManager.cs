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
    [Tooltip("Пока используется только один префаб для всех ролей. В будущем можно будет расширить.")]
    public GameObject internPrefab; // TODO: Заменить на список префабов для каждой роли
    
    [Header("Базы данных")]
	[Tooltip("Перетащите сюда все ассеты RoleData")]
	public List<RoleData> allRoleData;
    [Tooltip("Перетащите сюда ассет RankDatabase")]
    public List<RankData> rankDatabase;
	
    public List<StaffController> AllStaff = new List<StaffController>();
    public List<StaffController> UnassignedStaff = new List<StaffController>();

    private List<Transform> unassignedStaffPoints = new List<Transform>();
    private Dictionary<Transform, StaffController> occupiedPoints = new Dictionary<Transform, StaffController>();
    
    [Header("Настройки генерации кандидатов")]
    [Tooltip("Кривая, определяющая КОЛИЧЕСТВО стажеров в зависимости от дня. Ось X - день, Y - количество.")]
    public AnimationCurve internCountOverTime = new AnimationCurve(new Keyframe(1, 4), new Keyframe(30, 1));
    
    [Tooltip("Кривая, определяющая КОЛИЧЕСТВО специалистов в зависимости от дня.")]
    public AnimationCurve specialistCountOverTime = new AnimationCurve(new Keyframe(1, 0), new Keyframe(10, 1), new Keyframe(30, 3));
    
    [Tooltip("Кривая, определяющая ШАНС (0-1) появления опытного стажера (2-го или 3-го разряда) в зависимости от дня.")]
    public AnimationCurve experiencedInternChance = new AnimationCurve(new Keyframe(1, 0), new Keyframe(5, 0.1f), new Keyframe(30, 0.5f));

    [Header("Стоимость найма")]
    public int baseCost = 100;
    public int costPerSkillPoint = 150;

    // --- РАСШИРЕННЫЕ СПИСКИ ИМЕН ---
    private List<string> firstNamesMale = new List<string> 
    { 
        "Аркадий", "Иннокентий", "Пантелеймон", "Акакий", "Евграф", "Порфирий", 
        "Лука", "Фома", "Прохор", "Варфоломей", "Ипполит", "Модест",
        "Савва", "Корней", "Никанор", "Афанасий", "Ефим", "Игнат",
        "Аполлон", "Власий", "Захар", "Климент", "Лаврентий", "Макар", "Поликарп"
    };
    private List<string> firstNamesFemale = new List<string> 
    { 
        "Аглая", "Евпраксия", "Пелагея", "Серафима", "Фёкла", "Глафира", 
        "Агриппина", "Василиса", "Ефросинья", "Ираида", "Марфа", "Прасковья",
        "Акулина", "Матрёна", "Степанида", "Анфиса", "Зинаида", "Варвара",
        "Авдотья", "Евдокия", "Изольда", "Олимпиада", "Пульхерия", "Феврония"
    };
    private List<string> lastNames = new List<string> 
    { 
        "Перепискин", "Протоколов", "Архивариусов", "Гербовый", "Подшивайлов", "Нумеров", 
        "Формуляров", "Циркуляров", "Бланков", "Скрепкин", "Печаткин", "Резолюцин",
        "Визируйко", "Канцелярский", "Копиркин", "Чернилов", "Аттестатов", "Докладов",
        "Входящий", "Исходящий", "Журналов", "Приказов", "Указов", "Описин"
    };
    private List<string> patronymicsMale = new List<string> 
    { 
        "Аркадьевич", "Иннокентьевич", "Пантелеймонович", "Акакиевич", "Евграфович", "Порфирьевич",
        "Лукич", "Фомич", "Прохорович", "Варфоломеевич", "Ипполитович", "Модестович",
        "Саввич", "Корнеевич", "Никанорович", "Афанасьевич", "Ефимович", "Игнатьевич",
        "Аполлонович", "Власьевич", "Захарович", "Климентович", "Лаврентьевич", "Макарович"
    };
    private List<string> patronymicsFemale = new List<string> 
    { 
        "Аркадьевна", "Иннокентьевна", "Пантелеймоновна", "Акакиевна", "Евграфовна", "Порфирьевна",
        "Лукинична", "Фоминична", "Прохоровна", "Варфоломеевна", "Ипполитовна", "Модестовна",
        "Саввична", "Корнеевна", "Никаноровна", "Афанасьевна", "Ефимовна", "Игнатьевна",
        "Аполлоновна", "Власьевна", "Захаровна", "Климентовна", "Лаврентьевна", "Макаровна"
    };
    // --- КОНЕЦ СПИСКОВ ИМЕН ---

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
            RankData savedRank = staff.currentRank; 
            int savedXP = staff.experiencePoints;
            int savedSalary = staff.salaryPerPeriod;
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
            newControllerReference.currentRank = savedRank;
            newControllerReference.experiencePoints = savedXP;
            newControllerReference.salaryPerPeriod = savedSalary;
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
            staffBeingModified.RemoveAll(s => s == null || s.gameObject == staff.gameObject);
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
    
    public void PromoteStaff(StaffController staff, RankData newRankData)
    {
        if (staff == null || newRankData == null) return;
        
        if (staff.experiencePoints < newRankData.experienceRequired) return;
        if (PlayerWallet.Instance.GetCurrentMoney() < newRankData.promotionCost) return;

        PlayerWallet.Instance.AddMoney(-newRankData.promotionCost, $"Повышение: {staff.characterName}");

        staff.currentRank = newRankData;
        staff.salaryPerPeriod = (int)(staff.salaryPerPeriod * newRankData.salaryMultiplier);

        if (newRankData.unlockedActions != null)
        {
            foreach (var action in newRankData.unlockedActions)
            {
                if (!staff.activeActions.Contains(action))
                {
                    staff.activeActions.Add(action);
                }
            }
        }

        if (staff.currentRole != newRankData.associatedRole)
        {
            AssignNewRole_Immediate(staff, newRankData.associatedRole, staff.activeActions);
        }
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
        int currentDay = ClientSpawner.Instance.GetCurrentDay();

        int internsToCreate = Mathf.RoundToInt(internCountOverTime.Evaluate(currentDay));
        int specialistsToCreate = Mathf.RoundToInt(specialistCountOverTime.Evaluate(currentDay));
        float experiencedChance = experiencedInternChance.Evaluate(currentDay);

        for (int i = 0; i < internsToCreate; i++)
        {
            AvailableCandidates.Add(CreateRandomCandidate(StaffController.Role.Intern, experiencedChance));
        }

        for (int i = 0; i < specialistsToCreate; i++)
        {
            var specialistRoles = System.Enum.GetValues(typeof(StaffController.Role))
                .Cast<StaffController.Role>()
                .Where(r => r != StaffController.Role.Intern && r != StaffController.Role.Unassigned)
                .ToList();
            
            StaffController.Role randomRole = specialistRoles[Random.Range(0, specialistRoles.Count)];
            AvailableCandidates.Add(CreateRandomCandidate(randomRole, 0));
        }
        
        Debug.Log($"[HiringManager] Сгенерировано {AvailableCandidates.Count} новых кандидатов ({internsToCreate} стажеров, {specialistsToCreate} специалистов).");
    }

    private Candidate CreateRandomCandidate(StaffController.Role role, float experiencedChance)
    {
        Candidate candidate = new Candidate();
        candidate.Role = role;
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
        
        if (rankDatabase == null) 
        {
            Debug.LogError("RankDatabase не назначен в HiringManager!");
            return null;
        }

        if (role == StaffController.Role.Intern && Random.value < experiencedChance)
        {
            int targetRankLevel = Random.Range(1, 3); // 1 = Стажер 2-го разряда, 2 = Опытный стажер
            candidate.Rank = rankDatabase.FirstOrDefault(r => r.associatedRole == StaffController.Role.Intern && r.rankLevel == targetRankLevel);
        }
        else
        {
            // Находим ранг 0-го уровня для этой роли
            candidate.Rank = rankDatabase.FirstOrDefault(r => r.associatedRole == role && r.rankLevel == 0);
        }

        if (candidate.Rank != null)
        {
            candidate.Experience = candidate.Rank.experienceRequired;
        }
        
        float totalSkillPoints = candidate.Skills.paperworkMastery + candidate.Skills.sedentaryResilience +
                                 candidate.Skills.pedantry + candidate.Skills.softSkills;
        candidate.HiringCost = baseCost + (int)(totalSkillPoints * costPerSkillPoint);
        if (candidate.Rank != null) candidate.HiringCost += candidate.Rank.promotionCost; // Добавляем стоимость "обучения"

        candidate.Bio = ResumeGenerator.GenerateBio();
        return candidate;
    }

    public bool HireCandidate(Candidate candidate)
    {
        if (PlayerWallet.Instance.GetCurrentMoney() < candidate.HiringCost) return false;
        
        Transform freePoint = unassignedStaffPoints.FirstOrDefault(p => !occupiedPoints.ContainsKey(p));
        if (freePoint == null) return false;

        RoleData roleData = allRoleData.FirstOrDefault(data => data.roleType == candidate.Role);
        GameObject prefabToSpawn = GetPrefabForRole(candidate.Role); // Используем хелпер

        if (roleData == null || prefabToSpawn == null) 
        {
            Debug.LogError($"Не найдены данные или префаб для роли {candidate.Role}!");
            return false;
        }
        
        GameObject newStaffGO = Instantiate(prefabToSpawn, freePoint.position, Quaternion.identity);
        StaffController staffController = newStaffGO.GetComponent<StaffController>(); // Получаем базовый контроллер

        if (staffController != null)
        {
            staffController.characterName = candidate.Name;
            staffController.skills = candidate.Skills;
            staffController.gender = candidate.Gender;
            staffController.currentRank = candidate.Rank;
            staffController.experiencePoints = candidate.Experience;
			
			if (staffController.currentRank != null && staffController.currentRank.unlockedActions != null)
{
    // Назначаем действия, разблокированные начальным рангом
    staffController.activeActions = new List<StaffAction>(
        staffController.currentRank.unlockedActions
            .Where(action => action != null && action.category == ActionCategory.Tactic) // Убедимся, что добавляются только тактические действия
    );
    Debug.Log($"Назначено {staffController.activeActions.Count} стартовых действий для {staffController.characterName} на основе Ранга {staffController.currentRank.rankLevel} ({staffController.currentRank.rankName}).");
}
else
{
    staffController.activeActions = new List<StaffAction>(); // Убедимся, что список существует, даже если пуст
    Debug.LogWarning($"Стартовый ранг или его действия были null для {staffController.characterName}. Стартовые действия не назначены.");
}

            staffController.Initialize(roleData);
            
            // Назначаем системные действия
            var existingStaff = AllStaff.FirstOrDefault(s => s != null && s.systemActionDatabase != null);
            if (existingStaff != null)
            {
                staffController.systemActionDatabase = existingStaff.systemActionDatabase;
            }

            // Назначаем расписание по умолчанию
            if (ClientSpawner.Instance != null && ClientSpawner.Instance.mainCalendar != null)
            {
                staffController.workPeriods = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).ToList();
            }

            AllStaff.Add(staffController);
            UnassignedStaff.Add(staffController);
            newStaffGO.name = candidate.Name;
            occupiedPoints.Add(freePoint, staffController);
            AvailableCandidates.Remove(candidate);
            
            staffController.StartShift();
            Debug.Log($"Нанят сотрудник {candidate.Name}. Он немедленно приступает к работе.");
            return true;
        }

        Destroy(newStaffGO);
        PlayerWallet.Instance.AddMoney(candidate.HiringCost, Vector3.zero);
        return false;
    }
    
    private GameObject GetPrefabForRole(StaffController.Role role)
    {
        // TODO: В будущем здесь будет switch-case, возвращающий разные префабы
        // для охранника, уборщика и т.д. Пока все создаются из "универсального".
        return internPrefab;
    }
	
	public StaffController.Role GetRoleForDeskId(int deskId)
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
        foreach (var staff in AllStaff.ToList())
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