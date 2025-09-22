// Файл: HiringManager.cs

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

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
public void AssignNewRole(StaffController staff, StaffController.Role newRole)
{
    if (staff.currentRole == newRole) return;
    GameObject staffGO = staff.gameObject;
    Debug.Log($"Меняем роль {staff.characterName} с {staff.currentRole} на {newRole}");

    // --- Шаг 1: Находим нужную "должностную инструкцию" (RoleData) ---
    RoleData dataForNewRole = allRoleData.FirstOrDefault(data => data.roleType == newRole);
    if (dataForNewRole == null)
    {
        Debug.LogError($"Не найден RoleData для роли {newRole}! Операция прервана.");
        return;
    }

    // --- Шаг 2: Добавляем новые компоненты и получаем ссылку на новый "мозг" ---
    StaffController newController = null; // Переменная для хранения нового контроллера
    switch (newRole)
    {
        case StaffController.Role.Guard:
            newController = staffGO.AddComponent<GuardMovement>();
            staffGO.AddComponent<GuardNotification>();
            break;
        // ... (другие case'ы)
        case StaffController.Role.Janitor:
            newController = staffGO.AddComponent<ServiceWorkerController>();
            staffGO.AddComponent<ServiceWorkerNotification>();
            break;
    }
    
    staff.currentRole = newRole;

    // --- Шаг 3: Обновляем внешний вид и экипируем аксессуар ---
    var visuals = staffGO.GetComponent<CharacterVisuals>();
    if (visuals != null && newController != null)
    {
        // Эта логика пока что заглушка, так как spriteCollection и stateEmotionMap 
        // мы еще не перенесли на новые контроллеры. Мы это сделаем.
        // visuals.Setup(staff.gender, newController.spriteCollection, newController.stateEmotionMap);

        // А вот экипировка аксессуара уже будет работать!
        // Динамически получаем префаб аксессуара из нового контроллера
        var accessoryField = newController.GetType().GetField("accessoryPrefab");
        if (accessoryField != null)
        {
            GameObject accessoryToEquip = accessoryField.GetValue(newController) as GameObject;
            visuals.EquipAccessory(accessoryToEquip);
        }
        else
        {
            // Если у роли нет аксессуара, просто убираем старый
            visuals.EquipAccessory(null);
        }
    }
	
	// --- Шаг 4: Инициализируем новые компоненты данными из RoleData ---
    switch (newRole)
    {
        case StaffController.Role.Guard:
            staffGO.GetComponent<GuardMovement>()?.InitializeFromData(dataForNewRole);
            break;
        case StaffController.Role.Clerk: // Добавь сюда остальные роли клерков
		case StaffController.Role.Registrar:
		case StaffController.Role.Cashier:
		case StaffController.Role.Archivist:
            staffGO.GetComponent<ClerkController>()?.InitializeFromData(dataForNewRole);
            break;
        case StaffController.Role.Janitor:
            staffGO.GetComponent<ServiceWorkerController>()?.InitializeFromData(dataForNewRole);
            break;
    }

    // --- Шаг 5: Обновляем базовые данные и внешний вид (как и раньше) ---
    staff.currentRole = newRole;
    staff.GetComponent<CharacterVisuals>()?.EquipAccessory(dataForNewRole.accessoryPrefab);
    // И теперь вызываем Setup для CharacterVisuals, передавая данные из RoleData
    staff.GetComponent<CharacterVisuals>()?.Setup(staff.gender, dataForNewRole.spriteCollection, dataForNewRole.stateEmotionMap);

    Debug.Log($"Роль успешно изменена на {newRole}");
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