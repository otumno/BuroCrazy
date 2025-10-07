using System.Collections.Generic;

// Enum для "особых дней"
public enum DayEvent 
{ 
    None, 
    PensionDay, 
    ClownDay 
}

// Класс для хранения плана на один день
[System.Serializable]
public class DailyPlan
{
    public DayEvent eventOfTheDay = DayEvent.None;
    public List<PeriodSettings> periodSettings = new List<PeriodSettings>();
}