using System;
using System.Collections.Generic;

namespace Data.Calendar
{
    [Serializable]
    public class SpecialDaySettings
    {
        public float Weight;
        public SpecialEventType EventType = SpecialEventType.None;
        public List<PeriodSettings> Settings = new List<PeriodSettings>();
    }
}