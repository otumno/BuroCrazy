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
            for (int i = current; i < periods.Count && count > 0; i++, count--)
            {
                if (periods[i].PeriodType.IsNight()) break;
                mask |= periods[i].PeriodType;
            }
            return mask;
        }

        // A temporary contract cannot sell the same period twice. Past work and a period already in progress stay paid.
        public static CalendarDayPeriodType MoveTemporaryShift(IReadOnlyList<PeriodSettings> periods, CalendarDayPeriodType mask,
            int current, int start, bool workingNow)
        {
            if (periods == null || current < 0 || start < current || start >= periods.Count || periods[start].PeriodType.IsNight())
                return mask;
            var committed = CalendarDayPeriodType.None;
            int firstMovable = current + (workingNow && (mask & periods[current].PeriodType) != 0 ? 1 : 0);
            for (int i = 0; i < firstMovable; i++) committed |= mask & periods[i].PeriodType;
            int remaining = CountPeriods(mask & ~committed);
            var moved = BuildTemporaryShift(periods, Mathf.Max(firstMovable, start), remaining);
            return CountPeriods(moved) == remaining ? committed | moved : mask;
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
