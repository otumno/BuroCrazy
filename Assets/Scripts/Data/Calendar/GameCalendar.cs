using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GameCalendar", menuName = "Bureau/Game Calendar")]
public class GameCalendar : ScriptableObject
{
    [Tooltip("Список настроек для каждого периода. Каждый параметр внутри можно настроить по дням с помощью кривой.")]
    public List<PeriodSettings> periodSettings = new List<PeriodSettings>();

    // Список дней окончательно удален для перехода на новую систему.
}