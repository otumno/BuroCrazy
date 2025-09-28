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
    [Tooltip("Перетащите сюда универсальный префаб Стажёра (InternPrefab)")]
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

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Мы будем запускать поиск, только если загружена основная игровая сцена
        if (scene.name == "GameScene") // <-- Убедись, что имя твоей сцены здесь верное
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

    // Находим данные для СЛЕДУЮЩЕГО ранга
    RankData nextRankData = ExperienceManager.Instance.rankDatabase
        .FirstOrDefault(r => r.rankLevel == staff.rank + 1);

    if (nextRankData == null)
    {
        Debug.LogWarning($"Не найдены данные для следующего ранга после {staff.rank}. Возможно, сотрудник уже достиг максимума.");
        return;
    }
    
    // Проверяем, хватает ли денег
    if (PlayerWallet.Instance.GetCurrentMoney() < nextRankData.promotionCost)
    {
        Debug.Log($"Недостаточно денег для повышения. Нужно: ${nextRankData.promotionCost}");
        // Тут можно показать игроку сообщение об ошибке
        return;
    }

    // Списываем деньги
    PlayerWallet.Instance.AddMoney(-nextRankData.promotionCost, Vector3.zero);

    // Обновляем данные сотрудника
    staff.rank = nextRankData.rankLevel;
    staff.salaryPerPeriod = (int)(staff.salaryPerPeriod * nextRankData.salaryMultiplier); // Можно сделать и базовую ставку + бонус
    staff.isReadyForPromotion = false; // Сбрасываем флаг

    Debug.Log($"<color=green>Сотрудник {staff.characterName} повышен до ранга '{nextRankData.rankName}'! Новая З/П: ${staff.salaryPerPeriod}</color>");
}




    /// <summary>
    /// Находит всех сотрудников, уже размещенных на сцене, и регистрирует их в системе.
    /// </summary>
    private void RegisterExistingStaff()
    {
        Debug.Log("[HiringManager] Запущена регистрация существующих сотрудников...");
        
        // Очищаем список на случай перезагрузки сцены, чтобы избежать дубликатов
        AllStaff.Clear();

        // Находим всех персонажей со скриптом StaffController (и его наследниками) на сцене
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

/// <summary>
/// "Пересадка мозга": меняет роль сотрудника, удаляя старые и добавляя новые компоненты поведения.
/// </summary>
/// <summary>
/// "Пересадка мозга": меняет роль сотрудника, удаляя старые и добавляя новые компоненты поведения.
/// </summary>
public Coroutine AssignNewRole(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
{
    // Если роль не меняется, просто обновляем список действий и выходим.
    // Это быстрая операция, не требующая сложной корутины.
    if (staff.currentRole == newRole)
    {
        staff.activeActions = newActions;
        staff.RefreshAIState(); 
        return null;
    }
    
    // Если роль действительно меняется, запускаем полную корутину "пересадки мозга".
    return StartCoroutine(RoleChangeRoutine(staff, newRole, newActions));
}

// А это сама "операционная", где происходит вся магия.
private IEnumerator RoleChangeRoutine(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
{
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
        
        // 1. Сохраняем все данные, КРОМЕ списка действий
        string savedName = staff.characterName;
        Gender savedGender = staff.gender;
        CharacterSkills savedSkills = staff.skills;
        int savedRank = staff.rank;
        int savedXP = staff.experiencePoints;
        int savedSalary = staff.salaryPerPeriod;
        bool savedPromoStatus = staff.isReadyForPromotion;
        List<string> savedWorkPeriods = new List<string>(staff.workPeriods);

        // 2. Уничтожаем старый компонент-контроллер
        switch (staff.currentRole)
        {
            case StaffController.Role.Intern: if(staffGO.GetComponent<InternController>()) Destroy(staffGO.GetComponent<InternController>()); break;
            case StaffController.Role.Guard: if(staffGO.GetComponent<GuardMovement>()) Destroy(staffGO.GetComponent<GuardMovement>()); break;
            case StaffController.Role.Clerk: case StaffController.Role.Registrar: case StaffController.Role.Cashier: case StaffController.Role.Archivist: if(staffGO.GetComponent<ClerkController>()) Destroy(staffGO.GetComponent<ClerkController>()); break;
            case StaffController.Role.Janitor: if(staffGO.GetComponent<ServiceWorkerController>()) Destroy(staffGO.GetComponent<ServiceWorkerController>()); break;
        }

        yield return null; 

        // 3. Добавляем новый компонент-контроллер
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

        // 4. Находим ссылку на НОВЫЙ контроллер и "вдыхаем" в него жизнь
        StaffController newControllerReference = staffGO.GetComponent<StaffController>();
        if (newControllerReference != null)
        {
            // Инициализируем его данными из RoleData (униформа, параметры)
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

            // Восстанавливаем ВСЕ сохраненные данные
            newControllerReference.characterName = savedName;
            newControllerReference.gender = savedGender;
            newControllerReference.skills = savedSkills;
            newControllerReference.rank = savedRank;
            newControllerReference.experiencePoints = savedXP;
            newControllerReference.salaryPerPeriod = savedSalary;
            newControllerReference.isReadyForPromotion = savedPromoStatus;
            newControllerReference.workPeriods = savedWorkPeriods;
            
            // Напрямую присваиваем НОВЫЙ список действий
            newControllerReference.activeActions = newActions;
            
            newControllerReference.currentRole = newRole;

            // Обновляем ссылку на контроллер в главном списке сотрудников
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
        // Вне зависимости от успеха или провала, убираем сотрудника из списка "модифицируемых"
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
		
		// --- ДОБАВЛЕНО: Генерация новых данных ---
    candidate.Bio = ResumeGenerator.GenerateBio();

    // Резервируем логику для уникальных действий (пока просто выводим в лог)
    if (Random.value < 0.2f) // 20% шанс на уникальное действие
    {
        // TODO: Найти в ActionDatabase случайное уникальное действие и добавить в candidate.UniqueActionsPool
        Debug.Log($"Сгенерирован кандидат {candidate.Name} с потенциальным уникальным навыком!");
    }

        return candidate;
    }

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
    
    // Списываем деньги за найм
    PlayerWallet.Instance.AddMoney(-candidate.HiringCost, Vector3.zero);
    
    // Создаем сотрудника на свободной точке
    GameObject newStaffGO = Instantiate(internPrefab, freePoint.position, Quaternion.identity);
    
    StaffController staffController = newStaffGO.GetComponent<StaffController>();
    if (staffController != null)
    {
        // Передаем все данные от кандидата новому сотруднику
        staffController.characterName = candidate.Name;
        staffController.skills = candidate.Skills;
        staffController.gender = candidate.Gender;
        
        // Регистрируем его во всех системах
        AllStaff.Add(staffController);
        UnassignedStaff.Add(staffController); // Если есть отдельная логика для "неназначенных"
        newStaffGO.name = candidate.Name;
        occupiedPoints.Add(freePoint, staffController);
        AvailableCandidates.Remove(candidate);
        
        // --- ГЛАВНОЕ ИЗМЕНЕНИЕ: Отправляем новичка на смену! ---
        staffController.StartShift();

        Debug.Log($"Нанят сотрудник {candidate.Name} за ${candidate.HiringCost}. Он немедленно приступает к работе.");
        return true;
    }

    // Если что-то пошло не так
    Destroy(newStaffGO); // Удаляем созданный объект, чтобы не мусорить
    PlayerWallet.Instance.AddMoney(candidate.HiringCost, Vector3.zero); // Возвращаем деньги
    return false;
}

    public void FireStaff(StaffController staffToFire)
{
    if (staffToFire == null) return;
    
    Debug.Log($"Отдана команда на увольнение сотрудника {staffToFire.characterName}!");

    // Немедленно удаляем из списков, чтобы он пропал из UI
    if(AllStaff.Contains(staffToFire)) AllStaff.Remove(staffToFire);
    if(UnassignedStaff.Contains(staffToFire)) UnassignedStaff.Remove(staffToFire);

    // Если сотрудник занимал место в кабинете, освобождаем его
    if (occupiedPoints.ContainsValue(staffToFire))
    {
        Transform pointToFree = occupiedPoints.FirstOrDefault(kvp => kvp.Value == staffToFire).Key;
        if (pointToFree != null)
        {
            occupiedPoints.Remove(pointToFree);
        }
    }
    
    // --- ИЗМЕНЕНИЕ: Отдаем команду уйти домой вместо мгновенного удаления ---
    staffToFire.FireAndGoHome();
}
}