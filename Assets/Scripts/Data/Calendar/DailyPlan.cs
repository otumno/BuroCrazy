using System.Collections.Generic;

public enum DayEvent { None, PensionDay, ClownDay /*, и т.д. */ }

[System.Serializable]
public class DailyPlan
{
    public DayEvent eventOfTheDay = DayEvent.None;
    public List<PeriodSettings> periodSettings = new List<PeriodSettings>();
}