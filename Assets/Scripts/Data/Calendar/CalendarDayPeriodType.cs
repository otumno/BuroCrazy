using System;

namespace Data.Calendar
{
    [Flags]
    public enum CalendarDayPeriodType : int
    {
        None        = 0,
        Morning     = 1 << 0,
        EarlyDay    = 1 << 1,
        Noon        = 1 << 2,
        Day         = 1 << 3,
        LateDay     = 1 << 4,
        Evening     = 1 << 5,
        StartNight  = 1 << 6,
        EndNight    = 1 << 7,
        
        // --- ДОБАВЛЕНО: Маска полного рабочего дня ---
        FullDay     = Morning | EarlyDay | Noon | Day | LateDay | Evening
    }

    public static class CalendarDayPeriodTypeExtensions
    {
        public static CalendarDayPeriodType AllDay => CalendarDayPeriodType.FullDay;

        public static CalendarDayPeriodType AllNight => CalendarDayPeriodType.StartNight |
                                                        CalendarDayPeriodType.EndNight;

        public static CalendarDayPeriodType AllCalendarDay => AllDay | AllNight; 

        public static bool IsNight(this CalendarDayPeriodType type) =>
            (type & AllNight) != 0; // Проверка битовой маски
    }
}