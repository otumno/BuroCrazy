// Файл: HiringManager.cs
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
    [Tooltip("Перетащите сюда универсальный префаб Стажёра (internPrefab)")]
    public GameObject internPrefab;
    
    [Header("Базы данных")]
	[Tooltip("Перетащите сюда все созданные ассеты RoleData")]
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

// --- НОВЫЙ МЕТОД ДЛЯ ЗАПУСКА AI ---
    public void ActivateAllScheduledStaff()
    {
        string currentPeriod = ClientSpawner.CurrentPeriodName;
        if (string.IsNullOrEmpty(currentPeriod)) return;

        Debug.Log($"<color=orange>ЗАПУСК AI:</color> Активация всех сотрудников, работающих в период '{currentPeriod}'...");

        foreach (var staff in AllStaff)
        {
            // Проверяем, должен ли сотрудник работать сейчас и не работает ли он уже
            if (staff.workPeriods.Contains(currentPeriod) && !staff.IsOnDuty())
            {
                staff.StartShift();
            }
        }
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
            RegisterExistingStaff();
        }
    }

    public void PromoteStaff(StaffController staff)
    {
        if (staff == null || !staff.isReadyForPromotion)
        {
            Debug.LogWarning($"Попытка повысить сотрудника {staff?.characterName}, который не готов к повышению.");
            return;
        }

        RankData nextRankData = ExperienceManager.Instance.rankDatabase
            .FirstOrDefault(r => r.rankLevel == staff.rank + 1);
        if (nextRankData == null)
        {
            Debug.LogWarning($"Не найдены данные для следующего ранга после {staff.rank}. Возможно, сотрудник уже достиг максимума.");
            return;
        }
    
        if (PlayerWallet.Instance.GetCurrentMoney() < nextRankData.promotionCost)
        {
            Debug.Log($"Недостаточно денег для повышения. Нужно: ${nextRankData.promotionCost}");
            return;
        }

        PlayerWallet.Instance.AddMoney(-nextRankData.promotionCost, Vector3.zero);
        staff.rank = nextRankData.rankLevel;
        staff.salaryPerPeriod = (int)(staff.salaryPerPeriod * nextRankData.salaryMultiplier);
        staff.isReadyForPromotion = false;

        Debug.Log($"<color=green>Сотрудник {staff.characterName} повышен до ранга '{nextRankData.rankName}'! Новая З/П: ${staff.salaryPerPeriod}</color>");
    }

    private void RegisterExistingStaff()
{
    Debug.Log("[HiringManager] Запущена регистрация существующих сотрудников...");
    AllStaff.Clear();
    // Находим всех StaffController на сцене
    StaffController[] existingStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
    if (existingStaff.Length > 0)
    {
        foreach (var staffMember in existingStaff)
        {
            // >>> ИЗМЕНЕНИЕ: Игнорируем Директора при подсчете <<<
            if (staffMember is DirectorAvatarController)
            {
                continue; // Пропускаем итерацию, если это Директор
            }
            AllStaff.Add(staffMember);
        }
    }
    Debug.Log($"[HiringManager] Регистрация завершена. Найдено и учтено: {AllStaff.Count} сотрудников.");
}

    public void ResetState()
    {
        AvailableCandidates.Clear();
        occupiedPoints.Clear();
        UnassignedStaff.Clear();
        AllStaff.Clear();
        Debug.Log("[HiringManager] Состояние сброшено для новой игры.");
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
            Debug.LogError("[HiringManager] НЕ УДАЛОСЬ найти InternPointsRegistry на сцене GameScene! Наем новых сотрудников не будет работать.");
        }
    }

    public void AssignNewRole_Immediate(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
{
    if (staff == null)
    {
        Debug.LogError("Попытка сменить роль у несуществующего сотрудника (staff is null)!");
        return;
    }

    // Проверка, чтобы не запускать смену роли для одного и того же сотрудника дважды
    if (staffBeingModified.Contains(staff))
    {
        Debug.LogWarning($"<color=orange>ОПЕРАЦИЯ ПРЕРВАНА:</color> Повторный вызов смены роли для {staff.characterName}.");
        return;
    }
    staffBeingModified.Add(staff);

    try
    {
        GameObject staffGO = staff.gameObject;
        Debug.Log($"<color=yellow>ОПЕРАЦИЯ (НЕМЕДЛЕННО):</color> Начинаем смену роли для {staff.characterName} с {staff.currentRole} на {newRole}");

        // 1. Сохраняем все данные и ссылки
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
        
        CharacterVisuals visualsComponent = staffGO.GetComponent<CharacterVisuals>();
        AgentMover agentMoverComponent = staffGO.GetComponent<AgentMover>();
        CharacterStateLogger loggerComponent = staffGO.GetComponent<CharacterStateLogger>();

        // 2. Уничтожаем СТАРЫЙ компонент-контроллер
        Destroy(staff);

        // 3. Добавляем НОВЫЙ компонент-контроллер
        StaffController newControllerReference = null;
        switch (newRole)
        {
            case StaffController.Role.Guard: newControllerReference = staffGO.AddComponent<GuardMovement>(); break;
            case StaffController.Role.Clerk:
            case StaffController.Role.Registrar:
            case StaffController.Role.Cashier:
            case StaffController.Role.Archivist: newControllerReference = staffGO.AddComponent<ClerkController>(); break;
            case StaffController.Role.Janitor: newControllerReference = staffGO.AddComponent<ServiceWorkerController>(); break;
            case StaffController.Role.Intern: newControllerReference = staffGO.AddComponent<InternController>(); break;
        }

        if (newControllerReference == null)
        {
            Debug.LogError($"<color=red>КРИТИЧЕСКАЯ ОШИБКА:</color> После смены роли не удалось добавить новый компонент-контроллер для роли {newRole}!");
            return;
        }
        
        // 4. Восстанавливаем все данные в новом контроллере
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
        
        newControllerReference.ForceInitializeBaseComponents(agentMoverComponent, visualsComponent, loggerComponent);
        
        RoleData dataForNewRole = allRoleData.FirstOrDefault(data => data.roleType == newRole);
        if (dataForNewRole != null)
        {
            newControllerReference.Initialize(dataForNewRole);

            // Инициализация специфичных для роли данных
            if (newControllerReference is GuardMovement newGuard) newGuard.InitializeFromData(dataForNewRole);
            if (newControllerReference is ServiceWorkerController newWorker) newWorker.InitializeFromData(dataForNewRole);
            if (newControllerReference is InternController newIntern) newIntern.InitializeFromData(dataForNewRole);
            if (newControllerReference is ClerkController newClerk)
            {
                newClerk.InitializeFromData(dataForNewRole);
                newClerk.allRoleData = this.allRoleData;
                if (newRole == StaffController.Role.Registrar) newClerk.role = ClerkController.ClerkRole.Registrar;
                else if (newRole == StaffController.Role.Cashier) newClerk.role = ClerkController.ClerkRole.Cashier;
                else if (newRole == StaffController.Role.Archivist) newClerk.role = ClerkController.ClerkRole.Archivist;
                else newClerk.role = ClerkController.ClerkRole.Regular;
            }
        }
        
        // 5. Обновляем ссылку в главном списке сотрудников
        int staffIndex = AllStaff.FindIndex(s => s == null || s.gameObject == staffGO);
        if (staffIndex != -1)
        {
            AllStaff[staffIndex] = newControllerReference;
        }
        else
        {
            Debug.LogWarning($"Не удалось найти старую ссылку на {savedName} в списке AllStaff. Возможно, он уже был удален. Добавляем заново.");
            AllStaff.Add(newControllerReference);
        }
        
        Debug.Log($"<color=green>ОПЕРАЦИЯ УСПЕШНА:</color> Роль для {newControllerReference.characterName} успешно изменена.");
    }
    finally
    {
        // В конце операции убираем сотрудника из списка "изменяемых"
        var staffToRemove = staffBeingModified.FirstOrDefault(s => s?.gameObject == staff.gameObject);
        if (staffToRemove != null)
        {
            staffBeingModified.Remove(staffToRemove);
        }
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

    // --- ИСПРАВЛЕНО: Полностью обновленный метод найма ---
    public bool HireCandidate(Candidate candidate)
    {
        if (PlayerWallet.Instance.GetCurrentMoney() < candidate.HiringCost)
        {
            Debug.Log("Недостаточно средств для найма!");
            return false;
        }

        Transform freePoint = unassignedStaffPoints.FirstOrDefault(p => !occupiedPoints.ContainsKey(p));
        if (freePoint == null)
        {
            Debug.Log("Нет свободных мест в кабинете директора для новых стажеров!");
            return false;
        }

        RoleData internRoleData = allRoleData.FirstOrDefault(data => data.roleType == StaffController.Role.Intern);
        if (internRoleData == null)
        {
            Debug.LogError("В HiringManager не найден RoleData для роли Intern! Найм невозможен.");
            return false;
        }

        PlayerWallet.Instance.AddMoney(-candidate.HiringCost, $"Найм: {candidate.Name}");
        
        GameObject newStaffGO = Instantiate(internPrefab, freePoint.position, Quaternion.identity);
        
        InternController internController = newStaffGO.GetComponent<InternController>();
        if (internController != null)
        {
            internController.characterName = candidate.Name;
            internController.skills = candidate.Skills;
            internController.gender = candidate.Gender;
            
            internController.Initialize(internRoleData);
            
            AllStaff.Add(internController);
            UnassignedStaff.Add(internController);
            newStaffGO.name = candidate.Name;
            occupiedPoints.Add(freePoint, internController);
            AvailableCandidates.Remove(candidate);
            
            internController.StartShift();
            
            Debug.Log($"Нанят сотрудник {candidate.Name} за ${candidate.HiringCost}. Он немедленно приступает к работе.");
            return true;
        }

        Destroy(newStaffGO);
        PlayerWallet.Instance.AddMoney(candidate.HiringCost, Vector3.zero);
        return false;
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
}