using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ClientQueueManager : MonoBehaviour
{
    public static ClientQueueManager Instance { get; private set; }

    public WaitingZone mainWaitingZone;

    public float patienceMinTime = 8f;
    public float patienceMaxTime = 15f;
    public AudioClip nextClientSound;

    private Dictionary<Transform, ClientPathfinding> occupiedSeats = new Dictionary<Transform, ClientPathfinding>();
    private List<ClientPathfinding> standingClients = new List<ClientPathfinding>();
    private Dictionary<ClientPathfinding, int> queue = new Dictionary<ClientPathfinding, int>();
    private int nextQueueNumber = 1;
    
    public List<ClientPathfinding> dissatisfiedClients = new List<ClientPathfinding>();
    public List<int> currentlyCalledNumbers = new List<int>();
    
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }
    
    public void ResetQueueNumber() 
    { 
        nextQueueNumber = 1; 
        currentlyCalledNumbers.Clear(); 
    }
    
    public Transform FindSeatForClient(ClientPathfinding client) 
    {
        if (mainWaitingZone == null || mainWaitingZone.seatPoints.Count == 0) return null;
        Transform freeSeat = mainWaitingZone.seatPoints.FirstOrDefault(s => s != null && !occupiedSeats.ContainsKey(s));
        if (freeSeat != null) 
        { 
            occupiedSeats[freeSeat] = client; 
            if(standingClients.Contains(client)) standingClients.Remove(client); 
            return freeSeat; 
        } 
        else 
        { 
            if (!standingClients.Contains(client)) standingClients.Add(client); 
            return null; 
        }
    }

    public void OnClientLeavesWaitingZone(ClientPathfinding client) 
    {
        if (standingClients.Contains(client)) standingClients.Remove(client);
        if (occupiedSeats.ContainsValue(client)) 
        {
            Transform seatToFree = occupiedSeats.FirstOrDefault(kvp => kvp.Value == client).Key;
            if (seatToFree != null) 
            { 
                occupiedSeats.Remove(seatToFree); 
                FindAndAssignNearestStandingClient(seatToFree); 
            }
        }
    }

    private void FindAndAssignNearestStandingClient(Transform freeSeat) 
    {
        if (occupiedSeats.ContainsKey(freeSeat)) return;
        if (standingClients.Count == 0) return;
        ClientPathfinding closestClient = standingClients.OrderBy(c => Vector2.Distance(c.transform.position, freeSeat.position)).FirstOrDefault();
        if (closestClient != null) 
        { 
            standingClients.Remove(closestClient); 
            occupiedSeats[freeSeat] = closestClient; 
            closestClient.stateMachine.GoToSeat(freeSeat); 
        }
    }
    
    public bool CallNextClient(ClerkController clerk) 
    { 
        if (queue.Count == 0) return false;
        LimitedCapacityZone registrationZone = ClientSpawner.GetRegistrationZone();
        if (registrationZone.IsWaypointOccupied(clerk.assignedServicePoint.clientStandPoint))
        {
            return false;
        }

        var nextInQueue = queue.Where(c => c.Key != null && c.Key.stateMachine.GetCurrentState() != ClientState.MovingToGoal && (c.Key.stateMachine.GetCurrentState() == ClientState.AtWaitingArea || c.Key.stateMachine.GetCurrentState() == ClientState.SittingInWaitingArea)).OrderBy(kvp => kvp.Value).FirstOrDefault();
        ClientPathfinding nextClient = nextInQueue.Key; 

        if (nextClient != null) 
        { 
            if (nextClientSound != null)
            {
                AudioSource.PlayClipAtPoint(nextClientSound, clerk.transform.position);
            }
            else if(nextClientSound == null)
            {
                Debug.LogWarning("Звук вызова следующего клиента (Next Client Sound) не назначен в ClientQueueManager!");
            }

            int calledNumber = nextInQueue.Value;
            currentlyCalledNumbers.Add(calledNumber);
             
            queue.Remove(nextClient);
            Debug.Log($"Клерк {clerk.name} вызывает клиента #{calledNumber} ({nextClient.name}) к стойке {clerk.assignedServicePoint.name}");

            nextClient.stateMachine.GetCalledToSpecificDesk(clerk.assignedServicePoint.clientStandPoint, calledNumber, clerk);
            return true;
        } 
        return false;
    }
    
    public void ServiceFinishedForNumber(int number)
    {
        if (currentlyCalledNumbers.Contains(number))
        {
            currentlyCalledNumbers.Remove(number);
        }
    }
    
    public void RemoveClientFromQueue(ClientPathfinding c) 
    {
        if (c != null && queue.ContainsKey(c)) 
        {
            ServiceFinishedForNumber(queue[c]);
            if (c.notification != null) c.notification.SetQueueNumber(-1); 
            OnClientLeavesWaitingZone(c); 
            queue.Remove(c);
        }
    }

    public ClientPathfinding GetRandomClientFromQueue() 
    { 
        if (queue.Count == 0) return null;
        return queue.Keys.ElementAt(Random.Range(0, queue.Count)); 
    }

    public Waypoint GetToiletReturnGoal(ClientPathfinding client) 
    { 
        if (queue.ContainsKey(client)) return mainWaitingZone.GetRandomStandingPoint().GetComponent<Waypoint>(); 
        return ClientSpawner.Instance.exitWaypoint; 
    }

    public Waypoint ChooseNewGoal(ClientPathfinding client) 
    { 
        return mainWaitingZone.GetRandomStandingPoint().GetComponent<Waypoint>(); 
    }

    public void JoinQueue(ClientPathfinding c) 
    { 
        if (!queue.ContainsKey(c)) 
        { 
            queue.Add(c, nextQueueNumber++); 
            if (c.notification != null) c.notification.SetQueueNumber(queue[c]); 
            StartPatienceTimer(c); 
        } 
    }
    
    public void AddAngryClient(ClientPathfinding client)
    {
        if (!dissatisfiedClients.Contains(client))
        {
            dissatisfiedClients.Add(client);
        }
    }

    public void StartPatienceTimer(ClientPathfinding client) 
    { 
        StartCoroutine(PatienceCheck(client)); 
    }
    
    private IEnumerator PatienceCheck(ClientPathfinding client) 
    { 
        float minWait = patienceMinTime * (1f + client.babushkaFactor) * (1f - client.suetunFactor * 0.5f);
        float maxWait = patienceMaxTime * (1f + client.babushkaFactor) * (1f - client.suetunFactor * 0.5f);
        yield return new WaitForSeconds(Random.Range(minWait, maxWait));
        if (client == null || (client.stateMachine.GetCurrentState() != ClientState.AtWaitingArea && client.stateMachine.GetCurrentState() != ClientState.SittingInWaitingArea)) yield break; 
        
        float confusedChance = 0.65f;
        float choice = Random.value;
        if (choice < 0.15f) 
        { 
            client.stateMachine.DecideToVisitToilet(); 
        } 
        else if (choice < confusedChance) 
        { 
            RemoveClientFromQueue(client); 
            client.stateMachine.SetState(ClientState.Confused); 
        } 
        else 
        { 
            StartPatienceTimer(client); 
        } 
    }
    
    public bool IsWaypointInWaitingZone(Waypoint wp)
    {
        if (mainWaitingZone == null || wp == null) return false;
        bool inStandingPoints = mainWaitingZone.standingPoints.Any(p => p != null && p.GetComponent<Waypoint>() == wp);
        bool inSeatPoints = mainWaitingZone.seatPoints.Any(p => p != null && p.GetComponent<Waypoint>() == wp);
        return inStandingPoints || inSeatPoints;
    }
}