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
    }

    public static class CalendarDayPeriodTypeExtensions
    {
        public static CalendarDayPeriodType FullDay => CalendarDayPeriodType.Morning |
                                                       CalendarDayPeriodType.EarlyDay |
                                                       CalendarDayPeriodType.Noon |
                                                       CalendarDayPeriodType.Day |
                                                       CalendarDayPeriodType.LateDay |
                                                       CalendarDayPeriodType.Evening;

        public static CalendarDayPeriodType FullNight => CalendarDayPeriodType.StartNight |
                                                         CalendarDayPeriodType.EndNight;

        public static CalendarDayPeriodType Hours24 => FullDay | FullNight;

        public static bool IsNight(this CalendarDayPeriodType type) =>
            (type & FullNight) != 0; // Проверка битовой маски

        public static string GetLocalization(this CalendarDayPeriodType type)
        {
            switch (type)
            {
                default:
                case CalendarDayPeriodType.None:
                {
                    return "Out of range";
                }
                case CalendarDayPeriodType.Morning:
                case CalendarDayPeriodType.EarlyDay:
                case CalendarDayPeriodType.Noon:
                {
                    return "УТРО";
                }
                case CalendarDayPeriodType.Day:
                case CalendarDayPeriodType.LateDay:
                {
                    return "ДЕНЬ";
                }
                case CalendarDayPeriodType.Evening:
                {
                    return "ВЕЧЕР";
                }
                case CalendarDayPeriodType.StartNight:
                case CalendarDayPeriodType.EndNight:
                {
                    return "НОЧЬ";
                }
            }
        }
    }
}