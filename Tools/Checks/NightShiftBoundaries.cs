using System;
using System.Linq;
using System.Reflection;
using Data.Calendar;
using Managers;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NightShiftBoundariesCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var oldClock = TimeManager.Instance;
        var oldCalendar = CalendarManager.Instance;
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
        Action_EndShift endShift = null;
        try
        {
            var roots = scene.GetRootGameObjects();
            var clock = roots.SelectMany(r => r.GetComponentsInChildren<TimeManager>(true)).Single();
            var hiring = roots.SelectMany(r => r.GetComponentsInChildren<HiringManager>(true)).Single();
            SetSingleton(typeof(TimeManager), clock);
            SetSingleton(typeof(CalendarManager), null);
            var go = new GameObject("NightShiftFixture");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var staff = go.AddComponent<StaffController>();
            var periods = clock.mainCalendarDay.periodSettings;
            int last = periods.Count - 1;
            staff.WorkShiftMask = StaffScheduleMath.BuildShift(periods, last, 3);
            Require(StaffScheduleMath.CountPeriods(staff.WorkShiftMask) == 3 &&
                (staff.WorkShiftMask & periods[last].PeriodType) != 0 && (staff.WorkShiftMask & periods[0].PeriodType) != 0 &&
                (staff.WorkShiftMask & periods[1].PeriodType) != 0, "Cross-midnight shift was truncated.");
            staff.hasArrivedToday = true;
            staff.hasLeftToday = false;
            staff.HasStartedAnyShift = true;
            staff.currentLateness = 0;
            typeof(StaffController).GetField("<ShiftSequence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(staff, 17L);
            hiring.AllStaff.Clear();
            hiring.AllStaff.Add(staff);
            typeof(HiringManager).GetMethod("TimeManager_DayChanged", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hiring, new object[] { 8 });
            Require(staff.hasArrivedToday && !staff.hasLeftToday && staff.ShiftSequence == 17,
                "Day change reset an active night worker.");
            endShift = ScriptableObject.CreateInstance<Action_EndShift>();
            foreach (int i in new[] { last, 0, 1 })
            {
                Set(clock, "currentPeriodType", periods[i].PeriodType);
                Require(!endShift.AreConditionsMet(staff), "Home action enabled inside continuous night shift.");
            }
            Set(clock, "currentPeriodType", periods[2].PeriodType);
            Require(endShift.AreConditionsMet(staff), "Home action unavailable after last assigned period.");
            float expected = periods[0].durationInSeconds - 1f + periods[1].durationInSeconds + periods[last].durationInSeconds;
            Require(Mathf.Abs(StaffScheduleMath.RemainingToday(periods, staff.WorkShiftMask, periods[0].PeriodType, 1) - expected) < .001f,
                "HUD remaining-today total is wrong for wrapped shift.");
            int evening = periods.FindIndex(p => p.PeriodType == CalendarDayPeriodType.Evening);
            Require(StaffScheduleMath.BuildTemporaryShift(periods, evening, 3) == CalendarDayPeriodType.Evening,
                "Temporary hire leaked into night or tomorrow.");
            staff.hireDay = 7;
            staff.hirePeriod = periods[evening].PeriodType;
            Set(clock, "dayCounter", 7);
            Set(clock, "currentPeriodType", periods[evening].PeriodType);
            Require(staff.HasEmploymentStarted(), "Permanent hire delayed in the hiring period.");
            Set(clock, "currentPeriodType", periods[evening + 1].PeriodType);
            Require(staff.HasEmploymentStarted(), "Permanent hire cannot start next night period.");
            var late = periods.FindIndex(p => p.PeriodType == CalendarDayPeriodType.LateDay);
            Set(clock, "currentPeriodType", periods[late].PeriodType);
            var candidate = new Utilities.Candidate { HiringCost = 100, Rank = null };
            int cost = hiring.GetHiringCost(candidate, Enums.EmploymentType.Temporary, out int paidPeriods);
            Require(paidPeriods == 2 && cost == Mathf.Max(0, Mathf.RoundToInt(100 * hiring.temporaryHiringCostMultiplier))
                + 2 * hiring.GetSalaryPerPeriod(null), "Short shift must include the hiring period.");
            CheckSchedule(staff, periods, scene);
            staff.WorkShiftMask = periods[late].PeriodType;
            var saved = new StaffSaveData();
            hiring.CaptureAttendance(staff, ref saved, 7);
            var restored = JsonUtility.FromJson<StaffSaveData>(JsonUtility.ToJson(saved));
            Require(restored.activeShiftSequence == 17 && restored.hasAttendanceState, "Shift order not saved.");
            var mark = typeof(StaffController).GetMethod("MarkShiftStarted", BindingFlags.Instance | BindingFlags.NonPublic);
            var seq = typeof(StaffController).GetField("nextShiftSequence", BindingFlags.Static | BindingFlags.NonPublic);
            var originalSequence = seq.GetValue(null);
            try
            {
                hiring.RestoreAttendance(staff, restored, 7);
                mark.Invoke(staff, null);
                Require(staff.ShiftSequence == 17, "Restored shift changed its position.");
                mark.Invoke(staff, null);
                Require(staff.ShiftSequence > 17, "Next arrival reused an older position.");
                Require(JsonUtility.FromJson<StaffSaveData>("{}").activeShiftSequence == 0, "Legacy sequence default.");
            }
            finally { seq.SetValue(null, originalSequence); }
            go.SetActive(false);
            staff.currentLateness = -1;
            typeof(HiringManager).GetMethod("TimeManager_DayChanged", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hiring, new object[] { 8 });
            Require(!staff.hasArrivedToday && staff.currentLateness == 0, "Inactive sick worker never gets a new day's arrival.");
            return "PASS: actual calendar wrap/night periods, active shift identity across day change, home timing, HUD sum, immediate hiring, actual short-shift charge, immediate hatching, live schedule editing and shift order after save/load.";
        }
        finally
        {
            SetSingleton(typeof(TimeManager), oldClock);
            SetSingleton(typeof(CalendarManager), oldCalendar);
            if (endShift != null) UnityEngine.Object.DestroyImmediate(endShift);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static void CheckSchedule(StaffController staff, System.Collections.Generic.List<Data.Calendar.PeriodSettings> periods,
        UnityEngine.SceneManagement.Scene scene)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI_ScheduleRow.prefab");
        var root = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
        var row = root.GetComponent<StaffScheduleRowUI>();
        staff.WorkShiftMask = CalendarDayPeriodType.Morning;
        row.Setup(staff, periods, true);
        var cells = row.GetComponentsInChildren<ScheduleCellUI>(true);
        Require(cells.Length == periods.Count, "Missing schedule period.");
        foreach (var mask in new[] { CalendarDayPeriodType.Morning, CalendarDayPeriodType.StartNight | CalendarDayPeriodType.EndNight })
        {
            staff.WorkShiftMask = mask;
            for (int i = 0; i < cells.Length; i++)
            {
                if (mask != CalendarDayPeriodType.Morning)
                    typeof(ScheduleCellUI).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cells[i], null);
                var hatch = cells[i].GetComponentInChildren<UI.SchedulePendingHatch>(true);
                var fill = hatch.transform.parent.GetComponent<UnityEngine.UI.Image>();
                bool assigned = (periods[i].PeriodType & mask) != 0;
                Require(hatch.enabled == assigned && fill.enabled == assigned && !hatch.raycastTarget, "Wrong assignment at " + periods[i].PeriodType);
                Require(cells[i].GetComponentInChildren<UnityEngine.UI.Button>(true).interactable, "Daytime cell is locked.");
            }
        }
        var day = periods.FindIndex(p => p.PeriodType == CalendarDayPeriodType.Day);
        Require(ShiftPlanning.Apply(staff, day) && (staff.WorkShiftMask & CalendarDayPeriodType.Day) != 0, "Live edit did not update the mask.");
        var morning = periods.FindIndex(p => p.PeriodType == CalendarDayPeriodType.Morning);
        var original = StaffScheduleMath.BuildTemporaryShift(periods, morning, 3);
        var moved = StaffScheduleMath.MoveTemporaryShift(periods, original, morning, day, true);
        Require(moved != original && (moved & CalendarDayPeriodType.Morning) != 0 && StaffScheduleMath.CountPeriods(moved) == 3 &&
            (moved & CalendarDayPeriodTypeExtensions.FullNight) == 0, "Temporary edit lost worked time or changed paid duration.");
        Require(StaffScheduleMath.MoveTemporaryShift(periods, moved, morning, periods.Count - 1, true) == moved, "Temporary hours moved into night.");
    }

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetSingleton(Type type, object value) => type.GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { value });
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
