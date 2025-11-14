// Файл: Assets/Scripts/Managers/HiringManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Collections; // Required for IEnumerator

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
    [Tooltip("Перетащите сюда все ассеты RankData")]
    public List<RankData> rankDatabase; // This should be List<RankData>

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
    // Note: baseCost might be less relevant now with baseHiringCost in RoleData
    public int baseCost = 100;
    public int costPerSkillPoint = 150;

    // --- Списки Имен ---
    private List<string> firstNamesMale = new List<string>
    {
        "Аркадий", "Иннокентий", "Пантелеймон", "Акакий", "Евграф", "Порфирий", "Лука", "Фома", "Прохор", "Варфоломей", "Ипполит", "Модест", "Савва", "Корней", "Никанор", "Афанасий", "Ефим", "Игнат", "Аполлон", "Власий", "Захар", "Климент", "Лаврентий", "Макар", "Поликарп"
    };
    private List<string> firstNamesFemale = new List<string>
    {
        "Аглая", "Евпраксия", "Пелагея", "Серафима", "Фёкла", "Глафира", "Агриппина", "Василиса", "Ефросинья", "Ираида", "Марфа", "Прасковья", "Акулина", "Матрёна", "Степанида", "Анфиса", "Зинаида", "Варвара", "Авдотья", "Евдокия", "Изольда", "Олимпиада", "Пульхерия", "Феврония"
    };
    private List<string> lastNames = new List<string>
    {
        "Перепискин", "Протоколов", "Архивариусов", "Гербовый", "Подшивайлов", "Нумеров", "Формуляров", "Циркуляров", "Бланков", "Скрепкин", "Печаткин", "Резолюцин", "Визируйко", "Канцелярский", "Копиркин", "Чернилов", "Аттестатов", "Докладов", "Входящий", "Исходящий", "Журналов", "Приказов", "Указов", "Описин"
    };
    private List<string> patronymicsMale = new List<string>
    {
        "Аркадьевич", "Иннокентьевич", "Пантелеймонович", "Акакиевич", "Евграфович", "Порфирьевич", "Лукич", "Фомич", "Прохорович", "Варфоломеевич", "Ипполитович", "Модестович", "Саввич", "Корнеевич", "Никанорович", "Афанасьевич", "Ефимович", "Игнатьевич", "Аполлонович", "Власьевич", "Захарович", "Климентович", "Лаврентьевич", "Макарович"
    };
    private List<string> patronymicsFemale = new List<string>
    {
        "Аркадьевна", "Иннокентьевна", "Пантелеймоновна", "Акакиевна", "Евграфовна", "Порфирьевна", "Лукинична", "Фоминична", "Прохоровна", "Варфоломеевна", "Ипполитовна", "Модестовна", "Саввична", "Корнеевна", "Никаноровна", "Афанасьевна", "Ефимовна", "Игнатьевна", "Аполлоновна", "Власьевна", "Захаровна", "Климентовна", "Лаврентьевна", "Макаровна"
    };
    // --- Конец Списков Имен ---

    public List<Candidate> AvailableCandidates { get; private set; } = new List<Candidate>();
    // List to track staff currently undergoing component rebuild to prevent race conditions
    private List<StaffController> staffBeingModified = new List<StaffController>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // --- <<< ИЗМЕНЕНИЕ ЗДЕСЬ >>> ---
            // УБИРАЕМ строки, которые вызывают ошибку
            // transform.SetParent(null); 
            // DontDestroyOnLoad(gameObject); 
            // --- <<< КОНЕЦ ИЗМЕНЕНИЯ >>> ---
            
            Debug.Log($"<color=green>[HiringManager]</color> Awake: Я стал Singleton. Объект 'gameObject' будет сделан бессмертным (через родителя).");
            SceneManager.sceneLoaded += OnSceneLoaded; 
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[HiringManager] Awake: Найден дубликат. Уничтожаю *себя* (этот компонент).");
            
            Destroy(this); // Уничтожаем дубликат скрипта
        }
    }

    // ... (весь остальной код HiringManager.cs без изменений) ...

    void OnDestroy()
    {
        // Unsubscribe only if this was the singleton instance
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Check if the loaded scene is the main game scene
        if (scene.name == "GameScene") // Use your actual game scene name here
        {
            Debug.Log("[HiringManager] GameScene loaded. Finding references and registering staff...");
            FindSceneSpecificReferences();
            // Use StartCoroutine to delay registration slightly, ensuring other managers might be ready
            StartCoroutine(RegisterExistingStaffAndAssignDatabases());
        }
        else
        {
            Debug.Log($"[HiringManager] Loaded scene: {scene.name}. Clearing scene-specific references.");
            // Clear scene-specific references if loading a different scene (e.g., main menu)
            unassignedStaffPoints.Clear();
            occupiedPoints.Clear();
            // Decide if AllStaff/UnassignedStaff should persist based on game design
            // If they should only exist in GameScene, clear them here:
            // AllStaff.Clear();
            // UnassignedStaff.Clear();
            // staffBeingModified.Clear();
        }
    }

    private IEnumerator RegisterExistingStaffAndAssignDatabases()
    {
        // Wait until the end of the frame allows other objects' Awake/Start to run
        yield return new WaitForEndOfFrame();
        Debug.Log("[HiringManager] RegisterExistingStaff: Frame end reached. Starting registration...");

        AllStaff.Clear(); // Ensure list is clean before adding current staff
        StaffController[] existingStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
        Debug.Log($"[HiringManager] Found {existingStaff.Length} StaffController components in the scene.");

        ActionDatabase systemActions = null;
        // Attempt to find the system action database from the first valid staff member (excluding Director)
        var firstValidStaff = existingStaff.FirstOrDefault(s => s != null && !(s is DirectorAvatarController));
        if (firstValidStaff != null)
        {
            systemActions = firstValidStaff.systemActionDatabase;
            if (systemActions == null)
            {
                Debug.LogWarning($"Первый найденный сотрудник {firstValidStaff.name} не имеет systemActionDatabase.");
            }
        }
        else
        {
            Debug.LogWarning("Не найдено ни одного сотрудника (кроме Директора) для получения systemActionDatabase. Системные действия могут не работать.");
        }

        // Iterate through found staff controllers
        foreach (var staffMember in existingStaff)
        {
            if (staffMember == null)
            {
                 Debug.LogWarning("Найден null StaffController в массиве existingStaff.");
                 continue;
            }
            if (staffMember is DirectorAvatarController)
            {
                 Debug.Log("Пропуск регистрации DirectorAvatarController.");
                 continue; // Skip the director avatar
            }

            // Add to the main list
            if (!AllStaff.Contains(staffMember)) // Avoid duplicates if coroutine runs multiple times somehow
            {
                AllStaff.Add(staffMember);
                 Debug.Log($"Зарегистрирован сотрудник: {staffMember.characterName} (Type: {staffMember.GetType().Name})");

                // Assign the system action database if it's missing
                if (staffMember.systemActionDatabase == null && systemActions != null)
                {
                    staffMember.systemActionDatabase = systemActions;
                    Debug.Log($"-> Назначена база системных действий.");
                }
                else if (staffMember.systemActionDatabase == null && systemActions == null)
                {
                    Debug.LogWarning($"-> Не удалось назначить базу системных действий - база не найдена.");
                }

                 // Ensure essential component references are linked
                 var agentMover = staffMember.GetComponent<AgentMover>();
                 var visuals = staffMember.GetComponent<CharacterVisuals>();
                 var logger = staffMember.GetComponent<CharacterStateLogger>();
                 if (agentMover == null || visuals == null || logger == null) {
                     Debug.LogError($"-> Ошибка: У сотрудника {staffMember.characterName} отсутствуют базовые компоненты (AgentMover/Visuals/Logger)!");
                 } else {
                     staffMember.ForceInitializeBaseComponents(agentMover, visuals, logger);
                 }


                 // Optionally re-initialize visuals based on RoleData if needed after scene load/editor changes
                 // RoleData currentRoleData = allRoleData.FirstOrDefault(data => data != null && data.roleType == staffMember.currentRole);
                 // if (currentRoleData != null && visuals != null) {
                 //     visuals.SetupFromRoleData(currentRoleData, staffMember.gender);
                 // }
            } else {
                 Debug.LogWarning($"Сотрудник {staffMember.characterName} уже был в списке AllStaff.");
            }
        }
        Debug.Log($"[HiringManager] Регистрация завершена. Всего в AllStaff: {AllStaff.Count} сотрудников.");
    }


    public Coroutine AssignNewRole_Immediate(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
    {
        if (staff == null)
        {
             Debug.LogError("AssignNewRole_Immediate: Попытка сменить роль для null сотрудника!");
             return null;
        }
        // Ensure newActions list is not null
        List<StaffAction> actionsToAssign = newActions ?? new List<StaffAction>();

        Debug.Log($"<color=yellow>ОПЕРАЦИЯ:</color> Начинаем смену роли для {staff.characterName} ({staff.GetInstanceID()}) с {staff.currentRole} на {newRole}. Новых действий: {actionsToAssign.Count}");

        // Assign actions immediately, regardless of rebuild, make a copy
        staff.activeActions = new List<StaffAction>(actionsToAssign);

        System.Type requiredControllerType = GetControllerTypeForRole(newRole);
        System.Type currentControllerType = staff.GetType();

        if (requiredControllerType == null) {
            Debug.LogError($"Не удалось определить тип контроллера для роли {newRole}. Смена роли отменена.");
            return null;
        }


        if (requiredControllerType == currentControllerType)
        {
            // If the controller type is the same, just update the role properties
            staff.currentRole = newRole;
            if (staff is ClerkController clerk)
            {
                clerk.role = GetClerkRoleFromStaffRole(newRole);
            }
            Debug.Log($"<color=green>ОПЕРАЦИЯ УСПЕШНА:</color> Роль для {staff.characterName} обновлена до {newRole} без пересоздания компонента.");
            return null; // No coroutine needed
        }
        else
        {
            // If controller type needs to change, start the rebuild coroutine
            Debug.Log($"Требуется пересборка компонента для {staff.characterName}: {currentControllerType.Name} -> {requiredControllerType.Name}.");
            // Pass a copy of the actions list to the rebuild coroutine
            return StartCoroutine(RebuildControllerComponent(staff, newRole, new List<StaffAction>(actionsToAssign)));
        }
    }


    public IEnumerator RebuildControllerComponent(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
    {
         if (staff == null) {
              Debug.LogError("[RebuildComponent] Попытка пересборки null сотрудника!");
              yield break;
         }
         // Prevent concurrent modification if already rebuilding this staff member
         if (staffBeingModified.Contains(staff))
         {
             Debug.LogWarning($"[RebuildComponent] Попытка пересборки {staff.name} ({staff.GetInstanceID()}), который уже в процессе.");
             yield break;
         }
         staffBeingModified.Add(staff);

         GameObject staffGO = staff.gameObject; // Store reference to the GameObject
         int staffInstanceID = staffGO.GetInstanceID(); // Store Instance ID for later lookup
         string staffNameForLogs = staff.characterName ?? staffGO.name; // Use character name if available

         // --- Move yield statements BEFORE the try block ---
         yield return new WaitForEndOfFrame(); // Wait before destruction
         yield return null; // Wait another frame
         // --- End yield move ---

         StaffController newControllerReference = null; // Declare here for broader scope

         try
         {
            Debug.Log($"<color=orange>ПЕРЕСБОРКА КОМПОНЕНТА:</color> для {staffNameForLogs} (ID: {staffInstanceID}, GO: {staffGO.name})");

            // --- Save Data ---
            string savedName = staff.characterName;
            Gender savedGender = staff.gender;
            CharacterSkills savedSkills = staff.skills;
            RankData savedRank = staff.currentRank;
            int savedXP = staff.experiencePoints;
            int savedSalary = staff.salaryPerPeriod;
            int savedUnpaidPeriods = staff.unpaidPeriods;
            int savedMissedPayments = staff.missedPaymentCount;
            List<string> savedWorkPeriods = new List<string>(staff.workPeriods ?? new List<string>()); // Handle null list
            ServicePoint savedWorkstation = staff.assignedWorkstation;
            ActionDatabase savedSystemDb = staff.systemActionDatabase;
            // --- End Save Data ---

            // --- Save Component References ---
            AgentMover agentMover = staffGO.GetComponent<AgentMover>();
            CharacterVisuals visuals = staffGO.GetComponent<CharacterVisuals>();
            CharacterStateLogger logger = staffGO.GetComponent<CharacterStateLogger>();
            if (agentMover == null || visuals == null || logger == null) {
                 Debug.LogError($"Критическая ошибка перед уничтожением {staffNameForLogs}: Отсутствуют базовые компоненты (Mover/Visuals/Logger)!");
                 // Decide how to handle this - potentially stop the process?
            }
            // --- End Save Components ---

            // --- Destroy Old Component ---
            Debug.Log($"Уничтожение старого компонента {staff.GetType().Name} у {staffNameForLogs}...");
            Object.DestroyImmediate(staff); // Use DestroyImmediate for safety within coroutine/editor context? Check implications. Or just Destroy(staff).
            staff = null; // Invalidate the old reference immediately
             // It might be safer to wait a frame *after* destruction too
             // yield return null;
            // --- End Destroy ---

             // Safety check after destroy
             if (staffGO.GetComponent<StaffController>() != null) {
                  Debug.LogError($"Старый StaffController {staffGO.GetComponent<StaffController>().GetType().Name} не был уничтожен у {staffNameForLogs}!");
             }


            // --- Add New Component ---
            System.Type newControllerType = GetControllerTypeForRole(newRole);
            if(newControllerType != null)
            {
                Debug.Log($"Добавление нового компонента {newControllerType.Name} для {staffNameForLogs}...");
                Component addedComponent = staffGO.AddComponent(newControllerType);
                newControllerReference = addedComponent as StaffController;
                if (newControllerReference == null) {
                     Debug.LogError($"Не удалось привести добавленный компонент ({addedComponent?.GetType().Name}) к StaffController для {staffNameForLogs}!");
                }
            } else {
                 Debug.LogError($"Не удалось определить тип контроллера для роли {newRole}. Невозможно добавить компонент для {staffNameForLogs}.");
            }
            // --- End Add New ---

            if (newControllerReference == null)
            {
                Debug.LogError($"<color=red>КРИТИЧЕСКАЯ ОШИБКА:</color> Не удалось создать новый компонент контроллера для роли {newRole} на {staffNameForLogs}!");
                // Attempt cleanup or recovery if possible
                yield break; // Exit coroutine
            }

             Debug.Log($"Новый контроллер {newControllerReference.GetType().Name} добавлен. Восстановление данных для {savedName}...");

            // --- Restore Saved Data ---
            newControllerReference.characterName = savedName;
            newControllerReference.gender = savedGender;
            newControllerReference.skills = savedSkills;
            newControllerReference.currentRank = savedRank;
            newControllerReference.experiencePoints = savedXP;
            newControllerReference.salaryPerPeriod = savedSalary;
            newControllerReference.unpaidPeriods = savedUnpaidPeriods;
            newControllerReference.missedPaymentCount = savedMissedPayments;
            newControllerReference.workPeriods = savedWorkPeriods;
            newControllerReference.activeActions = newActions; // Assign the (potentially new) list of actions
            newControllerReference.systemActionDatabase = savedSystemDb;
            // --- End Restore Data ---

            // --- Re-assign Workstation ---
            if (savedWorkstation != null && AssignmentManager.Instance != null)
            {
                AssignmentManager.Instance.AssignStaffToWorkstation(newControllerReference, savedWorkstation);
                 Debug.Log($"Рабочее место {savedWorkstation.name} восстановлено для {savedName}.");
            } else {
                 newControllerReference.assignedWorkstation = null;
                 if (savedWorkstation != null) Debug.LogWarning($"AssignmentManager не найден, не удалось восстановить рабочее место для {savedName}.");
            }
            // --- End Workstation ---

            // --- Link Components ---
             // Ensure components were found earlier
            if (agentMover != null && visuals != null && logger != null) {
                 newControllerReference.ForceInitializeBaseComponents(agentMover, visuals, logger);
                 Debug.Log($"Базовые компоненты связаны для {savedName}.");
            } else {
                 Debug.LogError($"Не удалось связать базовые компоненты для {savedName} - ссылки потеряны!");
            }
            // --- End Link ---

            // --- Role Specific Init ---
            RoleData dataForNewRole = allRoleData?.FirstOrDefault(data => data != null && data.roleType == newRole);
            if (dataForNewRole != null)
            {
                newControllerReference.Initialize(dataForNewRole); // Base StaffController init
                // Specific controller initializers
                if (newControllerReference is GuardMovement newGuard) newGuard.InitializeFromData(dataForNewRole);
                else if (newControllerReference is ServiceWorkerController newWorker) newWorker.InitializeFromData(dataForNewRole);
                else if (newControllerReference is InternController newIntern) newIntern.InitializeFromData(dataForNewRole);
                else if (newControllerReference is ClerkController newClerk)
                {
                    newClerk.role = GetClerkRoleFromStaffRole(newRole);
                    newClerk.allRoleData = this.allRoleData;
                }
                 Debug.Log($"Специфичная инициализация для роли {newRole} выполнена для {savedName}.");
            } else {
                 Debug.LogError($"RoleData для роли {newRole} не найдена! Невозможно выполнить специфичную инициализацию для {savedName}.");
            }
            // --- End Role Specific ---

            // --- Update Manager Lists ---
             // Find by Instance ID
             bool updatedInAllStaff = false;
            for(int i = 0; i < AllStaff.Count; i++) {
                if (AllStaff[i] == null || AllStaff[i].gameObject.GetInstanceID() == staffInstanceID) {
                     AllStaff[i] = newControllerReference;
                    updatedInAllStaff = true;
                    break;
                }
            }
             if (!updatedInAllStaff) {
                 // If somehow it wasn't found (e.g., removed between yield calls), add it back.
                 if (!AllStaff.Contains(newControllerReference)) {
                     Debug.LogWarning($"Сотрудник {savedName} не найден в AllStaff после пересборки (ID: {staffInstanceID}), добавляем заново.");
                     AllStaff.Add(newControllerReference);
                 }
            }

            bool updatedInUnassigned = false;
             for(int i = 0; i < UnassignedStaff.Count; i++) {
                 if (UnassignedStaff[i] == null || UnassignedStaff[i].gameObject.GetInstanceID() == staffInstanceID) {
                     UnassignedStaff[i] = newControllerReference;
                     updatedInUnassigned = true;
                     break;
                 }
             }
             // No need to add to UnassignedStaff if not found, it implies it was assigned.

             Debug.Log($"Ссылки в списках менеджера обновлены для {savedName}.");
            // --- End Update Lists ---

            Debug.Log($"<color=green>ПЕРЕСБОРКА УСПЕШНА:</color> Роль для {newControllerReference.characterName} полностью изменена на {newRole}. Новый компонент: {newControllerReference.GetType().Name}");
         }
         catch (System.Exception ex) {
             Debug.LogError($"КРИТИЧЕСКАЯ ОШИБКА во время RebuildControllerComponent для {staffNameForLogs} (ID: {staffInstanceID}): {ex}");
             // Attempt to clean up or revert if possible
             // Ensure the staff member is removed from staffBeingModified even on error
         }
         finally
         {
            // Remove the staff member (using GameObject Instance ID) from the modification list
            staffBeingModified.RemoveAll(s => s == null || s.gameObject.GetInstanceID() == staffInstanceID);
            Debug.Log($"[RebuildControllerComponent] Завершение finally блока для {staffNameForLogs} (ID: {staffInstanceID}). staffBeingModified count: {staffBeingModified.Count}");
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
            case StaffController.Role.Unassigned:
                 Debug.LogError("Попытка получить тип контроллера для роли Unassigned.");
                 return null;
            default:
                 Debug.LogError($"Неизвестный тип роли для GetControllerTypeForRole: {role}");
                 return null;
        }
    }


    private ClerkController.ClerkRole GetClerkRoleFromStaffRole(StaffController.Role role)
    {
        switch (role)
        {
            case StaffController.Role.Registrar: return ClerkController.ClerkRole.Registrar;
            case StaffController.Role.Cashier: return ClerkController.ClerkRole.Cashier;
            case StaffController.Role.Archivist: return ClerkController.ClerkRole.Archivist;
            case StaffController.Role.Clerk: return ClerkController.ClerkRole.Regular;
            default:
                 Debug.LogWarning($"Попытка получить ClerkRole для не-клерк роли: {role}. Возвращен Regular.");
                 return ClerkController.ClerkRole.Regular;
        }
    }

    public void ActivateAllScheduledStaff()
    {
        string currentPeriod = ClientSpawner.CurrentPeriodName;
        if (string.IsNullOrEmpty(currentPeriod))
        {
             // Debug.LogWarning("ActivateAllScheduledStaff: CurrentPeriodName пуст."); // Optional log
             return;
        }

        Debug.Log($"<color=orange>ЗАПУСК AI:</color> Активация сотрудников для периода '{currentPeriod}'...");
        // Iterate over a copy of the list
        foreach (var staff in AllStaff.ToList())
        {
            if (staff == null) continue;

            bool isScheduledNow = staff.workPeriods != null && staff.workPeriods.Any(p => p.Equals(currentPeriod, System.StringComparison.InvariantCultureIgnoreCase));

            if (isScheduledNow && !staff.IsOnDuty())
            {
                 Debug.Log($" -> Активация смены для {staff.characterName} (Роль: {staff.currentRole})");
                staff.StartShift();
            }
             // else if (!isScheduledNow && staff.IsOnDuty()) {
                 // Ending shift logic is usually handled by CheckAllStaffShiftsImmediately or similar
                 // Debug.Log($" -> {staff.characterName} не должен работать, но на смене. (Завершение смены будет обработано отдельно)");
             // }
        }
         Debug.Log($"<color=orange>Активация смен завершена.</color>");
    }

    public void PromoteStaff(StaffController staff, RankData newRankData)
    {
        // --- Pre-Promotion Checks ---
        if (staff == null || newRankData == null)
        {
             Debug.LogError($"Ошибка повышения: staff ({staff?.name}) или newRankData ({newRankData?.name}) равен null.");
             return;
        }
        if (staff.experiencePoints < newRankData.experienceRequired)
        {
             Debug.LogWarning($"Недостаточно опыта ({staff.experiencePoints}/{newRankData.experienceRequired}) для повышения {staff.characterName} до {newRankData.rankName}");
             return;
        }
        if (PlayerWallet.Instance == null) {
            Debug.LogError("PlayerWallet не найден! Невозможно проверить/списать деньги за повышение.");
            return;
        }
        if (PlayerWallet.Instance.GetCurrentMoney() < newRankData.promotionCost)
        {
             Debug.LogWarning($"Недостаточно средств ({PlayerWallet.Instance.GetCurrentMoney()}/{newRankData.promotionCost}) для повышения {staff.characterName} до {newRankData.rankName}");
             // Optionally show UI feedback to player
             return;
        }
        // --- End Pre-Promotion Checks ---

        Debug.Log($"Начинаем повышение {staff.characterName} до {newRankData.rankName}...");

        // Deduct cost BEFORE applying changes
        PlayerWallet.Instance.AddMoney(-newRankData.promotionCost, $"Повышение: {staff.characterName}");

        // Apply new rank and salary
        staff.currentRank = newRankData;
        staff.salaryPerPeriod = Mathf.Max(staff.salaryPerPeriod, (int)(staff.salaryPerPeriod * newRankData.salaryMultiplier)); // Ensure salary doesn't decrease, apply multiplier

        // Play visual/audio feedback
        CharacterVisuals visuals = staff.GetComponent<CharacterVisuals>();
        visuals?.PlayLevelUpEffect(); // Play the visual effect
		
		staff.promotionAvailableNotificationPlayed = false;

        // Add newly unlocked tactical actions
        int actionsAdded = 0;
        if (newRankData.unlockedActions != null)
        {
            foreach (var action in newRankData.unlockedActions)
            {
                if (action != null && action.category == ActionCategory.Tactic && !staff.activeActions.Contains(action))
                {
                    staff.activeActions.Add(action);
                    actionsAdded++;
                }
            }
        }
         Debug.Log($"Добавлено {actionsAdded} новых тактических действий для {staff.characterName}.");


        // Change role if necessary (this might trigger component rebuild)
        if (staff.currentRole != newRankData.associatedRole)
        {
            Debug.Log($"Роль меняется с {staff.currentRole} на {newRankData.associatedRole} при повышении.");
            // Pass the current active actions (including newly added ones)
             AssignNewRole_Immediate(staff, newRankData.associatedRole, staff.activeActions); // This might start a coroutine
        } else {
             Debug.Log($"Роль {staff.currentRole} остается прежней при повышении.");
        }


         Debug.Log($"Повышение {staff.characterName} до {newRankData.rankName} завершено.");
         // Refresh the hiring panel UI to show updated rank/role
         FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
    }


    public void ResetState()
    {
        Debug.Log("[HiringManager] ResetState вызван.");
        AvailableCandidates.Clear();
        occupiedPoints.Clear();
        UnassignedStaff.Clear();
        AllStaff.Clear();
        staffBeingModified.Clear();
    }

    private void FindSceneSpecificReferences()
    {
        unassignedStaffPoints.Clear();
        occupiedPoints.Clear();
        InternPointsRegistry registry = FindFirstObjectByType<InternPointsRegistry>();
        if (registry != null)
        {
            unassignedStaffPoints = registry.points?.Where(p => p != null).ToList() ?? new List<Transform>();
            Debug.Log($"[HiringManager] Найдено {unassignedStaffPoints.Count} точек для неназначенных сотрудников.");
        }
        else
        {
            Debug.LogError("[HiringManager] НЕ НАЙДЕН InternPointsRegistry на сцене! Неназначенные сотрудники не смогут быть размещены.");
        }
         // Add similar finds for other registries if needed
    }


    public void GenerateNewCandidates()
    {
        AvailableCandidates.Clear();
        int currentDay = ClientSpawner.Instance != null ? ClientSpawner.Instance.GetCurrentDay() : 1;

        int internsToCreate = Mathf.Max(0, Mathf.RoundToInt(internCountOverTime.Evaluate(currentDay))); // Ensure non-negative
        int specialistsToCreate = Mathf.Max(0, Mathf.RoundToInt(specialistCountOverTime.Evaluate(currentDay))); // Ensure non-negative
        float experiencedChance = Mathf.Clamp01(experiencedInternChance.Evaluate(currentDay));

        Debug.Log($"[HiringManager] Генерация кандидатов для Дня {currentDay}: Стажеров={internsToCreate} (Шанс опыта={experiencedChance:P0}), Специалистов={specialistsToCreate}");

        // Create Interns
        for (int i = 0; i < internsToCreate; i++)
        {
             Candidate newIntern = CreateRandomCandidate(StaffController.Role.Intern, experiencedChance);
             if (newIntern != null) AvailableCandidates.Add(newIntern);
        }

        // Create Specialists
        var specialistRoles = System.Enum.GetValues(typeof(StaffController.Role))
            .Cast<StaffController.Role>()
            .Where(r => r != StaffController.Role.Intern && r != StaffController.Role.Unassigned)
            .ToList();

        if (specialistRoles.Any())
        {
            for (int i = 0; i < specialistsToCreate; i++)
            {
                StaffController.Role randomRole = specialistRoles[Random.Range(0, specialistRoles.Count)];
                Candidate newSpecialist = CreateRandomCandidate(randomRole, 0f); // Experience chance is 0 for specialists
                if (newSpecialist != null) AvailableCandidates.Add(newSpecialist);
            }
        } else {
             Debug.LogWarning("[HiringManager] Нет доступных ролей специалистов для генерации.");
        }


        Debug.Log($"[HiringManager] Сгенерировано {AvailableCandidates.Count} новых кандидатов.");
        // Optionally trigger UI update if needed immediately
        // FindFirstObjectByType<HiringSystemUI>(FindObjectsInactive.Include)?.RefreshCandidatesDisplay();
    }

    private Candidate CreateRandomCandidate(StaffController.Role role, float experiencedChance)
     {
        Candidate candidate = new Candidate();
        candidate.Role = role;
        candidate.Gender = (Random.value > 0.5f) ? Gender.Male : Gender.Female;

        // --- Generate Name ---
        bool nameGenerated = false;
        try { // Add try-catch for safety if lists might be empty
            if (candidate.Gender == Gender.Male)
            {
                if (lastNames.Any() && firstNamesMale.Any() && patronymicsMale.Any())
                {
                    candidate.Name = $"{lastNames[Random.Range(0, lastNames.Count)]} {firstNamesMale[Random.Range(0, firstNamesMale.Count)]} {patronymicsMale[Random.Range(0, patronymicsMale.Count)]}";
                    nameGenerated = true;
                }
            }
            else // Female
            {
                if (lastNames.Any() && firstNamesFemale.Any() && patronymicsFemale.Any())
                {
                    string lastNameBase = lastNames[Random.Range(0, lastNames.Count)];
                    string femaleLastName = lastNameBase.EndsWith("в") || lastNameBase.EndsWith("н") || lastNameBase.EndsWith("й") ? lastNameBase + "а" : lastNameBase; // Slightly better check
                    candidate.Name = $"{femaleLastName} {firstNamesFemale[Random.Range(0, firstNamesFemale.Count)]} {patronymicsFemale[Random.Range(0, patronymicsFemale.Count)]}";
                    nameGenerated = true;
                }
            }
        } catch (System.ArgumentOutOfRangeException ex) {
             Debug.LogError($"Ошибка генерации имени (индекс вне диапазона): {ex.Message}. Проверьте списки имен.");
             nameGenerated = false;
        }

        if (!nameGenerated) {
            candidate.Name = $"Кандидат {(candidate.Gender == Gender.Male ? "М" : "Ж")} #{Random.Range(100, 1000)}";
            Debug.LogWarning("Не удалось сгенерировать полное имя.");
        }
        // --- End Generate Name ---

        // --- Create and Assign Skills ---
        candidate.Skills = ScriptableObject.CreateInstance<CharacterSkills>();
        candidate.Skills.paperworkMastery = Random.Range(0, 5) * 0.25f;
        candidate.Skills.sedentaryResilience = Random.Range(0, 5) * 0.25f;
        candidate.Skills.pedantry = Random.Range(0, 5) * 0.25f;
        candidate.Skills.softSkills = Random.Range(0, 5) * 0.25f;
        candidate.Skills.corruption = Random.Range(0, 5) * 0.25f;
        // --- End Skills ---

        // --- Determine Starting Rank ---
        if (rankDatabase == null || !rankDatabase.Any(r => r != null)) // Check if list exists and has non-null entries
        {
            Debug.LogError("RankDatabase не назначен, пуст или содержит только null элементы! Невозможно назначить ранг кандидату.");
            Destroy(candidate.Skills); // Clean up created ScriptableObject
            return null;
        }

        RankData startingRank = null;
        if (role == StaffController.Role.Intern && Random.value < experiencedChance)
        {
            // Try to find Rank 1 or 2 for Intern
            int targetRankLevel = Random.Range(1, 3);
            startingRank = rankDatabase.FirstOrDefault(r => r != null && r.associatedRole == StaffController.Role.Intern && r.rankLevel == targetRankLevel);
            if (startingRank == null) {
                 // Fallback to Rank 0 if higher rank not found
                 startingRank = rankDatabase.FirstOrDefault(r => r != null && r.associatedRole == StaffController.Role.Intern && r.rankLevel == 0);
                 if (startingRank != null) Debug.Log($"Не найден RankData для опытного стажера (Ур. {targetRankLevel}), используется Ранг 0 для {candidate.Name}.");
            } else {
                 Debug.Log($"Сгенерирован опытный стажер {candidate.Name} (Ур. {targetRankLevel}).");
            }
        }
        else
        {
            // Find Rank 0 for the specified role (Intern or Specialist)
            startingRank = rankDatabase.FirstOrDefault(r => r != null && r.associatedRole == role && r.rankLevel == 0);
        }

        if (startingRank == null)
        {
            Debug.LogError($"Не найден RankData (Уровень 0) для роли {role}! Проверьте список Rank Database в HiringManager ({rankDatabase.Count} записей) и ассеты RankData.");
            Destroy(candidate.Skills); // Clean up
            return null;
        }
        candidate.Rank = startingRank;
        // --- End Rank ---

        candidate.Experience = candidate.Rank.experienceRequired;

        // --- Calculate Hiring Cost ---
        RoleData roleData = allRoleData?.FirstOrDefault(data => data != null && data.roleType == role);
        int roleBaseCost = (roleData != null) ? roleData.baseHiringCost : this.baseCost; // Use RoleData cost or fallback

        float totalSkillPoints = candidate.Skills.paperworkMastery + candidate.Skills.sedentaryResilience +
                         candidate.Skills.pedantry + candidate.Skills.softSkills;

        candidate.HiringCost = roleBaseCost + (int)(totalSkillPoints * costPerSkillPoint);
        // Add the cumulative promotion cost stored in the starting RankData asset
        candidate.HiringCost += candidate.Rank.promotionCost;
        candidate.HiringCost = Mathf.Max(10, candidate.HiringCost); // Ensure minimum cost
        // --- End Cost ---

        candidate.Bio = ResumeGenerator.GenerateBio();
        candidate.UniqueActionsPool = new List<StaffAction>(); // Initialize empty

        return candidate;
     }

    public bool HireCandidate(Candidate candidate)
    {
        // --- Pre-Hire Checks ---
        if (candidate == null) { Debug.LogError("HireCandidate: Попытка нанять null кандидата!"); return false; }
        if (PlayerWallet.Instance == null) { Debug.LogError("HireCandidate: PlayerWallet не найден!"); return false; }
        if (PlayerWallet.Instance.GetCurrentMoney() < candidate.HiringCost) { Debug.LogWarning($"HireCandidate: Недостаточно средств ({PlayerWallet.Instance.GetCurrentMoney()}/{candidate.HiringCost}) для найма {candidate.Name}."); return false; }

        Transform freePoint = unassignedStaffPoints.FirstOrDefault(p => p != null && !occupiedPoints.ContainsKey(p));
        if (freePoint == null) { Debug.LogWarning("HireCandidate: Нет свободных мест для размещения (unassignedStaffPoints)."); return false; }
        // --- End Pre-Hire Checks ---

        // --- Find RoleData and Prefab ---
        RoleData roleData = allRoleData?.FirstOrDefault(data => data != null && data.roleType == candidate.Role);
        GameObject prefabToSpawn = GetPrefabForRole(candidate.Role);

        if (roleData == null || prefabToSpawn == null) { Debug.LogError($"HireCandidate: Не найдены RoleData ({roleData == null}) или префаб ({prefabToSpawn == null}) для роли {candidate.Role}!"); return false; }
        // --- End Find RoleData ---

        // --- Instantiate and Get Controller ---
        GameObject newStaffGO = Instantiate(prefabToSpawn, freePoint.position, Quaternion.identity);
        StaffController staffController = newStaffGO.GetComponent<StaffController>();
        // --- End Instantiate ---

        if (staffController != null)
        {
            // --- Assign Candidate Data ---
            staffController.characterName = candidate.Name;
            staffController.skills = candidate.Skills;
            staffController.gender = candidate.Gender;
            staffController.currentRank = candidate.Rank;
            staffController.experiencePoints = candidate.Experience;
            staffController.salaryPerPeriod = candidate.Rank.salaryMultiplier > 0 ? Mathf.RoundToInt(baseCost * candidate.Rank.salaryMultiplier) : baseCost; // Example salary calculation
            // --- End Assign Data ---

            // --- Assign Actions (Start with empty tactical actions) ---
            staffController.activeActions = new List<StaffAction>();
            Debug.Log($"Новый сотрудник {staffController.characterName} (Ранг {staffController.currentRank?.rankName ?? "N/A"}) начинает с 0 активных тактических действий.");
            // --- End Assign Actions ---

            // --- Initialize Controller ---
            staffController.Initialize(roleData);
            // Assign System Actions DB
            var existingStaff = AllStaff.FirstOrDefault(s => s != null && s.systemActionDatabase != null);
            if (existingStaff != null) { staffController.systemActionDatabase = existingStaff.systemActionDatabase; }
            else { Debug.LogWarning($"Не удалось найти базу системных действий для назначения {staffController.characterName}."); }
            // Assign Default Schedule
             if (ClientSpawner.Instance?.mainCalendar?.periodSettings != null) {
                 staffController.workPeriods = ClientSpawner.Instance.mainCalendar.periodSettings.Select(p => p.periodName).Where(name => !string.IsNullOrEmpty(name)).ToList();
             } else {
                  Debug.LogWarning($"Не удалось назначить расписание по умолчанию для {staffController.characterName}.");
                  staffController.workPeriods = new List<string>();
             }
            // --- End Initialize ---

            // --- Register Staff Member ---
            AllStaff.Add(staffController);
            UnassignedStaff.Add(staffController);
            newStaffGO.name = candidate.Name;
            occupiedPoints.Add(freePoint, staffController);
            AvailableCandidates.Remove(candidate);
            // --- End Register ---

            // --- Deduct Cost ---
            PlayerWallet.Instance.AddMoney(-candidate.HiringCost, $"Наём: {candidate.Name}");
            // --- End Deduct Cost ---

            // --- Start Shift if Applicable ---
            string currentPeriod = ClientSpawner.CurrentPeriodName;
             if (!string.IsNullOrEmpty(currentPeriod) && staffController.workPeriods.Any(p => p.Equals(currentPeriod, System.StringComparison.InvariantCultureIgnoreCase))) {
                 staffController.StartShift();
                 Debug.Log($"Сотрудник {candidate.Name} нанят и немедленно приступает к работе в период '{currentPeriod}'.");
             } else {
                 Debug.Log($"Сотрудник {candidate.Name} нанят, но его смена ('{currentPeriod}') сейчас неактивна.");
             }
            // --- End Start Shift ---

            Debug.Log($"Успешно нанят: {candidate.Name}. Стоимость: {candidate.HiringCost}.");
             FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
            return true;
        }
        else // Critical error
        {
            Destroy(newStaffGO);
            Debug.LogError($"КРИТИЧЕСКАЯ ОШИБКА при найме {candidate.Name}: не найден компонент StaffController на префабе {prefabToSpawn.name}!");
            return false;
        }
    }


    private GameObject GetPrefabForRole(StaffController.Role role)
    {
        // Currently returns internPrefab for all roles.
        // Needs expansion if different prefabs are used.
        if (internPrefab == null) {
             Debug.LogError("Intern Prefab не назначен в HiringManager!");
             return null; // Return null if prefab is missing
        }
        return internPrefab;
    }


    public StaffController.Role GetRoleForDeskId(int deskId)
    {
        if (deskId == 0) return StaffController.Role.Registrar;
        if (deskId == -1) return StaffController.Role.Cashier; // Main Cashier
        if (deskId == 1 || deskId == 2) return StaffController.Role.Clerk; // Office Desks
        if (deskId == 3) return StaffController.Role.Archivist; // Archivist Desk
        if (deskId == 4) return StaffController.Role.Cashier; // Bookkeeping Desk? Assumed Cashier skill. Adjust if needed.
        // Add other IDs if necessary
        // Debug.LogWarning($"GetRoleForDeskId: Неизвестный deskId: {deskId}. Возвращена роль Unassigned.");
        return StaffController.Role.Unassigned;
    }


    public void FireStaff(StaffController staffToFire)
    {
        if (staffToFire == null) { Debug.LogWarning("Попытка уволить null сотрудника."); return; }

        Debug.Log($"Начинаем процедуру увольнения для {staffToFire.characterName}...");

        // Remove from manager lists
        bool removedFromAll = AllStaff.Remove(staffToFire);
        bool removedFromUnassigned = UnassignedStaff.Remove(staffToFire);
         if (!removedFromAll) Debug.LogWarning($"{staffToFire.characterName} не найден в AllStaff при увольнении.");


        // Free up spawn point
        Transform pointToFree = null;
        try { // Use try-catch for potential dictionary modification issues if called rapidly
            pointToFree = occupiedPoints.FirstOrDefault(kvp => kvp.Value == staffToFire).Key;
        } catch {}

        if (pointToFree != null) {
            occupiedPoints.Remove(pointToFree);
            Debug.Log($"Освобождена точка {pointToFree.name} после увольнения {staffToFire.characterName}.");
        } else {
             Debug.Log($"Не найдена точка для освобождения для {staffToFire.characterName} (возможно, был назначен на рабочее место).");
        }


        // Unassign from workstation
        if (staffToFire.assignedWorkstation != null && AssignmentManager.Instance != null) {
             AssignmentManager.Instance.UnassignStaff(staffToFire);
             Debug.Log($"Сотрудник {staffToFire.characterName} снят с рабочего места {staffToFire.assignedWorkstation.name}.");
        }

        // Trigger go home routine (should handle stopping actions etc.)
        staffToFire.FireAndGoHome();

        Debug.Log($"Сотрудник {staffToFire.characterName} уволен.");

         // Refresh UI
         FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
    }


    public void CheckAllStaffShiftsImmediately()
    {
        string currentPeriod = ClientSpawner.CurrentPeriodName;
        if (string.IsNullOrEmpty(currentPeriod)) { return; } // Exit if no period name

        Debug.Log($"<color=orange>ПРОВЕРКА СМЕН:</color> Период '{currentPeriod}'. Сотрудников в AllStaff: {AllStaff.Count}");

        // Iterate over a copy
        foreach (var staff in AllStaff.ToList())
        {
            if (staff == null) {
                 Debug.LogWarning("Найден null сотрудник в AllStaff во время проверки смен.");
                 continue;
            }

            bool isScheduledNow = staff.workPeriods != null && staff.workPeriods.Any(p => p.Equals(currentPeriod, System.StringComparison.InvariantCultureIgnoreCase));
            bool isOnDuty = staff.IsOnDuty(); // Check current duty status

            // Debug log for each staff member
            // Debug.Log($" - Проверка {staff.characterName} (На смене: {isOnDuty}, Расписание содержит '{currentPeriod}': {isScheduledNow})");

            if (isScheduledNow && !isOnDuty)
            {
                Debug.Log($"   -> {staff.characterName}: Начать смену.");
                staff.StartShift();
            }
            else if (!isScheduledNow && isOnDuty)
            {
                 Debug.Log($"   -> {staff.characterName}: Закончить смену.");
                staff.EndShift();
            }
             // else: Correct state, do nothing.
        }
         Debug.Log($"<color=orange>ПРОВЕРКА СМЕН ЗАВЕРШЕНА</color>");
    }

} // End of HiringManager class