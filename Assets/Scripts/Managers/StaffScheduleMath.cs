using System.Collections.Generic;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public static class StaffScheduleMath
    {
        public static CalendarDayPeriodType BuildShift(IReadOnlyList<PeriodSettings> periods, int start, int count)
        {
            var mask = CalendarDayPeriodType.None;
            if (periods == null || periods.Count == 0 || start < 0 || start >= periods.Count) return mask;
            for (int i = 0; i < Mathf.Min(count, periods.Count); i++)
                mask |= periods[(start + i) % periods.Count].PeriodType;
            return mask;
        }

        // Temporary hires never wrap into tomorrow and never work at night.
        public static CalendarDayPeriodType BuildTemporaryShift(IReadOnlyList<PeriodSettings> periods, int current, int count)
        {
            var mask = CalendarDayPeriodType.None;
            if (periods == null || current < 0 || current >= periods.Count) return mask;
            for (int i = current + 1; i < periods.Count && count > 0; i++, count--)
            {
                if (periods[i].PeriodType.IsNight()) break;
                mask |= periods[i].PeriodType;
            }
            return mask;
        }

        public static int CountPeriods(CalendarDayPeriodType mask)
        {
            int count = 0;
            uint bits = (uint)(mask & CalendarDayPeriodTypeExtensions.Hours24);
            while (bits != 0) { bits &= bits - 1; count++; }
            return count;
        }

        public static float RemainingToday(IReadOnlyList<PeriodSettings> periods, CalendarDayPeriodType mask,
            CalendarDayPeriodType current, float elapsed)
        {
            if (periods == null) return 0;
            float remaining = 0;
            bool reachedCurrent = false;
            foreach (var period in periods)
            {
                if (period.PeriodType == current) reachedCurrent = true;
                if (!reachedCurrent || (mask & period.PeriodType) == 0) continue;
                remaining += Mathf.Max(0, period.durationInSeconds - (period.PeriodType == current ? elapsed : 0));
            }
            return remaining;
        }
    }
}
