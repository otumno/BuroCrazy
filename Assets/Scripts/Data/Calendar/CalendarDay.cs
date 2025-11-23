using System.Collections.Generic;
using UnityEngine;

namespace Data.Calendar
{
    [CreateAssetMenu(fileName = "CalendarDay", menuName = "Bureau/Calendar Day")]
    public class CalendarDay : ScriptableObject
    {
        [Tooltip("Список настроек для каждого периода. Каждый параметр внутри можно настроить по дням с помощью кривой.")]
        public List<PeriodSettings> periodSettings = new();
    }
}