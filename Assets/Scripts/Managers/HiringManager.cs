// Assets/Scripts/Managers/HiringManager.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;
using Characters;

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

        // --- Списки Имен (Сокращено для краткости, они у вас есть) ---
        private List<string> firstNamesMale = new List<string> { "Аркадий", "Иннокентий", "Пантелеймон", "Акакий" }; // и т.д.
        private List<string> firstNamesFemale = new List<string> { "Аглая", "Евпраксия", "Пелагея", "Серафима" }; // и т.д.
        private List<string> lastNames = new List<string> { "Перепискин", "Протоколов", "Архивариусов", "Гербовый" }; // и т.д.
        private List<string> patronymicsMale = new List<string> { "Аркадьевич", "Иннокентьевич", "Пантелеймонович" }; // и т.д.
        private List<string> patronymicsFemale = new List<string> { "Аркадьевна", "Иннокентьевна", "Пантелеймоновна" }; // и т.д.

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

        // --- ЗДЕСЬ БЫЛ ОШИБОЧНЫЙ КОД, ОН УДАЛЕН ---

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

        public Coroutine AssignNewRole_Immediate(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
        {
            if (staff == null) return null;
            List<StaffAction> actionsToAssign = newActions ?? new List<StaffAction>();

            staff.activeActions = new List<StaffAction>(actionsToAssign);

            System.Type requiredControllerType = GetControllerTypeForRole(newRole);
            System.Type currentControllerType = staff.GetType();

            if (requiredControllerType == null) return null;

            if (requiredControllerType == currentControllerType)
            {
                staff.currentRole = newRole;
                if (staff is ClerkController clerk) clerk.clerkRole = GetClerkRoleFromStaffRole(newRole);
                return null; 
            }
            else
            {
                return StartCoroutine(RebuildControllerComponent(staff, newRole, new List<StaffAction>(actionsToAssign)));
            }
        }

        public IEnumerator RebuildControllerComponent(StaffController staff, StaffController.Role newRole, List<StaffAction> newActions)
        {
            if (staff == null) yield break;
            if (staffBeingModified.Contains(staff)) yield break;
            staffBeingModified.Add(staff);

            GameObject staffGO = staff.gameObject; 
            int staffInstanceID = staffGO.GetInstanceID(); 
            string staffNameForLogs = staff.characterName ?? staffGO.name; 

            yield return new WaitForEndOfFrame(); 
            yield return null; 

            StaffController newControllerReference = null; 

            try
            {
                // Save Data
                string savedName = staff.characterName;
                Gender savedGender = staff.gender;
                // ВНИМАНИЕ: StaffController.CharacterSkillsWrapper неявно приводится к CharacterSkills благодаря оператору в StaffController
                CharacterSkills savedSkills = staff.skills; 
                RankData savedRank = staff.currentRank;
                int savedXP = (int)staff.experiencePoints;
                int savedSalary = staff.salaryPerPeriod;
                int savedUnpaidPeriods = staff.unpaidPeriods;
                int savedMissedPayments = staff.missedPaymentCount;
				Data.Calendar.CalendarDayPeriodType savedMask = staff.WorkShiftMask; // Неявное приведение
                ServicePoint savedWorkstation = staff.assignedWorkstation;
                ActionDatabase savedSystemDb = staff.systemActionDatabase;
                
                AgentMover agentMover = staffGO.GetComponent<AgentMover>();
                CharacterVisuals visuals = staffGO.GetComponent<CharacterVisuals>();
                CharacterStateLogger logger = staffGO.GetComponent<CharacterStateLogger>();

                Object.DestroyImmediate(staff); 
                staff = null; 

                System.Type newControllerType = GetControllerTypeForRole(newRole);
                if(newControllerType != null)
                {
                    Component addedComponent = staffGO.AddComponent(newControllerType);
                    newControllerReference = addedComponent as StaffController;
                }

                if (newControllerReference == null) yield break; 

                // Restore Saved Data
                newControllerReference.characterName = savedName;
                newControllerReference.gender = savedGender;
                newControllerReference.skills = savedSkills; // Неявное приведение CharacterSkills -> CharacterSkillsWrapper
                newControllerReference.currentRank = savedRank;
                newControllerReference.experiencePoints = savedXP;
                newControllerReference.salaryPerPeriod = savedSalary;
                newControllerReference.unpaidPeriods = savedUnpaidPeriods;
                newControllerReference.missedPaymentCount = savedMissedPayments;
                newControllerReference.WorkShiftMask = savedMask; // Неявное приведение
                newControllerReference.activeActions = newActions; 
                newControllerReference.systemActionDatabase = savedSystemDb;
                
                if (savedWorkstation != null && AssignmentManager.Instance != null)
                    AssignmentManager.Instance.AssignStaffToWorkstation(newControllerReference, savedWorkstation);
                else 
                    newControllerReference.assignedWorkstation = null;

                if (agentMover != null && visuals != null && logger != null) 
                    newControllerReference.ForceInitializeBaseComponents(agentMover, visuals, logger);

                RoleData dataForNewRole = allRoleData?.FirstOrDefault(data => data != null && data.roleType == newRole);
                if (dataForNewRole != null)
                {
                    newControllerReference.InitializeFromData(dataForNewRole); 
                    var newGuard = newControllerReference.GetComponent<GuardMovement>();
					if (newGuard != null) newGuard.InitializeFromData(dataForNewRole);
                    else if (newControllerReference is ServiceWorkerController newWorker) newWorker.InitializeFromData(dataForNewRole);
                    else if (newControllerReference is InternController newIntern) newIntern.InitializeFromData(dataForNewRole);
                    else if (newControllerReference is ClerkController newClerk)
                    {
                        newClerk.clerkRole = GetClerkRoleFromStaffRole(newRole);
                        newClerk.allRoleData = this.allRoleData;
                    }
                }

                bool updatedInAllStaff = false;
                for(int i = 0; i < AllStaff.Count; i++) {
                    if (AllStaff[i] == null || AllStaff[i].gameObject.GetInstanceID() == staffInstanceID) {
                        AllStaff[i] = newControllerReference;
                        updatedInAllStaff = true;
                        break;
                    }
                }
                if (!updatedInAllStaff && !AllStaff.Contains(newControllerReference)) AllStaff.Add(newControllerReference);

                for(int i = 0; i < UnassignedStaff.Count; i++) {
                    if (UnassignedStaff[i] == null || UnassignedStaff[i].gameObject.GetInstanceID() == staffInstanceID)
                    {
                        UnassignedStaff[i] = newControllerReference;
                        break;
                    }
                }
            }
            catch (System.Exception ex) {
                Debug.LogError($"CRITICAL ERROR in Rebuild: {ex}");
            }
            finally
            {
                staffBeingModified.RemoveAll(s => s == null || s.gameObject.GetInstanceID() == staffInstanceID);
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

        public void ActivateAllScheduledStaff()
        {
            if (TimeManager.Instance == null) return;
            var periodType = TimeManager.Instance.GetCurrentPeriodType();

            foreach (var staff in AllStaff.ToList())
            {
                if (staff == null) continue;
                
                // Проверка битовой маски через перегруженный оператор в StaffController
                var isScheduledNow = (staff.WorkShiftMask & periodType) != 0;
                
                if (isScheduledNow && !staff.IsOnDuty())
                {
                    staff.StartShift();
                }
            }
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
                AssignNewRole_Immediate(staff, newRankData.associatedRole, staff.activeActions);
            }
            
            FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
        }

        public void ResetState()
        {
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
                unassignedStaffPoints = registry.points?.Where(p => p != null).ToList() ?? new List<Transform>();
        }

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
            
            // Если мало кандидатов (1-3), обеспечиваем хотя бы 1 на каждую роль
            if (totalSpecialists >= 1 && totalSpecialists <= 3)
            {
                // Создаем по 1 кандидату каждой роли
                foreach (var role in specialistRoles)
                {
                    Candidate candidate = CreateRandomCandidate(role, 0f);
                    if (candidate != null) AvailableCandidates.Add(candidate);
                }
            }
            else
            {
                // Обычная логика: создаем специалистов случайных ролей
                for (int i = 0; i < specialistsToCreate; i++)
                {
                    StaffController.Role randomRole = specialistRoles[Random.Range(0, specialistRoles.Count)];
                    Candidate newSpecialist = CreateRandomCandidate(randomRole, 0f);
                    if (newSpecialist != null) AvailableCandidates.Add(newSpecialist);
                }
            }

            // Стажёры создаются независимо
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

            var names = candidate.Gender == Gender.Male ? firstNamesMale : firstNamesFemale;
            var patronymics = candidate.Gender == Gender.Male ? patronymicsMale : patronymicsFemale;
            
            if (lastNames.Any() && names.Any() && patronymics.Any())
            {
                string lname = lastNames[Random.Range(0, lastNames.Count)];
                if (candidate.Gender == Gender.Female && !lname.EndsWith("а")) lname += "а";
                candidate.Name = $"{lname} {names[Random.Range(0, names.Count)]} {patronymics[Random.Range(0, patronymics.Count)]}";
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

            return candidate;
        }

        public bool HireCandidate(Candidate candidate)
        {
            if (candidate == null || PlayerWallet.Instance == null) return false;
            if (PlayerWallet.Instance.GetCurrentMoney() < candidate.HiringCost) return false;

            Transform freePoint = unassignedStaffPoints.FirstOrDefault(p => p != null && !occupiedPoints.ContainsKey(p));
            if (freePoint == null) return false;

            RoleData roleData = allRoleData?.FirstOrDefault(data => data != null && data.roleType == candidate.Role);
            GameObject prefabToSpawn = GetPrefabForRole(candidate.Role);

            if (roleData == null)
            {
                Debug.LogWarning($"[HiringManager] RoleData не найден для роли {candidate.Role}");
                return false;
            }
            
            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"[HiringManager] Prefab не найден для роли {candidate.Role}");
                return false;
            }

            GameObject newStaffGO = Instantiate(prefabToSpawn, freePoint.position, Quaternion.identity);
            StaffController staffController = newStaffGO.GetComponent<StaffController>();

            if (staffController != null)
            {
                staffController.characterName = candidate.Name;
                staffController.skills = candidate.Skills; // Implicit cast
                staffController.gender = candidate.Gender;
                staffController.currentRank = candidate.Rank;
                staffController.experiencePoints = candidate.Experience;
                staffController.salaryPerPeriod = candidate.Rank.salaryMultiplier > 0 ? Mathf.RoundToInt(baseCost * candidate.Rank.salaryMultiplier) : baseCost;
                
                staffController.activeActions = new List<StaffAction>();
                
                staffController.InitializeFromData(roleData);
                
                var existingStaff = AllStaff.FirstOrDefault(s => s != null && s.systemActionDatabase != null);
                if (existingStaff != null) staffController.systemActionDatabase = existingStaff.systemActionDatabase;
                
                // Дефолтная маска смен из ранга
                staffController.WorkShiftMask = 0; 
                if (Managers.TimeManager.Instance?.mainCalendarDay?.periodSettings != null)
                {
                    var allPeriods = Managers.TimeManager.Instance.mainCalendarDay.periodSettings.Select(p => p.PeriodType).ToList();
                    int duration = candidate.Rank != null ? candidate.Rank.workPeriodsCount : 3;
                    for (int i = 0; i < duration; i++)
                    {
                        if (i < allPeriods.Count) staffController.WorkShiftMask |= allPeriods[i]; // Используем оператор |
                    }
                }
                else staffController.WorkShiftMask = CalendarDayPeriodTypeExtensions.FullDay; // Используем оператор =

                AllStaff.Add(staffController);
                UnassignedStaff.Add(staffController);
                newStaffGO.name = candidate.Name;
                occupiedPoints.Add(freePoint, staffController);
                AvailableCandidates.Remove(candidate);

                PlayerWallet.Instance.AddMoney(-candidate.HiringCost, $"Наём: {candidate.Name}");

                var periodType = TimeManager.Instance.GetCurrentPeriodType();
                // Используем оператор & для проверки флага (определен в StaffController)
                if ((staffController.WorkShiftMask & periodType) != 0)
                {
                    staffController.StartShift();
                }

                FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
                return true;
            }
            
            Destroy(newStaffGO);
            return false;
        }

        private GameObject GetPrefabForRole(StaffController.Role role) => internPrefab;

        public void FireStaff(StaffController staffToFire)
        {
            if (staffToFire == null) return;

            AllStaff.Remove(staffToFire);
            UnassignedStaff.Remove(staffToFire);

            // Очистка occupiedPoints
            var pointEntry = occupiedPoints.FirstOrDefault(kvp => kvp.Value == staffToFire);
            if (pointEntry.Key != null)
            {
                occupiedPoints.Remove(pointEntry.Key);
            }

            if (staffToFire.assignedWorkstation != null && AssignmentManager.Instance != null)
                AssignmentManager.Instance.UnassignStaff(staffToFire);

            staffToFire.FireAndGoHome();
            
            // Очистка ссылок в staffBeingModified
            staffBeingModified.RemoveAll(s => s == null || s == staffToFire);
            
            FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
        }

        public void RemoveStaff(StaffController staff)
        {
            if (staff == null) return;
            AllStaff.Remove(staff);
            UnassignedStaff.Remove(staff);
            
            // Очистка occupiedPoints
            var pointsToRemove = occupiedPoints.Where(kvp => kvp.Value == staff).Select(kvp => kvp.Key).ToList();
            foreach (var point in pointsToRemove)
            {
                occupiedPoints.Remove(point);
            }
            
            staffBeingModified.RemoveAll(s => s == null || s == staff);
        }

        public void CheckAllStaffShiftsImmediately()
        {
            if (TimeManager.Instance == null) return;
            var periodType = TimeManager.Instance.GetCurrentPeriodType();

            foreach (var staff in AllStaff.ToList())
            {
                if (staff == null) continue;
                
                // Используем оператор &
                var isScheduledNow = (staff.WorkShiftMask & periodType) != 0;
                var isOnDuty = staff.IsOnDuty(); 

                if (isScheduledNow && !isOnDuty) staff.StartShift();
                else if (!isScheduledNow && isOnDuty) staff.EndShift();
            }
        }
    }
}