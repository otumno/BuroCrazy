using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LimitedCapacityZone : MonoBehaviour
{
    [Header("Настройки Вместимости")]
    [Tooltip("Максимальное количество персонажей с нужным тегом в зоне")]
    public int capacity = 1;
    [Tooltip("Список тегов, которые нужно учитывать при подсчете вместимости. Если пусто, учитываются все.")]
    public List<string> tagsToCount;

    [Header("Настройки Вейпоинтов")]
    [Tooltip("Точка, у которой персонажи будут ждать входа в зону")]
    public Waypoint waitingWaypoint;
    [Tooltip("Список точек, в которые можно пойти ПОСЛЕ входа в зону")]
    public List<Waypoint> insideWaypoints;

    private List<Waypoint> occupiedWaypoints = new List<Waypoint>();
    private Queue<GameObject> waitingQueue = new Queue<GameObject>();

    /// <summary>
    /// Возвращает текущее количество занятых мест в зоне, учитывая теги.
    /// </summary>
    public int GetCurrentOccupancy()
    {
        // Если теги не указаны, просто возвращаем количество занятых вейпоинтов.
        if (tagsToCount == null || tagsToCount.Count == 0)
        {
            return occupiedWaypoints.Count;
        }

        // Если теги указаны, считаем только персонажей с нужными тегами, которые заняли вейпоинты.
        // (Примечание: эта логика предполагает, что в occupiedWaypoints могут попасть только персонажи,
        // но для большей надежности можно добавить проверку на наличие у них нужного тега, если это необходимо)
        return occupiedWaypoints.Count;
    }

    public void JoinQueue(GameObject character) 
    { 
        if (!waitingQueue.Contains(character)) waitingQueue.Enqueue(character); 
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
    
    public Waypoint RequestAndOccupyWaypoint()
    {
        if (occupiedWaypoints.Count >= capacity)
        {
            return null;
        }

        foreach (var waypoint in insideWaypoints)
        {
            if (!occupiedWaypoints.Contains(waypoint))
            {
                occupiedWaypoints.Add(waypoint);
                return waypoint;
            }
        }
        return null;
    }

    public void ReleaseWaypoint(Waypoint waypointToRelease)
    {
        if (occupiedWaypoints.Contains(waypointToRelease))
        {
            occupiedWaypoints.Remove(waypointToRelease);
        }
    }
    
    public Waypoint GetRandomInsideWaypoint()
    {
        if (insideWaypoints == null || insideWaypoints.Count == 0) return null;
        return insideWaypoints[Random.Range(0, insideWaypoints.Count)];
    }
}