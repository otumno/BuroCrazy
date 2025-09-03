// Файл: LimitedCapacityZone.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LimitedCapacityZone : MonoBehaviour
{
    [Header("Настройки Вместимости")]
    public int capacity = 1;
    public List<string> tagsToCount;
    [Header("Настройки Вейпоинтов")]
    [Tooltip("Точка, у которой выстраивается очередь. Является точкой входа по умолчанию.")]
    public Waypoint waitingWaypoint;
    [Tooltip("(Опционально) Укажите отдельный вейпоинт для выхода из зоны, чтобы разделить потоки.")]
    public Waypoint exitWaypoint;
    [Space]
    public List<Waypoint> insideWaypoints;

    private List<Waypoint> occupiedWaypoints = new List<Waypoint>();
    private Queue<GameObject> waitingQueue = new Queue<GameObject>();
    private Dictionary<Waypoint, GameObject> occupiedWaypointOwners = new Dictionary<Waypoint, GameObject>();

    public int GetCurrentOccupancy() { return occupiedWaypoints.Count; }
    public void JoinQueue(GameObject character) { if (!waitingQueue.Contains(character) && !IsInside(character)) waitingQueue.Enqueue(character); }
    public void JumpQueue(GameObject character) { if (!waitingQueue.Contains(character) && !IsInside(character)) { List<GameObject> tempList = waitingQueue.ToList(); tempList.Insert(0, character); waitingQueue = new Queue<GameObject>(tempList); } }
    public void LeaveQueue(GameObject character) { if (waitingQueue.Contains(character)) { waitingQueue = new Queue<GameObject>(waitingQueue.Where(p => p != character)); } }
    public bool IsFirstInQueue(GameObject character) { return waitingQueue.Count > 0 && waitingQueue.Peek() == character; }
    public bool IsWaypointOccupied(Waypoint wp) { return occupiedWaypoints.Contains(wp); }

    public List<ClientPathfinding> GetOccupyingClients()
    {
        List<ClientPathfinding> clients = new List<ClientPathfinding>();
        foreach (GameObject owner in occupiedWaypointOwners.Values)
        {
            if (owner != null && owner.GetComponent<ClientPathfinding>() is ClientPathfinding client)
            {
                clients.Add(client);
            }
        }
        return clients;
    }

    public void OccupyWaypoint(Waypoint wp)
    {
        if (!occupiedWaypoints.Contains(wp))
        {
            occupiedWaypoints.Add(wp);
        }
    }

    public Waypoint RequestAndOccupyWaypoint(GameObject occupier)
    {
        if (occupiedWaypoints.Count >= capacity) return null;
        Waypoint freeSpot = insideWaypoints.FirstOrDefault(wp => wp != null && !occupiedWaypoints.Contains(wp));
        if (freeSpot != null)
        {
            occupiedWaypoints.Add(freeSpot);
            occupiedWaypointOwners[freeSpot] = occupier;
            return freeSpot;
        }
        return null;
    }

    public void ReleaseWaypoint(Waypoint waypointToRelease)
    {
        if (occupiedWaypoints.Contains(waypointToRelease))
        {
            occupiedWaypoints.Remove(waypointToRelease);
        }
        if (occupiedWaypointOwners.ContainsKey(waypointToRelease))
        {
            occupiedWaypointOwners.Remove(waypointToRelease);
        }
    }

    public Waypoint GetRandomInsideWaypoint() { if (insideWaypoints == null || insideWaypoints.Count == 0) return null; return insideWaypoints[Random.Range(0, insideWaypoints.Count)]; }
    private bool IsInside(GameObject character) { return false; }
}