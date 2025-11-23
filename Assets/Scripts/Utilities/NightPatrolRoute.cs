using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Этот компонент-помощник автоматически находит все путевые точки,
// кроме выхода, для использования в ночном патруле.
namespace Utilities
{
    public class NightPatrolRoute : MonoBehaviour
    {
        public static List<Waypoint> GetNightRoute()
        {
            return FindObjectsByType<Waypoint>(FindObjectsSortMode.None)
                .Where(wp => wp.name != "ExitWaypoint") // Исключаем точку выхода по имени
                .ToList();
        }
    }
}