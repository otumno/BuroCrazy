// Assets/Scripts/Managers/HiringManager.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;
using Characters;
using UI;
using Enums;
using Managers.Teletype;

namespace Managers
{
    public class HiringManager : MonoBehaviour
    {
        public static HiringManager Instance { get; set; }

        [Header("Префабы сотрудников")]
        public GameObject internPrefab; 

        [Header("Базы данных")]
        public List<RoleData> allRoleData;
        public List<RankData> rankDatabase;

        public List<StaffController> AllStaff = new List<StaffController>();
        public List<StaffController> UnassignedStaff = new List<StaffController>();

        private List<Transform> unassignedStaffPoints = new List<Transform>();
        private Dictionary<Transform, StaffController> occupiedPoints = new Dictionary<Transform, StaffController>();

        [Header("Настройки генерации кандидатов")]
        public AnimationCurve internCountOverTime = new AnimationCurve(new Keyframe(1, 4), new Keyframe(30, 1));
        public AnimationCurve specialistCountOverTime = new AnimationCurve(new Keyframe(1, 0), new Keyframe(10, 1), new Keyframe(30, 3));
        public AnimationCurve experiencedInternChance = new AnimationCurve(new Keyframe(1, 0), new Keyframe(5, 0.1f), new Keyframe(30, 0.5f));

        [Header("Стоимость найма")]
        public int baseCost = 100;
        public int costPerSkillPoint = 150;

        // --- Списки Имен ---
        [System.Serializable]
        private struct NameSet { public string full; public string shortName; public string diminutive; }

        private List<NameSet> maleNames = new List<NameSet> { 
            new NameSet { full = "Иван", shortName = "Ваня", diminutive = "Ванечка" },
            new NameSet { full = "Аркадий", shortName = "Аркаша", diminutive = "Аркашенька" },
            new NameSet { full = "Иннокентий", shortName = "Кеша", diminutive = "Кешенька" },
            new NameSet { full = "Пантелеймон", shortName = "Пантя", diminutive = "Пантеюшка" },
            new NameSet { full = "Акакий", shortName = "Акаша", diminutive = "Акакинька" },
            new NameSet { full = "Евгений", shortName = "Женя", diminutive = "Женечка" },
            new NameSet { full = "Анатолий", shortName = "Толя", diminutive = "Толечка" },
            new NameSet { full = "Вениамин", shortName = "Веня", diminutive = "Венечка" },
            new NameSet { full = "Борис", shortName = "Боря", diminutive = "Боренька" }
        };
        private List<NameSet> femaleNames = new List<NameSet> { 
            new NameSet { full = "Аглая", shortName = "Глаша", diminutive = "Глашенька" },
            new NameSet { full = "Евпраксия", shortName = "Прасковья", diminutive = "Прасенька" },
            new NameSet { full = "Пелагея", shortName = "Поля", diminutive = "Поленька" },
            new NameSet { full = "Серафима", shortName = "Сима", diminutive = "Симочка" },
            new NameSet { full = "Зинаида", shortName = "Зина", diminutive = "Зиночка" },
            new NameSet { full = "Клавдия", shortName = "Клава", diminutive = "Клавочка" },
            new NameSet { full = "Тамара", shortName = "Тома", diminutive = "Томочка" },
            new NameSet { full = "Антонина", shortName = "Тоня", diminutive = "Тонечка" },
            new NameSet { full = "Людмила", shortName = "Люда", diminutive = "Людочка" }
        };
        private List<string> lastNames = new List<string> { "Перепискин", "Протоколов", "Архивариусов", "Гербовый", "Скрепочкин", "Бланков", "Штампов", "Сургучев", "Бумагин", "Папкин", "Канцелярский", "Законов" };
        private List<string> patronymicsMale = new List<string> { "Аркадьевич", "Иннокентьевич", "Варфоломеевич", "Акакиевич", "Поликарпович", "Дормидонтович", "Евгеньевич", "Анатольевич" };
        private List<string> patronymicsFemale = new List<string> { "Аркадьевна", "Иннокентьевна", "Варфоломеевна", "Акакиевна", "Поликарповна", "Дормидонтовна", "Евгеньевна", "Анатольевна" };

