// Файл: Waypoint.cs
using UnityEngine;
using System.Collections.Generic;

public class Waypoint : MonoBehaviour
{
    public enum WaypointType { General, StaffOnly }

    [Tooltip("Тип точки: General - для всех, StaffOnly - только для персонала")]
    public WaypointType type = WaypointType.General;
    
    [Tooltip("Отметьте, если эта точка является конкретным местом обслуживания (у стойки клерка)")]
    public bool isServicePoint = false;

    // --- НОВОЕ ПОЛЕ ---
    [Tooltip("Понятное для игрока название этой точки (например, 'Окно 1', 'Касса')")]
    public string friendlyName;
    // ------------------

    [Tooltip("Список тегов, которым ЗАПРЕЩЕНО использовать этот вейпоинт")]
    public List<string> forbiddenTags;

    public List<Waypoint> neighbors;
}