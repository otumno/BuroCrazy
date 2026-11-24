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
using Scriptables.Progression;

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
        // Дебаг: на любой день минимум 1 специалист (по одной вакансии каждой роли).
        public AnimationCurve specialistCountOverTime = new AnimationCurve(new Keyframe(1, 1), new Keyframe(10, 2), new Keyframe(30, 3));
        [Tooltip("Шанс кандидата с рангом выше начального. Работает, только если такой ранг роли открыт карьерой Директора.")]
        public AnimationCurve experiencedInternChance = new AnimationCurve(new Keyframe(1, 0), new Keyframe(5, 0.1f), new Keyframe(30, 0.5f));

        [Header("Стоимость найма")]
        public int baseCost = 100;
        public int costPerSkillPoint = 150;
        [Tooltip("Множитель стоимости найма временного сотрудника (на текущий день).")]
        [Range(0f, 1f)]
        public float temporaryHiringCostMultiplier = 0.5f;

        [Header("Доступ к найму (карьера Директора)")]
        [Tooltip("Что доступно для найма без должностей. Остальное открывают JobTitleData.hiringAccessRules и unlockedRoles.")]
        public List<HiringAccessRule> startingHiringAccess = new List<HiringAccessRule>
        {
            new HiringAccessRule { role = StaffController.Role.Intern, maxRankLevel = 0 }
        };

        // --- Списки Имен ---
        // Все списки имён, фамилий и патронимов перенесены в Utilities.NameGenerator
        // (статические массивы на 40+ записей в стиле «отражений»).
        // EN-локализация в NameGenerator закомментирована и готова к включению.
        [System.Obsolete("Используйте Utilities.NameGenerator для генерации имён.")]
        private struct NameSet { public string full; public string shortName; public string diminutive; }

        public List<Candidate> AvailableCandidates { get; private set; } = new List<Candidate>();
        private List<StaffController> staffBeingModified = new List<StaffController>();
        private readonly Dictionary<StaffController, int> pendingArrivals = new Dictionary<StaffController, int>();
        private readonly Dictionary<StaffController, float> arrivalReadyTimes = new Dictionary<StaffController, float>();
        private readonly Dictionary<StaffController, int> arrivalRollDays = new Dictionary<StaffController, int>();

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
                TimeManager.Instance.OnDayChanged += TimeManager_DayChanged;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                if (TimeManager.Instance != null)
                {
                    TimeManager.Instance.OnPeriodChanged -= OnTimePeriodChanged;
                    TimeManager.Instance.OnDayChanged -= TimeManager_DayChanged;
                }
            }
        }

        private void OnTimePeriodChanged(PeriodSettings settings)
        {
            CheckAllStaffShiftsImmediately();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var staff in AllStaff)
            {
                if (staff == null) continue;
                Debug.Log($"[StaffShiftTrace] {staff.characterName}: день={TimeManager.Instance.GetCurrentDay()}, период={settings.PeriodType}, тип={staff.employmentType}, найм={staff.hireDay}/{staff.hirePeriod}, смена={staff.WorkShiftMask}, допущен={staff.HasEmploymentStarted()}, активен={staff.gameObject.activeSelf}, ушёл={staff.hasLeftToday}, опоздание={staff.currentLateness}, ожидает={pendingArrivals.ContainsKey(staff)}, выход={staff.ShiftSequence}");
            }
