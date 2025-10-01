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
        StaffController[] existingStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
        if (existingStaff.Length > 0)
        {
            foreach (var staffMember in existingStaff)
            {
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

    public Coroutine AssignNewRole(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
    {
        if (staff.currentRole == newRole)
        {
            staff.activeActions = newActions;
            return null;
        }
        return StartCoroutine(RoleChangeRoutine(staff, newRole, newActions));
    }

    // --- ИСПРАВЛЕНО: Полностью обновленная корутина смены роли ---
    private IEnumerator RoleChangeRoutine(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
    {
        while (ScenePointsRegistry.Instance == null)
        {
            Debug.LogWarning($"HiringManager ждет, пока ScenePointsRegistry будет готов...");
            yield return null; 
        }
        if (staffBeingModified.Contains(staff))
        {
            Debug.LogWarning($"<color=orange>ОПЕРАЦИЯ ПРЕРВАНА:</color> Повторный вызов для {staff.characterName}.");
            yield break;
        }
        staffBeingModified.Add(staff);

        try
        {
            GameObject staffGO = staff.gameObject;
            Debug.Log($"<color=yellow>ОПЕРАЦИЯ:</color> Начинаем смену роли для {staff.characterName} с {staff.currentRole} на {newRole}");
            
            string savedName = staff.characterName;
            Gender savedGender = staff.gender;
            CharacterSkills savedSkills = staff.skills;
            int savedRank = staff.rank;
            int savedXP = staff.experiencePoints;
            int savedSalary = staff.salaryPerPeriod;
            bool savedPromoStatus = staff.isReadyForPromotion;
            List<string> savedWorkPeriods = new List<string>(staff.workPeriods);

            switch (staff.currentRole)
            {
                case StaffController.Role.Intern: if(staffGO.GetComponent<InternController>()) Destroy(staffGO.GetComponent<InternController>()); break;
                case StaffController.Role.Guard: if(staffGO.GetComponent<GuardMovement>()) Destroy(staffGO.GetComponent<GuardMovement>()); break;
                case StaffController.Role.Clerk: case StaffController.Role.Registrar: case StaffController.Role.Cashier: case StaffController.Role.Archivist: if(staffGO.GetComponent<ClerkController>()) Destroy(staffGO.GetComponent<ClerkController>()); break;
                case StaffController.Role.Janitor: if(staffGO.GetComponent<ServiceWorkerController>()) Destroy(staffGO.GetComponent<ServiceWorkerController>()); break;
            }

            yield return null;
            
            RoleData dataForNewRole = allRoleData.FirstOrDefault(data => data.roleType == newRole);
            if (dataForNewRole == null)
            {
                Debug.LogError($"Не найден RoleData для роли {newRole}! Операция прервана.");
                yield break;
            }

            switch (newRole)
            {
                case StaffController.Role.Guard: staffGO.AddComponent<GuardMovement>(); break;
                case StaffController.Role.Clerk: staffGO.AddComponent<ClerkController>(); break;
                case StaffController.Role.Janitor: staffGO.AddComponent<ServiceWorkerController>(); break;
                case StaffController.Role.Intern: staffGO.AddComponent<InternController>(); break;
            }

            yield return null;
            
            StaffController newControllerReference = staffGO.GetComponent<StaffController>();
            if (newControllerReference != null)
            {
                newControllerReference.characterName = savedName;
                newControllerReference.gender = savedGender;
                newControllerReference.skills = savedSkills;
                newControllerReference.rank = savedRank;
                newControllerReference.experiencePoints = savedXP;
                newControllerReference.salaryPerPeriod = savedSalary;
                newControllerReference.isReadyForPromotion = savedPromoStatus;
                
                newControllerReference.Initialize(dataForNewRole);

                if (newRole == StaffController.Role.Guard) (newControllerReference as GuardMovement)?.InitializeFromData(dataForNewRole);
                if (newRole == StaffController.Role.Clerk || newRole == StaffController.Role.Registrar || newRole == StaffController.Role.Cashier || newRole == StaffController.Role.Archivist)
                {
                    var clerk = (newControllerReference as ClerkController);
                    if (clerk != null)
                    {
                        clerk.InitializeFromData(dataForNewRole);
                        if (newRole == StaffController.Role.Registrar) clerk.role = ClerkController.ClerkRole.Registrar;
                        else if (newRole == StaffController.Role.Cashier) clerk.role = ClerkController.ClerkRole.Cashier;
                        else if (newRole == StaffController.Role.Archivist) clerk.role = ClerkController.ClerkRole.Archivist;
                        else clerk.role = ClerkController.ClerkRole.Regular;
                    }
                }
                if (newRole == StaffController.Role.Janitor) (newControllerReference as ServiceWorkerController)?.InitializeFromData(dataForNewRole);
                if (newRole == StaffController.Role.Intern) (newControllerReference as InternController)?.InitializeFromData(dataForNewRole);

                newControllerReference.workPeriods = savedWorkPeriods;
                newControllerReference.activeActions = newActions;

				if (newRole == StaffController.Role.Guard)
            {
                // Загружаем ассет действия из папки Resources
                var writeReportAction = Resources.Load<StaffAction>("Actions/Action_WriteReport");
                if (writeReportAction != null && !newControllerReference.activeActions.Contains(writeReportAction))
                {
                    // Добавляем действие в список, если его там еще нет
                    newControllerReference.activeActions.Add(writeReportAction);
                    Debug.Log($"Для охранника {newControllerReference.characterName} принудительно добавлено действие 'Написать протокол'.");
                }
            }


                int staffIndex = AllStaff.IndexOf(staff);
                if (staffIndex != -1)
                {
                    AllStaff[staffIndex] = newControllerReference;
                }
                
                Debug.Log($"<color=green>ОПЕРАЦИЯ УСПЕШНА:</color> Роль для {newControllerReference.characterName} успешно изменена.");
            }
            else
            {
                Debug.LogError($"<color=red>КРИТИЧЕСКАЯ ОШИБКА:</color> После смены роли не удалось найти компонент StaffController!");
            }
        }
        finally
        {
            staffBeingModified.Remove(staff);
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

        PlayerWallet.Instance.AddMoney(-candidate.HiringCost, Vector3.zero);
        
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