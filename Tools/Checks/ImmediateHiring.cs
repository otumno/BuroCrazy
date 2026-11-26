using System;
using System.Linq;
using System.Collections.Generic;
using Scriptables.Progression;
using System.Reflection;
using Data.Calendar;
using Enums;
using Managers;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using Utilities;

public static class ImmediateHiringCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
        const string path = "Assets/Scenes/GameScene.unity";
        var previousScene = SceneManager.GetActiveScene();
        bool alreadyOpen = SceneManager.GetSceneByPath(path).isLoaded;
        // Global waypoint lookups need a loaded gameplay graph. Only mutate our isolated copy.
        var scene = alreadyOpen ? EditorSceneManager.OpenPreviewScene(path) : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (!alreadyOpen) SceneManager.SetActiveScene(scene);
        var oldClock = TimeManager.Instance;
        var oldCalendar = CalendarManager.Instance;
        var oldWallet = PlayerWallet.Instance;
        var oldHiring = HiringManager.Instance;
        var locale = LocalizationSettings.SelectedLocale;
        var originalRandom = UnityEngine.Random.state;
        var sequence = typeof(StaffController).GetField("nextShiftSequence", BindingFlags.NonPublic | BindingFlags.Static);
        var previousSequence = sequence.GetValue(null);
        try
        {
            var roots = scene.GetRootGameObjects();
            var clock = roots.SelectMany(r => r.GetComponentsInChildren<TimeManager>(true)).Single();
            var hiring = roots.SelectMany(r => r.GetComponentsInChildren<HiringManager>(true)).Single();
            var productionTemplate = hiring.internPrefab;
            // Edit Mode skips prefab Awake. Exercise hiring and the actual InternController here;
            // WorkActionAssets verifies the initialized production anatomy and presentation separately.
            var fixture = new GameObject("Hiring fixture");
            SceneManager.MoveGameObjectToScene(fixture, scene);
            var wallet = fixture.AddComponent<PlayerWallet>();
            var template = new GameObject("Intern fixture");
            SceneManager.MoveGameObjectToScene(template, scene);
            template.SetActive(false);
            template.AddComponent<InternController>();
            hiring.internPrefab = template;
            hiring.AllStaff.Clear();
            HiringManager.Instance = hiring;
            SetSingleton(typeof(PlayerWallet), wallet);
            SetSingleton(typeof(CalendarManager), null);
            SetSingleton(typeof(TimeManager), clock);
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ru");
            SetClock(clock, "dayCounter", 7);
            SetClock(clock, "currentPeriodType", CalendarDayPeriodType.Evening);
            wallet.SetMoney(10000);
            var rank = hiring.rankDatabase.First(r => r != null && r.associatedRole == StaffController.Role.Intern && r.rankLevel == 0);
            foreach (var kind in new[] { EmploymentType.Permanent, EmploymentType.Temporary })
            {
                var candidate = new Candidate { Role = StaffController.Role.Intern, Name = "Fixture " + kind, Rank = rank,
                    HiringCost = 80, NameData = new StaffController.StaffNameData { firstName = "Fixture", lastName = kind.ToString(), shortName = "Fixture" } };
                hiring.AvailableCandidates.Add(candidate);
                int before = wallet.GetCurrentMoney();
                int price = hiring.GetHiringCost(candidate, kind, out int paid);
                Require(hiring.HireCandidate(candidate, kind), "Could not hire " + kind);
                var staff = hiring.AllStaff.Last();
                Require(staff.gameObject.scene == scene && staff.gameObject.activeSelf && staff.IsOnDuty() && staff.HasStartedAnyShift &&
                    staff.ShiftSequence > 0 && staff.HasEmploymentStarted() && (staff.WorkShiftMask & CalendarDayPeriodType.Evening) != 0,
                    "Hire was not on shift before HireCandidate returned: " + kind);
                Require(wallet.GetCurrentMoney() == before - price && !hiring.HireCandidate(candidate, kind) && wallet.GetCurrentMoney() == before - price,
                    "A repeated hire charged the wallet twice.");
                PayStaffManager.AccrueCurrentPeriod(staff);
                Require(staff.unpaidPeriods == (kind == EmploymentType.Permanent ? 1 : 0), "Initial period double charged.");
                if (kind == EmploymentType.Temporary)
                    Require(paid == 1 && staff.WorkShiftMask == CalendarDayPeriodType.Evening && staff.temporaryWorkDay == 7,
                        "Evening temp must start now and finish before night.");
            }
            var permanent = hiring.AllStaff.First(s => !s.IsTemporary);
            var periods = clock.mainCalendarDay.periodSettings;
            int morning = periods.FindIndex(p => p.PeriodType == CalendarDayPeriodType.Morning);
            int evening = periods.FindIndex(p => p.PeriodType == CalendarDayPeriodType.Evening);
            long beforeEdit = permanent.ShiftSequence;
            Require(ShiftPlanning.Apply(permanent, morning) && !permanent.IsOnDuty(), "Worker did not finish when the current period was removed.");
            Require(ShiftPlanning.Apply(permanent, evening) && permanent.IsOnDuty() && permanent.gameObject.activeSelf &&
                permanent.ShiftSequence > beforeEdit && permanent.unpaidPeriods == 1, "Rejoining the current shift failed or charged twice.");

            SetClock(clock, "currentPeriodType", CalendarDayPeriodType.Day);
            var oldContract = new StaffSaveData { role = StaffController.Role.Intern, gameObjectName = "Pending legacy hire",
                nameData = new StaffController.StaffNameData { shortName = "Pending" }, hasVitals = true, hasEmploymentTiming = true,
                hireDay = 7, hirePeriod = CalendarDayPeriodType.Day, temporaryWorkDay = 7, employmentType = EmploymentType.Temporary,
                workShiftMask = CalendarDayPeriodType.LateDay | CalendarDayPeriodType.Evening, assignedWorkstationId = -999 };
            var restored = hiring.RestoreStaff(JsonUtility.FromJson<StaffSaveData>(JsonUtility.ToJson(oldContract)));
            hiring.CheckAllStaffShiftsImmediately();
            Require(restored.gameObject.activeSelf && restored.HasStartedAnyShift && restored.WorkShiftMask ==
                (CalendarDayPeriodType.Day | CalendarDayPeriodType.LateDay) && restored.unpaidPeriods == 0, "Pending prepaid slot was delayed or overcharged.");
            foreach (var night in new[] { CalendarDayPeriodType.StartNight, CalendarDayPeriodType.EndNight })
            {
                SetClock(clock, "currentPeriodType", night);
                Require(HiringManager.GetHiringShift(rank, EmploymentType.Temporary) == CalendarDayPeriodType.None &&
                    (HiringManager.GetHiringShift(rank, EmploymentType.Permanent) & night) != 0, "Wrong night contract availability.");
            }
            SetClock(clock, "currentPeriodType", CalendarDayPeriodType.Day);
            hiring.internPrefab = productionTemplate;
            CheckRoleCatalog(scene, hiring, wallet);
            return "PASS: all nine production roles hired on shift with one controller, assigned skills and eligible desks; real HireCandidate activates both contracts synchronously, charges once, pays the partial evening, excludes both night halves for temps and accepts them for permanent hires; live stop/rejoin and prepaid legacy contracts.";
        }
        finally
        {
            SetSingleton(typeof(PlayerWallet), oldWallet);
            SetSingleton(typeof(CalendarManager), oldCalendar);
            SetSingleton(typeof(TimeManager), oldClock);
            HiringManager.Instance = oldHiring;
            sequence.SetValue(null, previousSequence);
            LocalizationSettings.SelectedLocale = locale;
            UnityEngine.Random.state = originalRandom;
            if (alreadyOpen) EditorSceneManager.ClosePreviewScene(scene);
            else
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void CheckRoleCatalog(Scene scene, HiringManager hiring, PlayerWallet wallet)
    {
        var roots = scene.GetRootGameObjects();
        var progression = hiring.gameObject.AddComponent<ProgressionManager>();
        progression.allJobsDatabase = Resources.LoadAll<JobTitleData>("Progression/Jobs").ToList();
        var registry = roots.SelectMany(r => r.GetComponentsInChildren<ScenePointsRegistry>(true)).Single();
        var assignments = roots.SelectMany(r => r.GetComponentsInChildren<AssignmentManager>(true)).Single();
        var previousProgression = ProgressionManager.Instance;
        var previousRegistry = ScenePointsRegistry.Instance;
        var previousAssignments = AssignmentManager.Instance;
        var skills = new HashSet<CharacterSkills>();
        try
        {
            SetSingleton(typeof(ProgressionManager), progression);
            SetSingleton(typeof(ScenePointsRegistry), registry);
            SetSingleton(typeof(AssignmentManager), assignments);
            var unlocked = (HashSet<string>)typeof(ProgressionManager).GetField("unlockedJobIDs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(progression);
            foreach (var job in Resources.LoadAll<JobTitleData>("Progression/Jobs")) unlocked.Add(job.jobID);
            wallet.SetMoney(100000);
            hiring.GenerateNewCandidates();
            var board = hiring.AvailableCandidates.ToArray();
            foreach (var candidate in board) if (candidate.Skills != null) skills.Add(candidate.Skills);
            var expectedRoles = hiring.allRoleData.Where(r => r != null).Select(r => r.roleType).Distinct().ToArray();
            Require(expectedRoles.Length == 9, "Production role catalog incomplete.");
            foreach (var role in expectedRoles)
            {
                var candidate = board.First(c => c.Role == role);
                var contract = hiring.CanHireAs(candidate, EmploymentType.Permanent) ? EmploymentType.Permanent : EmploymentType.Temporary;
                Require(hiring.HireCandidate(candidate, contract), "Failed to hire " + role);
                var worker = hiring.AllStaff.Last();
                Require(worker.gameObject.scene == scene && worker.GetComponents<StaffController>().Length == 1 &&
                    worker.currentRole == role && worker.currentRank == candidate.Rank && worker.IsOnDuty() &&
                    worker.HasStartedAnyShift && worker.ShiftSequence > 0 && worker.gameObject.activeSelf, "Incorrect role or shift: " + role);
                var visuals = worker.GetComponent<Characters.CharacterVisuals>();
                Require(visuals.GetBodyRenderer()?.sprite != null && visuals.GetPortraitSprite() != null &&
                    visuals.GetAttachPoint(Characters.CharacterVisuals.AttachPointType.Hand) != null, "Missing staff presentation: " + role);
                Require(candidate.Rank.unlockedActions.Where(a => a != null).All(a => worker.GetAutonomousActions().Contains(a)),
                    "Missing rank action on hire: " + role);
                if (worker is ClerkController)
                    Require(worker.assignedWorkstation != null && AssignmentManager.CanAssignTo(worker, worker.assignedWorkstation),
                        "No suitable desk on hire: " + role);
                if (role == StaffController.Role.Registrar) CheckTactics(scene, worker);
                worker.WorkShiftMask &= ~TimeManager.Instance.GetCurrentPeriodType();
                worker.EndShift();
                Require(!worker.IsOnDuty(), "Role still on duty after shift end: " + role);
            }
        }
        finally
        {
            SetSingleton(typeof(AssignmentManager), previousAssignments);
            SetSingleton(typeof(ScenePointsRegistry), previousRegistry);
            SetSingleton(typeof(ProgressionManager), previousProgression);
            foreach (var skill in skills) UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    private static void CheckTactics(Scene scene, StaffController worker)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DirectorUi/ActionConfigPopup.prefab");
        var popup = ((GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponentInChildren<ActionConfigPopupUI>(true);
        popup.OpenForStaff(worker);
        var dropdown = (TMPro.TMP_Dropdown)new UnityEditor.SerializedObject(popup).FindProperty("workstationDropdown").objectReferenceValue;
        var windows = ScenePointsRegistry.Instance.allServicePoints.Where(p => AssignmentManager.CanAssignTo(worker, p)).ToArray();
        Require(windows.Length == 2 && dropdown.options.Count == 3, "Tactics must offer both physical registration windows.");
        var previous = worker.assignedWorkstation;
        var target = windows.Single(w => w != previous);
        var originalLocale = LocalizationSettings.SelectedLocale;
        long sequence = worker.ShiftSequence;
        try
        {
            dropdown.value = Array.IndexOf(windows, target) + 1;
            var actions = worker.activeActions.ToList();
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
            typeof(ActionConfigPopupUI).GetMethod("RefreshLocale", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(popup, new object[] { LocalizationSettings.SelectedLocale });
            typeof(ActionConfigPopupUI).GetMethod("OnSave", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(popup, null);
            Require(worker.assignedWorkstation == target && AssignmentManager.Instance.GetAssignedStaff(previous) == null &&
                worker.ShiftSequence == sequence && worker.IsOnDuty() && actions.SequenceEqual(worker.activeActions),
                "Saving translated tactics lost the selected physical window, actions or current shift.");
        }
        finally { LocalizationSettings.SelectedLocale = originalLocale; }
    }

    private static void SetClock(TimeManager clock, string field, object value) =>
        typeof(TimeManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clock, value);
    private static void SetSingleton(Type type, object value) => type.GetProperty("Instance").SetValue(null, value);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