#endif
        }

        // Дневные флаги прихода раньше не сбрасывались. CheckAllStaffShiftsImmediately не пересчитывает приход,
        // пока hasArrivedToday == true, поэтому заболевший оставался на больничном навсегда.
        // OnDayChanged приходит раньше OnPeriodChanged, так что к проверке смен флаги уже чистые.
        private void TimeManager_DayChanged(int day)
        {
            pendingArrivals.Clear();
            arrivalReadyTimes.Clear();
            foreach (var staff in AllStaff)
            {
                // Тем, кто ещё на смене (ночные смены), день не обрываем.
                if (staff == null || staff.gameObject.activeSelf) continue;

                staff.ResetDailySchedule();
            }
        }

        private IEnumerator RegisterExistingStaffAndAssignDatabases()
        {
            yield return new WaitForEndOfFrame();
            // Не Clear(): к этому моменту SaveLoadManager мог уже пересоздать сотрудников из сейва,
            // а они выключены (сидят дома) — FindObjectsByType без Include их бы не нашёл, и они выпали бы из AllStaff.
            AllStaff.RemoveAll(s => s == null);
            UnassignedStaff.RemoveAll(s => s == null);
            StaffController[] existingStaff = FindObjectsByType<StaffController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

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
                case StaffController.Role.Janitor:
                case StaffController.Role.ServiceWorker: return typeof(ServiceWorkerController);
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

        /// <summary>
        /// Можно ли нанять кандидата на указанных условиях (без учёта денег).
        /// </summary>
        public bool CanHireAs(Candidate candidate, EmploymentType employmentType)
        {
            if (candidate == null) return false;

            if (employmentType == EmploymentType.Temporary)
            {
                if (candidate.Availability == EmploymentAvailability.PermanentOnly) return false;

                // На текущий день: хотя бы один рабочий период должен оставаться до ночи.
                return TryBuildTemporaryShiftMask(GetShiftPeriodsCount(candidate.Rank), out _, out _);
            }

            return candidate.Availability != EmploymentAvailability.TemporaryOnly;
        }

        /// <summary>
        /// Сколько игрок платит при найме. Временный — предоплата всего за раз:
        /// найм со скидкой плюс полная смена (workPeriodsCount периодов ранга). За периоды ему потом не начисляется.
        /// </summary>
        public int GetHiringCost(Candidate candidate, EmploymentType employmentType)
        {
            if (candidate == null)
                return 0;
            
            if (employmentType == EmploymentType.Permanent)
                return candidate.HiringCost;

            int hiringPart = Mathf.Max(0, Mathf.RoundToInt(candidate.HiringCost * temporaryHiringCostMultiplier));
            TryBuildTemporaryShiftMask(GetShiftPeriodsCount(candidate.Rank), out var paidMask, out _);
            int shiftPart = StaffScheduleMath.CountPeriods(paidMask) * GetSalaryPerPeriod(candidate.Rank);
            return hiringPart + shiftPart;
        }

        public int GetSalaryPerPeriod(RankData rank)
        {
            return rank != null && rank.salaryMultiplier > 0 ? Mathf.RoundToInt(baseCost * rank.salaryMultiplier) : baseCost;
        }

        // Длина смены по рангу; 3 — как в панели расписания, если ранга нет.
        public static int GetShiftPeriodsCount(RankData rank)
        {
            return rank != null ? rank.workPeriodsCount : 3;
        }

        /// <summary>
        /// Смена временного: periodsCount периодов подряд, начиная со следующего после текущего, в порядке календаря.
        /// Работает только до ближайшей ночи в текущем дне. Ночью найм на завтра запрещён.
        /// </summary>
        /// <param name="isNextDay">true, если смена выпала уже на следующий день.</param>
        /// <returns>true, если смена помещается целиком (все periodsCount периодов).</returns>
        private static bool TryBuildTemporaryShiftMask(int periodsCount, out Data.Calendar.CalendarDayPeriodType mask, out bool isNextDay)
        {
            isNextDay = false;
            mask = Data.Calendar.CalendarDayPeriodType.None;

            var periods = TimeManager.Instance != null && TimeManager.Instance.mainCalendarDay != null
                ? TimeManager.Instance.mainCalendarDay.periodSettings
                : null;
            if (periods == null || periods.Count == 0) return false;

            int currentIndex = periods.FindIndex(p => p.PeriodType == TimeManager.Instance.GetCurrentPeriodType());
            if (currentIndex < 0) return false;

            mask = StaffScheduleMath.BuildTemporaryShift(periods, currentIndex, periodsCount);
            return mask != CalendarDayPeriodType.None;
        }

        /// <summary>
        /// Смена постоянного по умолчанию: periodsCount дневных периодов подряд, начиная с утра.
        /// Если календаря нет, остаётся одно утро, как было раньше.
        /// </summary>
        private static Data.Calendar.CalendarDayPeriodType BuildMorningShiftMask(int periodsCount)
        {
            const Data.Calendar.CalendarDayPeriodType fallbackMask = Data.Calendar.CalendarDayPeriodType.Morning;

            var periods = TimeManager.Instance != null && TimeManager.Instance.mainCalendarDay != null
                ? TimeManager.Instance.mainCalendarDay.periodSettings
                : null;
            if (periods == null) return fallbackMask;

            int startIndex = periods.FindIndex(p => p.PeriodType == Data.Calendar.CalendarDayPeriodType.Morning);
            if (startIndex < 0) return fallbackMask;

            var mask = Data.Calendar.CalendarDayPeriodType.None;
            int collected = 0;
            for (int index = startIndex; index < periods.Count && collected < periodsCount; index++)
            {
                var periodType = periods[index].PeriodType;
                if (periodType.IsNight()) break;

                mask |= periodType;
                collected++;
            }

            return mask == Data.Calendar.CalendarDayPeriodType.None ? fallbackMask : mask;
        }

        private static CalendarDayPeriodType BuildNextPeriodShiftMask(int periodsCount)
        {
            var clock = TimeManager.Instance;
            var periods = clock != null ? clock.mainCalendarDay?.periodSettings : null;
            if (periods == null || periods.Count == 0) return BuildMorningShiftMask(periodsCount);
            int current = periods.FindIndex(p => p.PeriodType == clock.GetCurrentPeriodType());
            if (current < 0) return BuildMorningShiftMask(periodsCount);
            // Постоянный сотрудник может работать ночью: смена циклически продолжается
            // после границы дня, вместо ожидания следующего утра.
            return StaffScheduleMath.BuildShift(periods, (current + 1) % periods.Count, periodsCount);
        }

        private Transform GetFreeStaffPoint()
        {
            return unassignedStaffPoints.FirstOrDefault(p => p != null && !occupiedPoints.ContainsKey(p));
        }

        private RoleData GetRoleData(StaffController.Role role)
        {
            return allRoleData?.FirstOrDefault(data => data != null && data.roleType == role);
        }

        /// <summary>
        /// Создаёт сотрудника с контроллером нужной роли — выключенным, в зоне дома.
        /// Общая часть найма и загрузки сейва.
        /// </summary>
        private StaffController CreateStaffObject(StaffController.Role role, Transform freePoint)
        {
            // Используем единый префаб, скрипты накинутся сами
            if (internPrefab == null) { Debug.LogWarning($"[HiringManager] Prefab не найден!"); return null; }

            // Если точек нет, спавним просто где-то (чтобы найм не ломался)
            Vector3 spawnPos = freePoint != null ? freePoint.position : Vector3.zero;
            GameObject newStaffGO = Instantiate(internPrefab, spawnPos, Quaternion.identity);

            // --- ПЕРЕМЕЩАЕМ В ЗОНУ ДОМА И СКРЫВАЕМ ---
            if (Managers.ScenePointsRegistry.Instance != null && Managers.ScenePointsRegistry.Instance.staffHomeZone != null)
            {
                newStaffGO.transform.position = Managers.ScenePointsRegistry.Instance.staffHomeZone.GetRandomPointInside();
            }

            // Сразу выключаем объект - он появится только когда менеджер смен решит его разбудить
            newStaffGO.SetActive(false);

            StaffController staffController = newStaffGO.GetComponent<StaffController>();
            if (staffController == null) staffController = newStaffGO.AddComponent<StaffController>(); // Fallback

            // Сразу меняем тип контроллера на правильный
            var targetType = GetControllerTypeForRole(role);
            if (targetType != null && staffController.GetType() != targetType)
            {
                RemoveRoleNotifications(newStaffGO);

                // На выключенном новом объекте должен остаться ровно один контроллер.
                // Иначе GetComponent возвращал базовый компонент вместо специализированного.
                DestroyImmediate(staffController);
                staffController = (StaffController)newStaffGO.AddComponent(targetType);
            }

            if (staffController == null)
            {
                Destroy(newStaffGO);
                return null;
            }

            staffController.currentRole = role;
            if (staffController is ClerkController clerk)
                clerk.clerkRole = GetClerkRoleFromStaffRole(role);

            // Инициализируем базовые компоненты как для существующих сотрудников
            var agentMover = newStaffGO.GetComponent<AgentMover>();
            var visuals = newStaffGO.GetComponent<CharacterVisuals>();
            var logger = newStaffGO.GetComponent<CharacterStateLogger>();
            if (agentMover != null && visuals != null && logger != null)
            {
                staffController.ForceInitializeBaseComponents(agentMover, visuals, logger);
            }

            EnsureRoleNotification(staffController);
            return staffController;
        }

        private static void RemoveRoleNotifications(GameObject go)
        {
            // RequireComponent prevents replacing a controller until its old notification is removed.
            foreach (var component in go.GetComponents<MonoBehaviour>())
                if (component is InternNotification || component is ClerkNotification || component is GuardNotification || component is ServiceWorkerNotification)
                    DestroyImmediate(component);
        }

        private static void EnsureRoleNotification(StaffController staff)
        {
            System.Type notification = staff is InternController ? typeof(InternNotification)
                : staff is ClerkController ? typeof(ClerkNotification)
                : staff is GuardMovement ? typeof(GuardNotification)
                : staff is ServiceWorkerController ? typeof(ServiceWorkerNotification) : null;
            if (notification != null && staff.GetComponent(notification) == null) staff.gameObject.AddComponent(notification);
        }

        /// <summary>
        /// Пересоздаёт нанятого сотрудника из сейва: выключенным дома, смену начнёт CheckAllStaffShiftsImmediately.
        /// Рабочее место назначает SaveLoadManager — общим кодом со старым форматом сейва.
        /// Перед серией вызовов нужен DestroyAllStaff, иначе штат задвоится.
        /// </summary>
        public StaffController RestoreStaff(StaffSaveData data)
        {
            Transform freePoint = GetFreeStaffPoint();
            StaffController staff = CreateStaffObject(data.role, freePoint);
            if (staff == null) return null;

            staff.nameData = data.nameData;
            // Сразу обёртку, а не CharacterSkills: неявное приведение копирует значения,
            // и правка исходного объекта потом не дошла бы до сотрудника.
            staff.skills = new StaffController.CharacterSkillsWrapper
            {
                paperworkMastery = data.paperworkMastery,
                sedentaryResilience = data.sedentaryResilience,
                pedantry = data.pedantry,
                softSkills = data.softSkills,
                corruption = data.corruption
            };
            staff.gender = data.gender;
            staff.currentRole = data.role;
            staff.currentRank = rankDatabase?.FirstOrDefault(r => r != null && r.name == data.rankName)
                                ?? rankDatabase?.FirstOrDefault(r => r != null && r.associatedRole == data.role && r.rankLevel == 0);
            staff.experiencePoints = data.experience;
            staff.salaryPerPeriod = data.salary;
            staff.permanentTrait = data.trait;
            staff.activeActions = FindActionsByName(data.activeActionNames);

            RoleData roleData = GetRoleData(data.role);
            if (roleData != null) staff.InitializeFromData(roleData);
            // После InitializeFromData: он сбрасывает dirtyHands бухгалтера в базовое значение роли.
            staff.skills.dirtyHands = data.dirtyHands;

            // Старые сейвы не содержат срока найма: сохраняем прежнее поведение для них.
            // Новые записи не должны выводить сотрудника раньше оплаченного периода после загрузки.
            staff.employmentType = data.employmentType;
            staff.hireDay = data.hasEmploymentTiming ? data.hireDay : -1;
            staff.hirePeriod = data.hasEmploymentTiming ? data.hirePeriod : CalendarDayPeriodType.None;
            staff.temporaryWorkDay = data.hasEmploymentTiming ? data.temporaryWorkDay
                : (TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : -1);
            staff.WorkShiftMask = data.workShiftMask;
            staff.uiScheduleTrackIndex = data.scheduleTrackIndex;
            staff.unpaidPeriods = data.unpaidPeriods;
            staff.missedPaymentCount = data.missedPaymentCount;
            staff.gameObject.name = data.gameObjectName;

            AllStaff.Add(staff);
            if (data.assignedWorkstationId == -999) UnassignedStaff.Add(staff);
            if (freePoint != null) occupiedPoints.Add(freePoint, staff);

            RestoreAttendance(staff, data, TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : -1);
            return staff;
        }

        public void CaptureAttendance(StaffController staff, ref StaffSaveData data, int day)
        {
            bool working = staff.gameObject.activeInHierarchy && staff.IsOnDuty() && staff.HasStartedAnyShift;
            data.hasAttendanceState = working || (arrivalRollDays.TryGetValue(staff, out int rolledDay) && rolledDay == day);
            data.attendanceDay = day;
            data.arrivalDelayRemaining = working ? 0 : arrivalReadyTimes.TryGetValue(staff, out float readyAt)
                ? Mathf.Max(0, readyAt - Time.time) : staff.currentLateness;
        }

        public void RestoreAttendance(StaffController staff, StaffSaveData data, int day)
        {
            if (!data.hasAttendanceState || data.attendanceDay != day) return;
            // Loading must not reroll illness or restart a completed lateness wait.
            arrivalRollDays[staff] = day;
            staff.currentLateness = data.arrivalDelayRemaining < 0 ? -1 : Mathf.Max(0, data.arrivalDelayRemaining);
            Debug.Log($"[StaffShiftTrace] Восстановлен выход {staff.characterName}: день={day}, ожидание={staff.currentLateness:F1}");
        }

        // Действия ищем и в ActionDatabase, и в рангах: тактики рангов в общую базу не входят.
        private List<StaffAction> FindActionsByName(List<string> actionNames)
        {
            var result = new List<StaffAction>();
            if (actionNames == null) return result;

            var knownActions = new List<StaffAction>();
            var actionDatabase = Resources.Load<ActionDatabase>("Databases/ActionDatabase");
            if (actionDatabase != null && actionDatabase.allActions != null) knownActions.AddRange(actionDatabase.allActions);
            if (rankDatabase != null)
            {
                foreach (var rank in rankDatabase)
                {
                    if (rank != null && rank.unlockedActions != null) knownActions.AddRange(rank.unlockedActions);
                }
            }

            foreach (var actionName in actionNames)
            {
                var action = knownActions.FirstOrDefault(a => a != null && a.name == actionName);
                if (action != null && !result.Contains(action)) result.Add(action);
                else if (action == null) Debug.LogWarning($"[HiringManager] Действие '{actionName}' из сейва не найдено.");
            }

            return result;
        }

        public bool HireCandidate(Candidate candidate, EmploymentType employmentType = EmploymentType.Permanent)
        {
            if (candidate == null || PlayerWallet.Instance == null) return false;
            if (!CanHireAs(candidate, employmentType)) return false;

            int hiringCost = GetHiringCost(candidate, employmentType);
            if (PlayerWallet.Instance.GetCurrentMoney() < hiringCost) return false;

            Transform freePoint = GetFreeStaffPoint();
            RoleData roleData = GetRoleData(candidate.Role);
            if (roleData == null) Debug.LogWarning($"[HiringManager] RoleData не найден для роли {candidate.Role}");

            StaffController staffController = CreateStaffObject(candidate.Role, freePoint);
            if (staffController == null) return false;
            GameObject newStaffGO = staffController.gameObject;

            staffController.nameData = candidate.NameData;
            staffController.skills = candidate.Skills;
            staffController.gender = candidate.Gender;
            staffController.currentRank = candidate.Rank;
            staffController.experiencePoints = candidate.Experience;
            staffController.salaryPerPeriod = GetSalaryPerPeriod(candidate.Rank);
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
            
            staffController.employmentType = employmentType;
            int currentDay = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : 0;
            if (TimeManager.Instance != null)
            {
                staffController.hireDay = currentDay;
                staffController.hirePeriod = TimeManager.Instance.GetCurrentPeriodType();
            }

            // Оба типа найма выходят со следующего периода без ручного планирования.
            if (employmentType == EmploymentType.Temporary)
            {
                // CanHireAs проверил наличие хотя бы одного дневного периода.
                TryBuildTemporaryShiftMask(GetShiftPeriodsCount(candidate.Rank), out var shiftMask, out bool isNextDay);
                staffController.WorkShiftMask = shiftMask;
                staffController.temporaryWorkDay = isNextDay ? currentDay + 1 : currentDay;
            }
            else
            {
                staffController.WorkShiftMask = BuildNextPeriodShiftMask(GetShiftPeriodsCount(candidate.Rank));
            }

            newStaffGO.name = $"{candidate.NameData.lastName} {candidate.NameData.firstName}";

            AllStaff.Add(staffController);
            UnassignedStaff.Add(staffController);
            newStaffGO.name = candidate.Name;
            if(freePoint != null) occupiedPoints.Add(freePoint, staffController);
            AvailableCandidates.Remove(candidate);

            // Логирование найма с трейтом
            string traitName = StaffController.TraitLibrary[candidate.Trait].Name;
            string employmentLabel = employmentType == EmploymentType.Temporary ? "временный" : "постоянный";
            Debug.Log($"<color=cyan>[HIRING]</color> Нанят сотрудник {candidate.Name} ({employmentLabel}). Особенность: <b>{traitName}</b>");

            // Лог в Телетайп
            if (TeletypeManager.Instance != null)
            {
                TeletypeManager.Instance.LogImportant($"Новый сотрудник ({employmentLabel}): {candidate.NameData.shortName}. Особенность: {traitName}");
            }

            PlayerWallet.Instance.AddMoney(-hiringCost, $"Наём: {candidate.Name}");

            // Смену сразу НЕ запускаем: сотрудник остаётся выключенным дома, а CheckAllStaffShiftsImmediately
            // разбудит его со следующего периода (см. StaffController.HasEmploymentStarted).
            Debug.Log($"[HiringManager] Найм: {staffController.characterName}, тип={employmentType}, день={currentDay}, период={staffController.hirePeriod}, смена={staffController.WorkShiftMask}, стоимость={hiringCost}");

            FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();

            // Обновляем панель расписания если она открыта
            var schedulePanel = FindFirstObjectByType<StaffSchedulePanelUI>(FindObjectsInactive.Include);
            if (schedulePanel != null)
            {
                schedulePanel.RefreshTable();
            }

            // Ачивка: найм клерка
            if (candidate.Role == StaffController.Role.Clerk)
            {
                AchievementManager.Instance?.UnlockAchievement("Achv_ExperiencedClerkStory");
            }

            return true;
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

        /// <summary>
        /// Убирает всех нанятых сотрудников. Нужно перед загрузкой сейва поверх уже идущей сцены
        /// (перезагрузка после отстранения), иначе RestoreStaff задвоит штат.
        /// </summary>
        public void DestroyAllStaff()
        {
            pendingArrivals.Clear();
            arrivalReadyTimes.Clear();
            arrivalRollDays.Clear();
            foreach (var staff in AllStaff.ToList())
            {
                if (staff != null) staff.FireAndGoHome();
            }

            AllStaff.Clear();
            UnassignedStaff.Clear();
            occupiedPoints.Clear();
            staffBeingModified.Clear();
        }

        public List<StaffController> GetTemporaryStaff()
        {
            return AllStaff.Where(s => s != null && s.IsTemporary).ToList();
        }

        /// <summary>
        /// Максимальный уровень ранга, с которым можно нанять роль: стартовый доступ плюс все открытые должности Директора.
        /// </summary>
        /// <returns>-1, если роль для найма закрыта.</returns>
        public int GetMaxHireRankLevel(StaffController.Role role)
        {
            int maxRankLevel = GetMaxRankLevel(startingHiringAccess, role);

            var progression = ProgressionManager.Instance;
            if (progression == null || progression.allJobsDatabase == null) return maxRankLevel;

            foreach (var job in progression.allJobsDatabase)
            {
                if (job == null || !progression.IsJobUnlocked(job.jobID)) continue;

                // unlockedRoles — старый формат: открывает роль только с начальным рангом.
                if (job.unlockedRoles != null && job.unlockedRoles.Contains(role))
                    maxRankLevel = Mathf.Max(maxRankLevel, 0);

                maxRankLevel = Mathf.Max(maxRankLevel, GetMaxRankLevel(job.hiringAccessRules, role));
            }

            return maxRankLevel;
        }

        public bool IsRoleHireable(StaffController.Role role) => GetMaxHireRankLevel(role) >= 0;

        private static int GetMaxRankLevel(List<HiringAccessRule> rules, StaffController.Role role)
        {
            int maxRankLevel = -1;
            if (rules == null) return maxRankLevel;

            foreach (var rule in rules)
            {
                if (rule != null && rule.role == role)
                    maxRankLevel = Mathf.Max(maxRankLevel, rule.maxRankLevel);
            }

            return maxRankLevel;
        }

        public void RemoveStaff(StaffController staff)
        {
            if (staff == null) return;
            pendingArrivals.Remove(staff);
            arrivalReadyTimes.Remove(staff);
            arrivalRollDays.Remove(staff);
            AllStaff.Remove(staff);
            UnassignedStaff.Remove(staff);
            staffBeingModified.RemoveAll(s => s == null || s == staff);
        }

        public void CheckAllStaffShiftsImmediately()
        {
            var clock = TimeManager.Instance;
            if (clock == null) return;
            var period = clock.GetCurrentPeriodType();
            int day = clock.GetCurrentDay();
            foreach (var staff in AllStaff.ToList())
            {
                if (staff == null || !staff.HasEmploymentStarted()) continue;
                if (staff.IsTemporary && (period.IsNight() || (staff.temporaryWorkDay >= 0 && day != staff.temporaryWorkDay))) continue;
                if ((staff.WorkShiftMask & period) == 0 || staff.gameObject.activeSelf || pendingArrivals.ContainsKey(staff)) continue;

                if (!arrivalRollDays.TryGetValue(staff, out int rolledDay) || rolledDay != day)
                {
                    arrivalRollDays[staff] = day;
                    // Первый выход после покупки гарантирован: случайное опоздание/болезнь
                    // не должны отменять обещанный игроку быстрый результат найма.
                    if (!staff.HasStartedAnyShift && staff.hireDay >= 0)
                        staff.currentLateness = 0;
                    else
                        staff.CalculateArrivalTime();
                    if (staff.currentLateness < 0)
                        TeletypeManager.Instance?.LogImportant($"ВНИМАНИЕ: {staff.characterName} взял больничный и сегодня не выйдет!");
                }
                if (staff.currentLateness < 0) continue;
                if (staff.currentLateness > 0)
                {
                    pendingArrivals[staff] = day;
                    arrivalReadyTimes[staff] = Time.time + staff.currentLateness;
                    StartCoroutine(DelayedStartShift(staff, staff.currentLateness, day));
                }
                else
                {
                    Debug.Log($"[HiringManager] Выход на смену: {staff.characterName}, день={day}, период={period}, смена={staff.WorkShiftMask}");
                    staff.StartShift();
                }
            }
        }

        private IEnumerator DelayedStartShift(StaffController staff, float delay, int scheduledDay)
        {
            yield return new WaitForSeconds(delay);
            // An old day's delayed arrival must not erase a new day's pending arrival.
            if (!pendingArrivals.TryGetValue(staff, out int pendingDay) || pendingDay != scheduledDay) yield break;
            pendingArrivals.Remove(staff);
            arrivalReadyTimes.Remove(staff);
            var clock = TimeManager.Instance;
            if (staff == null || !AllStaff.Contains(staff) || clock == null || clock.GetCurrentDay() != scheduledDay) yield break;
            staff.currentLateness = 0;
            // F10 and a changed schedule can end the shift while the lateness timer is running.
            if ((staff.WorkShiftMask & clock.GetCurrentPeriodType()) == 0 || staff.gameObject.activeSelf) yield break;
            if (staff.IsTemporary && clock.IsNight()) yield break;
            staff.currentLateness = 0;
            staff.StartShift();
            staff.thoughtBubble?.ShowPriorityMessage("Ох, пробки...", 3f, Color.yellow);
        }

        // ... (GenerateNewCandidates и прочее оставляем)
        public void GenerateNewCandidates()
        {
            AvailableCandidates.Clear();
            int currentDay = CalendarManager.Instance != null ? CalendarManager.Instance.CurrentDay : 1;

            int internsToCreate = Mathf.Max(0, Mathf.RoundToInt(internCountOverTime.Evaluate(currentDay)));
            int specialistsToCreate = Mathf.Max(0, Mathf.RoundToInt(specialistCountOverTime.Evaluate(currentDay)));
            float experiencedChance = Mathf.Clamp01(experiencedInternChance.Evaluate(currentDay));

            // Только роли, открытые карьерой Директора (на старте — никого, кроме стажёров).
            var specialistRoles = System.Enum.GetValues(typeof(StaffController.Role))
                .Cast<StaffController.Role>()
                .Where(r => r != StaffController.Role.Intern && r != StaffController.Role.Unassigned && r != StaffController.Role.Director)
                .Where(IsRoleHireable)
                .ToList();

            int totalSpecialists = specialistRoles.Count > 0 ? specialistsToCreate : 0;

            if (totalSpecialists >= 1 && totalSpecialists <= 3)
            {
                foreach (var role in specialistRoles)
                {
                    Candidate candidate = CreateRandomCandidate(role, experiencedChance);
                    if (candidate != null) AvailableCandidates.Add(candidate);
                }
            }
            else
            {
                for (int i = 0; i < totalSpecialists; i++)
                {
                    StaffController.Role randomRole = specialistRoles[Random.Range(0, specialistRoles.Count)];
                    Candidate newSpecialist = CreateRandomCandidate(randomRole, experiencedChance);
                    if (newSpecialist != null) AvailableCandidates.Add(newSpecialist);
                }
            }

            if (!IsRoleHireable(StaffController.Role.Intern)) return;

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

            // Создаем StaffNameData через новый генератор вымышленных имён
            candidate.NameData = NameGenerator.BuildStaffName(candidate.Gender);

            if (!string.IsNullOrEmpty(candidate.NameData.firstName))
            {
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

            // Ранг выше начального — только если его открыла карьера Директора.
            int maxRankLevel = GetMaxHireRankLevel(role);
            RankData startingRank = null;
            if (maxRankLevel > 0 && Random.value < experiencedChance)
            {
                var experiencedRanks = rankDatabase
                    .Where(r => r != null && r.associatedRole == role && r.rankLevel > 0 && r.rankLevel <= maxRankLevel)
                    .ToList();
                if (experiencedRanks.Count > 0)
                    startingRank = experiencedRanks[Random.Range(0, experiencedRanks.Count)];
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

                // Ачивка: повышение стажёра до клерка
                if (newRankData.associatedRole == StaffController.Role.Clerk)
                {
                    AchievementManager.Instance?.UnlockAchievement("Achv_ExperiencedClerkStory");
                }
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
            if (staff == null) yield break;
            var targetType = GetControllerTypeForRole(newRole);
            if (targetType == null) yield break;
            bool onDuty = staff.gameObject.activeInHierarchy && staff.IsOnDuty();
            long sequence = staff.ShiftSequence;
            bool startedBefore = staff.HasStartedAnyShift;
            int attendanceDay = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : -1;
            var attendance = new StaffSaveData();
            CaptureAttendance(staff, ref attendance, attendanceDay);
            staff.PrepareForRoleChange();
            AssignmentManager.Instance?.UnassignStaff(staff);
            staff.assignedWorkstation = null;
            var replacement = staff;
            if (staff.GetType() != targetType)
            {
                // Preserve serialized identity, rank, needs, skills and employment timing, not just the new role.
                string state = JsonUtility.ToJson(staff);
                var go = staff.gameObject;
                bool active = go.activeSelf;
                go.SetActive(false);
                int staffIndex = AllStaff.IndexOf(staff);
                bool unassigned = UnassignedStaff.Remove(staff);
                staffBeingModified.Remove(staff);
                pendingArrivals.Remove(staff);
                arrivalReadyTimes.Remove(staff);
                arrivalRollDays.Remove(staff);
                var points = occupiedPoints.Where(p => p.Value == staff).Select(p => p.Key).ToList();
                // GetComponent must never return the obsolete controller when the new role starts.
                RemoveRoleNotifications(go);
                DestroyImmediate(staff);
                replacement = (StaffController)go.AddComponent(targetType);
                JsonUtility.FromJsonOverwrite(state, replacement);
                replacement.ForceInitializeBaseComponents(go.GetComponent<AgentMover>(), go.GetComponent<Characters.CharacterVisuals>(), go.GetComponent<CharacterStateLogger>());
                if (staffIndex >= 0) AllStaff[staffIndex] = replacement;
                if (unassigned) UnassignedStaff.Add(replacement);
                foreach (var point in points) occupiedPoints[point] = replacement;
                if (targetType != typeof(InternController))
                {
                    var notification = go.GetComponent<InternNotification>();
                    if (notification != null) Destroy(notification);
                }
                go.SetActive(active);
            }
            replacement.HasStartedAnyShift = startedBefore;
            replacement.currentRole = newRole;
            EnsureRoleNotification(replacement);
            if (replacement is ClerkController clerk) clerk.clerkRole = GetClerkRoleFromStaffRole(newRole);
            if (newActions != null) replacement.activeActions = new List<StaffAction>(newActions);
            replacement.InitializeFromData(GetRoleData(newRole));
            RestoreAttendance(replacement, attendance, attendanceDay);
            if (onDuty) replacement.ResumeAfterRoleChange(sequence);
            else CheckAllStaffShiftsImmediately();
            Debug.Log($"[StaffRoleTrace] {replacement.characterName}: роль={newRole}, контроллер={replacement.GetType().Name}, на смене={onDuty}");
            FindFirstObjectByType<HiringPanelUI>(FindObjectsInactive.Include)?.RefreshTeamList();
            yield break;
        }

        public void ResetState()
        {
            // Через DestroyAllStaff, а не просто очистку списков: сброс бывает и поверх идущей сцены
            // (перезагрузка после отстранения), и объекты сотрудников остались бы в ней без учёта.
            DestroyAllStaff();
            AvailableCandidates.Clear();
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

            // Без этого остаётся маска из префаба: у стажёра она пустая и превращается в одно утро (InternController.StartShift),
            // хотя ранг даёт больше рабочих периодов.
            staffController.WorkShiftMask = BuildMorningShiftMask(GetShiftPeriodsCount(staffController.currentRank));

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
