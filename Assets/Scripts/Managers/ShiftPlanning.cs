using Data.Calendar;

namespace Managers
{
    public static class ShiftPlanning
    {
        public static CalendarDayPeriodType GetUpdatedMask(StaffController staff, int start)
        {
            var clock = TimeManager.Instance;
            var periods = clock != null ? clock.mainCalendarDay?.periodSettings : null;
            if (staff == null) return CalendarDayPeriodType.None;
            if (periods == null || start < 0 || start >= periods.Count) return staff.WorkShiftMask;
            if (!staff.IsTemporary) return StaffScheduleMath.BuildShift(periods, start, HiringManager.GetShiftPeriodsCount(staff.currentRank));

            if (staff.temporaryWorkDay >= 0 && staff.temporaryWorkDay != clock.GetCurrentDay()) return staff.WorkShiftMask;
            int current = periods.FindIndex(p => p.PeriodType == clock.GetCurrentPeriodType());
            return StaffScheduleMath.MoveTemporaryShift(periods, staff.WorkShiftMask, current, start,
                staff.gameObject.activeSelf && staff.IsOnDuty());
        }

        public static bool Apply(StaffController staff, int start)
        {
            if (staff == null) return false;
            var updated = GetUpdatedMask(staff, start);
            if (updated == staff.WorkShiftMask) return false;
            var current = TimeManager.Instance.GetCurrentPeriodType();
            bool wasScheduled = (staff.WorkShiftMask & current) != 0;
            staff.WorkShiftMask = updated;
            if ((updated & current) != 0)
            {
                if (!wasScheduled) PayStaffManager.AccrueCurrentPeriod(staff);
                if (staff.gameObject.activeSelf && staff.hasLeftToday) staff.ResumeFromWalkHome();
                else HiringManager.Instance?.CheckAllStaffShiftsImmediately();
            }
            else if (wasScheduled && staff.gameObject.activeSelf && staff.IsOnDuty())
            {
                staff.EndShift();
            }
            return true;
        }
    }
}
