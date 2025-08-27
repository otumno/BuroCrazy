using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LimitedCapacityZone : MonoBehaviour
{
    [Header("Настройки Вместимости")]
    public int capacity = 1;

    // public bool zoneProvidesService = true; // --- УДАЛЕНО ---

    public List<string> tagsToCount;

    [Header("Настройки Вейпоинтов")]
    public Waypoint waitingWaypoint;
    public List<Waypoint> insideWaypoints;

    private List<Waypoint> occupiedWaypoints = new List<Waypoint>();
    private Queue<GameObject> waitingQueue = new Queue<GameObject>();

    public int GetCurrentOccupancy() { return occupiedWaypoints.Count; }
    public void JoinQueue(GameObject character) { if (!waitingQueue.Contains(character) && !IsInside(character)) waitingQueue.Enqueue(character); }
    public void JumpQueue(GameObject character) { if (!waitingQueue.Contains(character) && !IsInside(character)) { List<GameObject> tempList = waitingQueue.ToList(); tempList.Insert(0, character); waitingQueue = new Queue<GameObject>(tempList); } }
    public void LeaveQueue(GameObject character) { if (waitingQueue.Contains(character)) { waitingQueue = new Queue<GameObject>(waitingQueue.Where(p => p != character)); } }
    public bool IsFirstInQueue(GameObject character) { return waitingQueue.Count > 0 && waitingQueue.Peek() == character; }
    
    // --- УПРОЩЕННАЯ ВЕРСИЯ МЕТОДА ---
    public Waypoint RequestAndOccupyWaypoint()
    {
        if (occupiedWaypoints.Count >= capacity) return null;

        // Просто ищем первую свободную точку в списке, игнорируя isServicePoint
        Waypoint freeSpot = insideWaypoints.FirstOrDefault(wp => wp != null && !occupiedWaypoints.Contains(wp));
        
        if (freeSpot != null)
        {
            occupiedWaypoints.Add(freeSpot);
            return freeSpot;
        }

        return null;
    }

    public void ReleaseWaypoint(Waypoint waypointToRelease) { if (occupiedWaypoints.Contains(waypointToRelease)) occupiedWaypoints.Remove(waypointToRelease); }
    public Waypoint GetRandomInsideWaypoint() { if (insideWaypoints == null || insideWaypoints.Count == 0) return null; return insideWaypoints[Random.Range(0, insideWaypoints.Count)]; }
    private bool IsInside(GameObject character) { return false; }
}