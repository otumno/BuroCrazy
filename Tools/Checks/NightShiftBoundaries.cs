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
            Require(StaffScheduleMath.BuildTemporaryShift(periods, evening, 3) == CalendarDayPeriodType.None,
                "Temporary hire leaked into night or tomorrow.");
            staff.hireDay = 7;
            staff.hirePeriod = periods[evening].PeriodType;
            Set(clock, "dayCounter", 7);
            Set(clock, "currentPeriodType", periods[evening].PeriodType);
            Require(!staff.HasEmploymentStarted(), "Permanent hire started in the hiring period.");
            Set(clock, "currentPeriodType", periods[evening + 1].PeriodType);
            Require(staff.HasEmploymentStarted(), "Permanent hire cannot start next night period.");
            go.SetActive(false);
            staff.currentLateness = -1;
            typeof(HiringManager).GetMethod("TimeManager_DayChanged", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hiring, new object[] { 8 });
            Require(!staff.hasArrivedToday && staff.currentLateness == 0, "Inactive sick worker never gets a new day's arrival.");
            return "PASS: actual calendar wrap/night periods, active shift identity across day change, home timing, HUD sum, temporary night exclusion and next-period permanent hire.";
        }
        finally
        {
            SetSingleton(typeof(TimeManager), oldClock);
            SetSingleton(typeof(CalendarManager), oldCalendar);
            if (endShift != null) UnityEngine.Object.DestroyImmediate(endShift);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetSingleton(Type type, object value) => type.GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { value });
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
