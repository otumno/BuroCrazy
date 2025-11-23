using System.Collections.Generic;

namespace Data.Calendar
{
    [System.Serializable]
    public class SpecialDaySettings
    {
        public SpecialEventType specialEventType = SpecialEventType.None;
        public List<PeriodSettings> periodSettings = new List<PeriodSettings>();
    }
}