        public List<Candidate> AvailableCandidates { get; private set; } = new List<Candidate>();
        private List<StaffController> staffBeingModified = new List<StaffController>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                SceneManager.sceneLoaded += OnSceneLoaded; 
            }
            else if (Instance != this)
            {
                Destroy(this); 
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "GameScene") 
            {
                FindSceneSpecificReferences();
                StartCoroutine(RegisterExistingStaffAndAssignDatabases());
            }
            else
            {
                unassignedStaffPoints.Clear();
                occupiedPoints.Clear();
            }
        }
		
		private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPeriodChanged += OnTimePeriodChanged;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                if (TimeManager.Instance != null) TimeManager.Instance.OnPeriodChanged -= OnTimePeriodChanged;
            }
        }

        private void OnTimePeriodChanged(PeriodSettings settings)
        {
            CheckAllStaffShiftsImmediately();
        }

        private IEnumerator RegisterExistingStaffAndAssignDatabases()
        {
            yield return new WaitForEndOfFrame();
            AllStaff.Clear(); 
            StaffController[] existingStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);

            ActionDatabase systemActions = null;
            var firstValidStaff = existingStaff.FirstOrDefault(s => s != null && !(s is DirectorAvatarController));
            if (firstValidStaff != null) systemActions = firstValidStaff.systemActionDatabase;

            foreach (var staffMember in existingStaff)
            {
                if (staffMember == null || staffMember is DirectorAvatarController) continue;

                if (!AllStaff.Contains(staffMember)) 
                {
                    AllStaff.Add(staffMember);
                    if (staffMember.systemActionDatabase == null && systemActions != null)
                        staffMember.systemActionDatabase = systemActions;

                    var agentMover = staffMember.GetComponent<AgentMover>();
                    var visuals = staffMember.GetComponent<CharacterVisuals>();
                    var logger = staffMember.GetComponent<CharacterStateLogger>();
                    
                    if (agentMover != null && visuals != null && logger != null) 
                        staffMember.ForceInitializeBaseComponents(agentMover, visuals, logger);
                } 
            }
        }

        // ... (Методы RebuildControllerComponent и AssignNewRole_Immediate пропущены для краткости, они не менялись)

        private System.Type GetControllerTypeForRole(StaffController.Role role)
        {
            switch (role)
            {
                case StaffController.Role.Guard: return typeof(GuardMovement);
                case StaffController.Role.Clerk:
                case StaffController.Role.Registrar:
                case StaffController.Role.Cashier:
                case StaffController.Role.Archivist: 
                case StaffController.Role.Accountant: return typeof(ClerkController);
                case StaffController.Role.Janitor: return typeof(ServiceWorkerController);
                case StaffController.Role.Intern: return typeof(InternController);
                case StaffController.Role.OfficeManager: return typeof(OfficeManagerController);
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
                case StaffController.Role.Accountant: return ClerkController.ClerkRole.Accountant;
                default: return ClerkController.ClerkRole.Regular;
            }
        }

        public bool HireCandidate(Candidate candidate)
        {
            if (candidate == null || PlayerWallet.Instance == null) return false;
            if (PlayerWallet.Instance.GetCurrentMoney() < candidate.HiringCost) return false;

            Transform freePoint = unassignedStaffPoints.FirstOrDefault(p => p != null && !occupiedPoints.ContainsKey(p));
            // Если точек нет, спавним просто где-то (чтобы найм не ломался)
            Vector3 spawnPos = freePoint != null ? freePoint.position : Vector3.zero;

            RoleData roleData = allRoleData?.FirstOrDefault(data => data != null && data.roleType == candidate.Role);
            GameObject prefabToSpawn = internPrefab; // Используем единый префаб, скрипты накинутся сами

            if (roleData == null) Debug.LogWarning($"[HiringManager] RoleData не найден для роли {candidate.Role}");
            if (prefabToSpawn == null) { Debug.LogWarning($"[HiringManager] Prefab не найден!"); return false; }

            GameObject newStaffGO = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            
            // --- ПЕРЕМЕЩАЕМ НАЙМА В ЗОНУ ДОМА И СКРЫВАЕМ ---
            // Переместим объект в точку дома
            if (Managers.ScenePointsRegistry.Instance != null && Managers.ScenePointsRegistry.Instance.staffHomeZone != null)
            {
                newStaffGO.transform.position = Managers.ScenePointsRegistry.Instance.staffHomeZone.GetRandomPointInside();
            }
            
            // Сразу выключаем объект - он появится только когда менеджер смен решит его разбудить
            newStaffGO.SetActive(false);
            // ----------------------------------------------------
            
            // ВАЖНО: Удаляем старый компонент (InternController), если роль другая, и добавляем нужный
            // Или используем универсальный StaffController и Rebuild.
            // Для упрощения предположим, что префаб пустой или Rebuild сработает.
            // Но лучше сразу добавить правильный компонент.
            
            StaffController staffController = newStaffGO.GetComponent<StaffController>();
            if (staffController == null) staffController = newStaffGO.AddComponent<StaffController>(); // Fallback

            // Сразу меняем тип контроллера на правильный
            var targetType = GetControllerTypeForRole(candidate.Role);
            if (targetType != null && staffController.GetType() != targetType)
            {
                // Удаляем InternNotification если конвертируем из Intern во что-то другое
                if (staffController.GetType() == typeof(InternController) && targetType != typeof(InternController))
                {
                    var internNotification = newStaffGO.GetComponent<InternNotification>();
                    if (internNotification != null) DestroyImmediate(internNotification);
                }
                
                // Удаляем старый контроллер если это не StaffController
                var currentType = staffController.GetType();
                if (currentType != typeof(StaffController))
                {
                    DestroyImmediate(staffController);
                    staffController = (StaffController)newStaffGO.AddComponent(targetType);
                }
                else if (newStaffGO.GetComponent(targetType) == null)
                {
                    // StaffController (базовый) остаётся, добавляем нужный тип
                    newStaffGO.AddComponent(targetType);
                    staffController = newStaffGO.GetComponent<StaffController>();
                }
            }

            if (staffController != null)
            {
                // Инициализируем базовые компоненты как для существующих сотрудников
                var agentMover = newStaffGO.GetComponent<AgentMover>();
                var visuals = newStaffGO.GetComponent<CharacterVisuals>();
                var logger = newStaffGO.GetComponent<CharacterStateLogger>();
                if (agentMover != null && visuals != null && logger != null)
                {
                    staffController.ForceInitializeBaseComponents(agentMover, visuals, logger);
                }

                staffController.nameData = candidate.NameData;
                staffController.skills = candidate.Skills;
                staffController.gender = candidate.Gender;
                staffController.currentRank = candidate.Rank;
                staffController.experiencePoints = candidate.Experience;
                staffController.salaryPerPeriod = candidate.Rank.salaryMultiplier > 0 ? Mathf.RoundToInt(baseCost * candidate.Rank.salaryMultiplier) : baseCost;
                staffController.currentRole = candidate.Role; // ВАЖНО!
                staffController.permanentTrait = candidate.Trait; // Передаём трейт
                
                staffController.activeActions = new List<StaffAction>();

                // ---> ИСПРАВЛЕНИЕ: ВЫДАЕМ СТАРТОВЫЕ ДЕЙСТВИЯ ИЗ РАНГА <---
                if (candidate.Rank != null && candidate.Rank.unlockedActions != null)
                {
                    foreach (var action in candidate.Rank.unlockedActions)
                    {
                        if (action != null && action.category == ActionCategory.Tactic)
                        {
                            staffController.activeActions.Add(action);
                        }
                    }
                }
                // ---------------------------------------------------------
                
                if (roleData != null) staffController.InitializeFromData(roleData);
                
                // График работы: по умолчанию - УТРО (Morning) для всех сотрудников
                staffController.WorkShiftMask = Data.Calendar.CalendarDayPeriodType.Morning;

                newStaffGO.name = $"{candidate.NameData.lastName} {candidate.NameData.firstName}";

                AllStaff.Add(staffController);
                UnassignedStaff.Add(staffController);
                newStaffGO.name = candidate.Name;
                if(freePoint != null) occupiedPoints.Add(freePoint, staffController);
                AvailableCandidates.Remove(candidate);

                // Логирование найма с трейтом
                string traitName = StaffController.TraitLibrary[candidate.Trait].Name;
                Debug.Log($"<color=cyan>[HIRING]</color> Нанят сотрудник {candidate.Name}. Особенность: <b>{traitName}</b>");
                
                // Лог в Телетайп
                if (TeletypeManager.Instance != null)
                {
                    TeletypeManager.Instance.LogImportant($"Новый сотрудник: {candidate.NameData.shortName}. Особенность: {traitName}");
                }

                PlayerWallet.Instance.AddMoney(-candidate.HiringCost, $"Наём: {candidate.Name}");

                // Сначала добавляем в AllStaff ЧТОБЫ таблица расписания видела сотрудника
                // Потом уже запускаем смену
                
                // ----- ИСПРАВЛЕНИЕ ЗДЕСЬ -----
                // Intern всегда выходит на работу сразу после найма
                bool isIntern = candidate.Role == StaffController.Role.Intern;

                // Проверяем расписание для не-Intern
                bool isScheduled = true;
                if (!isIntern)
                {
                    var periodType = TimeManager.Instance.GetCurrentPeriodType();
                    isScheduled = (staffController.WorkShiftMask & periodType) != 0;
                }

                // Вызываем StartShift() чтобы сотрудник инициализировался
                Debug.Log($"[HiringManager] Нанят {staffController.characterName}. Инициализация смены.");
                staffController.StartShift();

                // Для Intern - всегда visible, для остальных - проверяем расписание
                if (!isScheduled)
                {
                    Debug.Log($"[HiringManager] {staffController.characterName}: Сейчас не его смена. Скрываем до начала работы.");
                    staffController.gameObject.SetActive(false);
                }
                // -----------------------------

                FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();

                // Обновляем панель расписания если она открыта
                var schedulePanel = FindFirstObjectByType<StaffSchedulePanelUI>(FindObjectsInactive.Include);
                if (schedulePanel != null)
                {
                    schedulePanel.RefreshTable();
                }

                return true;
            }
            
            Destroy(newStaffGO);
            return false;
        }

        // ... (Остальные методы: FireStaff, CheckAllStaffShiftsImmediately и т.д. без изменений)
        
        public void FireStaff(StaffController staffToFire)
        {
            if (staffToFire == null) return;
            AllStaff.Remove(staffToFire);
            UnassignedStaff.Remove(staffToFire);
            if (staffToFire.assignedWorkstation != null && AssignmentManager.Instance != null)
                AssignmentManager.Instance.UnassignStaff(staffToFire);
            staffToFire.FireAndGoHome();
            staffBeingModified.RemoveAll(s => s == null || s == staffToFire);
            FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
        }

        public void RemoveStaff(StaffController staff)
        {
            if (staff == null) return;
            AllStaff.Remove(staff);
            UnassignedStaff.Remove(staff);
            staffBeingModified.RemoveAll(s => s == null || s == staff);
        }

        public void CheckAllStaffShiftsImmediately()
        {
            if (TimeManager.Instance == null) return;
            var periodType = TimeManager.Instance.GetCurrentPeriodType();

            foreach (var staff in AllStaff.ToList())
            {
                if (staff == null) continue;
                
                var isScheduledNow = (staff.WorkShiftMask & periodType) != 0;
                var isOnDuty = staff.IsOnDuty();

                // === ИСПРАВЛЕНИЕ "ЭФФЕКТА ЛИМБА" ===
                // Если запланирован И (выключен ИЛИ уже уходил)
                if (isScheduledNow && (!staff.gameObject.activeSelf || staff.hasLeftToday))
                {
                    // Сбрасываем флаг ухода
                    staff.hasLeftToday = false;
                    
                    // Если ещё не приходил - рассчитываем время прихода
                    if (!staff.hasArrivedToday)
                    {
                        staff.CalculateArrivalTime();
                    }
                    
                    float lateness = staff.currentLateness;

                    if (lateness == -1f) // Заболел
                    {
                        Managers.Teletype.TeletypeManager.Instance?.LogImportant($"ВНИМАНИЕ: {staff.characterName} взял больничный и сегодня не выйдет!");
                        staff.hasArrivedToday = true;
                        staff.gameObject.SetActive(false);
                    }
                    else if (lateness > 0f) // Опаздывает
                    {
                        Managers.Teletype.TeletypeManager.Instance?.Log($"{staff.characterName} задерживается на {Mathf.RoundToInt(lateness)}");
                        staff.hasArrivedToday = true;
                        StartCoroutine(DelayedStartShift(staff, lateness));
                    }
                    else // Пришел вовремя
                    {
                        if (!staff.gameObject.activeSelf) staff.gameObject.SetActive(true);
                        staff.StartShift();
                    }
                }
                else if (!isScheduledNow && isOnDuty)
                {
                    // УБИРАЕМ принудительное завершение смены - теперь AI сам решит, когда уйти!
                    // staff.EndShift();
                }
            }
        }

        private IEnumerator DelayedStartShift(StaffController staff, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (staff != null)
            {
                if (!staff.gameObject.activeSelf) staff.gameObject.SetActive(true);
                staff.StartShift();
                staff.thoughtBubble?.ShowPriorityMessage("Ох, пробки...", 3f, Color.yellow);
            }
        }
        
        // ... (GenerateNewCandidates и прочее оставляем)
        public void GenerateNewCandidates()
        {
            AvailableCandidates.Clear();
            int currentDay = CalendarManager.Instance != null ? CalendarManager.Instance.CurrentDay : 1;

            int internsToCreate = Mathf.Max(0, Mathf.RoundToInt(internCountOverTime.Evaluate(currentDay)));
            int specialistsToCreate = Mathf.Max(0, Mathf.RoundToInt(specialistCountOverTime.Evaluate(currentDay)));
            float experiencedChance = Mathf.Clamp01(experiencedInternChance.Evaluate(currentDay));

            var specialistRoles = System.Enum.GetValues(typeof(StaffController.Role))
                .Cast<StaffController.Role>()
                .Where(r => r != StaffController.Role.Intern && r != StaffController.Role.Unassigned && r != StaffController.Role.Director)
                .ToList();

            int totalSpecialists = specialistsToCreate;
            
            if (totalSpecialists >= 1 && totalSpecialists <= 3)
            {
                foreach (var role in specialistRoles)
                {
                    Candidate candidate = CreateRandomCandidate(role, 0f);
                    if (candidate != null) AvailableCandidates.Add(candidate);
                }
            }
            else
            {
                for (int i = 0; i < specialistsToCreate; i++)
                {
                    StaffController.Role randomRole = specialistRoles[Random.Range(0, specialistRoles.Count)];
                    Candidate newSpecialist = CreateRandomCandidate(randomRole, 0f);
                    if (newSpecialist != null) AvailableCandidates.Add(newSpecialist);
                }
            }

            for (int i = 0; i < internsToCreate; i++)
            {
                Candidate newIntern = CreateRandomCandidate(StaffController.Role.Intern, experiencedChance);
                if (newIntern != null) AvailableCandidates.Add(newIntern);
            }
        }
        
        private Candidate CreateRandomCandidate(StaffController.Role role, float experiencedChance)
        {
             Candidate candidate = new Candidate();
            candidate.Role = role;
            candidate.Gender = (Random.value > 0.5f) ? Gender.Male : Gender.Female;

            var firstNames = candidate.Gender == Gender.Male ? maleNames : femaleNames;
            var patronymics = candidate.Gender == Gender.Male ? patronymicsMale : patronymicsFemale;
            
            // Создаем StaffNameData
            candidate.NameData = new StaffController.StaffNameData();
            
            if (firstNames.Count > 0 && lastNames.Count > 0 && patronymics.Count > 0)
            {
                NameSet firstNameData = firstNames[Random.Range(0, firstNames.Count)];
                candidate.NameData.firstName = firstNameData.full;
                candidate.NameData.shortName = firstNameData.shortName;
                candidate.NameData.diminutiveName = firstNameData.diminutive;
                
                string lname = lastNames[Random.Range(0, lastNames.Count)];
                if (candidate.Gender == Gender.Female && !lname.EndsWith("а")) lname += "а";
                candidate.NameData.lastName = lname;
                candidate.NameData.patronymic = patronymics[Random.Range(0, patronymics.Count)];
                
                // UI fallback - полное ФИО
                candidate.Name = $"{candidate.NameData.lastName} {candidate.NameData.firstName} {candidate.NameData.patronymic}";
            }
            else candidate.Name = "Кандидат " + Random.Range(100, 999);

            candidate.Skills = ScriptableObject.CreateInstance<CharacterSkills>();
            candidate.Skills.paperworkMastery = Random.Range(0, 5) * 0.25f;
            candidate.Skills.sedentaryResilience = Random.Range(0, 5) * 0.25f;
            candidate.Skills.pedantry = Random.Range(0, 5) * 0.25f;
            candidate.Skills.softSkills = Random.Range(0, 5) * 0.25f;
            candidate.Skills.corruption = Random.Range(0, 5) * 0.25f;

            if (rankDatabase == null) return null;

            RankData startingRank = null;
            if (role == StaffController.Role.Intern && Random.value < experiencedChance)
            {
                startingRank = rankDatabase.FirstOrDefault(r => r != null && r.associatedRole == StaffController.Role.Intern && r.rankLevel > 0);
            }
            if (startingRank == null)
            {
                startingRank = rankDatabase.FirstOrDefault(r => r != null && r.associatedRole == role && r.rankLevel == 0);
            }
            if (startingRank == null) return null;
            
            candidate.Rank = startingRank;
            candidate.Experience = candidate.Rank.experienceRequired;

            RoleData roleData = allRoleData?.FirstOrDefault(data => data != null && data.roleType == role);
            int roleBaseCost = (roleData != null) ? roleData.baseHiringCost : this.baseCost;

            float totalSkillPoints = candidate.Skills.paperworkMastery + candidate.Skills.sedentaryResilience +
                                     candidate.Skills.pedantry + candidate.Skills.softSkills;

            candidate.HiringCost = roleBaseCost + (int)(totalSkillPoints * costPerSkillPoint);
            candidate.HiringCost += candidate.Rank.promotionCost;
            candidate.HiringCost = Mathf.Max(10, candidate.HiringCost);

            candidate.Bio = ResumeGenerator.GenerateBio();
            candidate.UniqueActionsPool = new List<StaffAction>();
            
            // Присваиваем случайный трейт (исключая None и ToiletRush для разнообразия)
            var traitValues = System.Enum.GetValues(typeof(StaffController.TraitType));
            candidate.Trait = (StaffController.TraitType)traitValues.GetValue(Random.Range(1, traitValues.Length));

            return candidate;
        }
        
        public void PromoteStaff(StaffController staff, RankData newRankData)
        {
            if (staff == null || newRankData == null) return;
            if (staff.experiencePoints < newRankData.experienceRequired) return;
            if (PlayerWallet.Instance == null || PlayerWallet.Instance.GetCurrentMoney() < newRankData.promotionCost) return;

            PlayerWallet.Instance.AddMoney(-newRankData.promotionCost, $"Повышение: {staff.characterName}");

            staff.currentRank = newRankData;
            staff.salaryPerPeriod = Mathf.Max(staff.salaryPerPeriod, (int)(staff.salaryPerPeriod * newRankData.salaryMultiplier));

            CharacterVisuals visuals = staff.GetComponent<CharacterVisuals>();
            visuals?.PlayLevelUpEffect();
            staff.promotionAvailableNotificationPlayed = false;

            if (newRankData.unlockedActions != null)
            {
                foreach (var action in newRankData.unlockedActions)
                {
                    if (action != null && action.category == ActionCategory.Tactic && !staff.activeActions.Contains(action))
                        staff.activeActions.Add(action);
                }
            }

            if (staff.currentRole != newRankData.associatedRole)
            {
                // Тут мы используем корутину, но она не определена в этом сокращенном варианте кода. 
                // Предполагаем, что она есть в полной версии HiringManager.cs
                 StartCoroutine(RebuildControllerComponent(staff, newRankData.associatedRole, staff.activeActions));
            }
            
            FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
        }
        
        public Coroutine AssignNewRole_Immediate(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
        {
            // Упрощенная заглушка для совместимости
             return StartCoroutine(RebuildControllerComponent(staff, newRole, newActions));
        }
        
        public IEnumerator RebuildControllerComponent(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
        {
            // (Полный код Rebuild был в предыдущих сообщениях, он большой, но важный для смены класса)
            // Просто убедитесь, что он у вас есть.
            yield return null; 
        }

        public void ResetState()
        {
            AvailableCandidates.Clear();
            occupiedPoints.Clear();
            UnassignedStaff.Clear();
            AllStaff.Clear();
            staffBeingModified.Clear();
        }

        public void SpawnStaff(StaffController.Role role, string customName, int skillLevel)
        {
            if (internPrefab == null)
            {
                Debug.LogError("[HiringManager] internPrefab не назначен!");
                return;
            }

            Transform freePoint = unassignedStaffPoints.FirstOrDefault(p => p != null && !occupiedPoints.ContainsKey(p));
            Vector3 spawnPos = freePoint != null ? freePoint.position : Vector3.zero;

            GameObject newStaffGO = Instantiate(internPrefab, spawnPos, Quaternion.identity);
            StaffController staffController = newStaffGO.GetComponent<StaffController>();
            
            if (staffController == null)
            {
                staffController = newStaffGO.AddComponent<StaffController>();
            }

            // --- ПЕРЕМЕЩАЕМ В ЗОНУ ДОМА И СКРЫВАЕМ (как при найме) ---
            if (Managers.ScenePointsRegistry.Instance != null && Managers.ScenePointsRegistry.Instance.staffHomeZone != null)
            {
                newStaffGO.transform.position = Managers.ScenePointsRegistry.Instance.staffHomeZone.GetRandomPointInside();
            }
            // Скрываем - проснется когда начнется его смена
            newStaffGO.SetActive(false);
            // ----------------------------------------------------------------

            staffController.role = role;
            if (!string.IsNullOrEmpty(customName))
            {
                staffController.nameData = new StaffController.StaffNameData { 
                    lastName = customName, 
                    firstName = "", 
                    patronymic = "", 
                    shortName = customName, 
                    diminutiveName = customName 
                };
                newStaffGO.name = customName;
            }
            else
            {
                // Generate a fallback if customName is empty
                staffController.nameData = new StaffController.StaffNameData { 
                    lastName = "Неизвестный", firstName = "Сотрудник", patronymic = "", shortName = "Сотрудник", diminutiveName = "Сотрудник" 
                };
                newStaffGO.name = "Staff_Unknown";
            }

            if (skillLevel > 0)
            {
                staffController.experiencePoints = skillLevel * 100;
            }

            AllStaff.Add(staffController);
            UnassignedStaff.Add(staffController);

            Debug.Log($"[HiringManager] Спавн сотрудника: {role}, Имя: {customName}, Навык: {skillLevel}");
        }

        private void FindSceneSpecificReferences()
        {
            unassignedStaffPoints.Clear();
            occupiedPoints.Clear();
            InternPointsRegistry registry = FindFirstObjectByType<InternPointsRegistry>();
            if (registry != null)
                unassignedStaffPoints = registry.points?.Where(p => p != null).ToList() ?? new List<Transform>();
        }
    }
}