using UnityEngine;
using System.Collections.Generic;

public class LimitedCapacityZone : MonoBehaviour
{
    [Header("Настройки Вместимости")]
    public int capacity = 1;
    public Waypoint waitingWaypoint;
    public Waypoint insideWaypoint;

    private List<ClientPathfinding> clientsInside = new List<ClientPathfinding>();

    public bool CanEnter() { return clientsInside.Count < capacity; }
    public void OnClientEntered(ClientPathfinding client) { if (!clientsInside.Contains(client)) clientsInside.Add(client); }
    public void OnClientExited(ClientPathfinding client) { if (clientsInside.Contains(client)) clientsInside.Remove(client); }
}