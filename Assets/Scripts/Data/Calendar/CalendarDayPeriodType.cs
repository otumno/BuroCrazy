using System;

namespace Data.Calendar
{
    [Flags]
    public enum CalendarDayPeriodType
    {
        None        = 0,
        Morning     = 1 << 0,
        EarlyDay    = 1 << 1,
        Noon        = 1 << 2,
        Day         = 1 << 3,
        LateDay     = 1 << 4,
        Evening     = 1 << 5,
        Night       = 1 << 6,
        
        FullDay     = Morning | EarlyDay | Noon | Day | LateDay | Evening
    }
}