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

    // --- НОВОЕ ПОЛЕ ---
    [Header("Специальные объекты зоны")]
    [Tooltip("(Опционально) Укажите стопку, куда будут сбрасываться документы в этой зоне.")]
    public DocumentStack archiveDropOffStack;
    // ------------------

    private List<Waypoint> occupiedWaypoints = new List<Waypoint>();
    public Queue<GameObject> waitingQueue = new Queue<GameObject>();
    private Dictionary<Waypoint, GameObject> occupiedWaypointOwners = new Dictionary<Waypoint, GameObject>();

    public int GetCurrentOccupancy() 
    { 
        return occupiedWaypoints.Count;
    }
    
    public void JoinQueue(GameObject character) 
    { 
        if (!waitingQueue.Contains(character) && !IsInside(character)) 
            waitingQueue.Enqueue(character);
    }
    
    public void JumpQueue(GameObject character) 
    { 
        if (!waitingQueue.Contains(character) && !IsInside(character)) 
        { 
            List<GameObject> tempList = waitingQueue.ToList(); 
            tempList.Insert(0, character);
            waitingQueue = new Queue<GameObject>(tempList); 
        } 
    }
    
    public void LeaveQueue(GameObject character) 
    { 
        if (waitingQueue.Contains(character)) 
        { 
            waitingQueue = new Queue<GameObject>(waitingQueue.Where(p => p != character));
        } 
    }
    
    public bool IsFirstInQueue(GameObject character) 
    { 
        return waitingQueue.Count > 0 && waitingQueue.Peek() == character;
    }
    
    public bool IsWaypointOccupied(Waypoint wp) 
    { 
        return occupiedWaypoints.Contains(wp);
    }

    public List<ClientPathfinding> GetOccupyingClients()
    {
        List<ClientPathfinding> clients = new List<ClientPathfinding>();
        foreach (var reservation in occupiedWaypointOwners)
        {
            GameObject owner = reservation.Value;
            // Capacity includes visitors still approaching; service actions must only see arrivals.
            if (owner != null && owner.activeInHierarchy && reservation.Key != null &&
                Vector2.Distance(owner.transform.position, reservation.Key.transform.position) <= 1f &&
                owner.GetComponent<ClientPathfinding>() is ClientPathfinding client &&
                client.stateMachine != null && client.stateMachine.GetTargetZone() == this &&
                client.stateMachine.GetCurrentState() != ClientState.MovingToGoal)
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
        var freeSpots = insideWaypoints.Where(wp => wp != null && !occupiedWaypoints.Contains(wp)).ToList();
        if (freeSpots.Count > 0)
        {
            Waypoint chosenSpot = freeSpots[Random.Range(0, freeSpots.Count)];
            occupiedWaypoints.Add(chosenSpot);
            occupiedWaypointOwners[chosenSpot] = occupier;
            return chosenSpot;
        }
        
        return null;
    }

    public void ReleaseWaypoint(Waypoint waypointToRelease)
    {
        if (waypointToRelease == null)
        {
            return;
        }
        
        if (occupiedWaypoints.Contains(waypointToRelease))
        {
            occupiedWaypoints.Remove(waypointToRelease);
        }
        if (occupiedWaypointOwners.ContainsKey(waypointToRelease))
        {
            occupiedWaypointOwners.Remove(waypointToRelease);
        }
    }

    public void ReleaseWaypoint(Waypoint waypointToRelease, GameObject owner)
    {
        // A delayed cleanup must not evict a new visitor who already owns the same spot.
        if (waypointToRelease != null && occupiedWaypointOwners.TryGetValue(waypointToRelease, out var currentOwner)
            && currentOwner == owner)
            ReleaseWaypoint(waypointToRelease);
    }

    public Waypoint GetRandomInsideWaypoint() 
    { 
        if (insideWaypoints == null || insideWaypoints.Count == 0) return null;
        return insideWaypoints[Random.Range(0, insideWaypoints.Count)]; 
    }
    
    private bool IsInside(GameObject character) 
    { 
        return occupiedWaypointOwners.ContainsValue(character);
    }
	
    public bool TryOccupyWaypoint(Waypoint wp, GameObject occupier)
    {
        if (wp == null || occupier == null) return false;
        if (occupiedWaypointOwners.TryGetValue(wp, out var owner)) return owner == occupier;
        if (occupiedWaypoints.Contains(wp) || occupiedWaypoints.Count >= capacity) return false;
        occupiedWaypoints.Add(wp);
        occupiedWaypointOwners[wp] = occupier;
        return true;
    }

    public void ManuallyOccupyWaypoint(Waypoint wp, GameObject occupier)
    {
        if (!TryOccupyWaypoint(wp, occupier))
            Debug.LogWarning($"[ClientZoneTrace] {name}: cannot claim {wp?.name} for {occupier?.name}, already reserved or full.");
    }
	
}
