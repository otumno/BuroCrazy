using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ClientQueueManager : MonoBehaviour
{
    public static ClientQueueManager Instance { get; private set; }

    [Header("Главная зона ожидания")]
    [Tooltip("Перетащите сюда объект со скриптом WaitingZone")]
    public WaitingZone mainWaitingZone;

    [Header("Настройки")]
    public float patienceMinTime = 8f;
    public float patienceMaxTime = 15f;
    
    // --- СПИСКИ УПРАВЛЕНИЯ ---
    private Dictionary<Transform, ClientPathfinding> occupiedSeats = new Dictionary<Transform, ClientPathfinding>();
    private List<ClientPathfinding> standingClients = new List<ClientPathfinding>();
    private Dictionary<ClientPathfinding, int> queue = new Dictionary<ClientPathfinding, int>();
    private int nextQueueNumber = 1;
    
    // --- СОСТОЯНИЕ РЕГИСТРАТУРЫ ---
    private bool isRegistrationBusy = false;
    private ClientPathfinding clientBeingServed;
    private float noShowTimer = 0f;
    private float noShowTimeout = 20f;
    
    public List<ClientPathfinding> dissatisfiedClients = new List<ClientPathfinding>();
    public int currentlyCalledNumber = -1;
    
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }
    
    void Start()
    {
        // Логика поиска стульев по тегам больше не нужна
    }
    
    void Update() 
    {
        if(!isRegistrationBusy)
        {
            CallNextClient();
        }
        CheckForStalledRegistrar();
    }
    
    public void ResetQueueNumber() { nextQueueNumber = 1; }
    
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
        if (standingClients.Count == 0) return;
        ClientPathfinding closestClient = standingClients.OrderBy(c => Vector2.Distance(c.transform.position, freeSeat.position)).FirstOrDefault();
        if (closestClient != null) 
        {
            standingClients.Remove(closestClient);
            occupiedSeats[freeSeat] = closestClient;
            closestClient.stateMachine.GoToSeat(freeSeat);
        }
    }
    
    public bool CallNextClient() 
    { 
        if (isRegistrationBusy || queue.Count == 0 || ClientSpawner.GetRegistrationZone().GetCurrentOccupancy() > 0) return false; 
        
        var nextInQueue = queue
            .Where(c => c.Key != null && (c.Key.stateMachine.GetCurrentState() == ClientState.AtWaitingArea || c.Key.stateMachine.GetCurrentState() == ClientState.SittingInWaitingArea))
            .OrderBy(kvp => kvp.Value)
            .FirstOrDefault(); 
        
        ClientPathfinding nextClient = nextInQueue.Key; 
        if (nextClient != null) 
        { 
            isRegistrationBusy = true; 
            clientBeingServed = nextClient; 
            currentlyCalledNumber = nextInQueue.Value; 
            noShowTimer = Time.time + noShowTimeout; 
            nextClient.stateMachine.GetCalledToRegistrar(); 
            return true; 
        } 
        return false; 
    }

    public void CheckForStalledRegistrar() { if (!isRegistrationBusy) return; if (clientBeingServed == null || Time.time > noShowTimer) { if (clientBeingServed != null) { clientBeingServed.stateMachine.SetState(ClientState.Confused); } ReleaseRegistrar(); } }
    public void ReleaseRegistrar() { isRegistrationBusy = false; clientBeingServed = null; currentlyCalledNumber = -1; }

    public void RemoveClientFromQueue(ClientPathfinding c) { if (c != null && queue.ContainsKey(c)) { if (c.notification != null) c.notification.SetQueueNumber(-1); OnClientLeavesWaitingZone(c); queue.Remove(c); } }
    
    public ClientPathfinding GetRandomClientFromQueue() { if (queue.Count == 0) return null; return queue.Keys.ElementAt(Random.Range(0, queue.Count)); }
    
    public Waypoint GetToiletReturnGoal(ClientPathfinding client) { if (queue.ContainsKey(client)) return mainWaitingZone.GetRandomStandingPoint().GetComponent<Waypoint>(); return ClientSpawner.Instance.exitWaypoint; }
    
    public Waypoint ChooseNewGoal(ClientPathfinding client) { return mainWaitingZone.GetRandomStandingPoint().GetComponent<Waypoint>(); }

    public void JoinQueue(ClientPathfinding c) { if (!queue.ContainsKey(c)) { queue.Add(c, nextQueueNumber++); if (c.notification != null) c.notification.SetQueueNumber(queue[c]); StartPatienceTimer(c); } }
    
    public void StartPatienceTimer(ClientPathfinding client) { StartCoroutine(PatienceCheck(client)); }
    private IEnumerator PatienceCheck(ClientPathfinding client) { yield return new WaitForSeconds(Random.Range(patienceMinTime, patienceMaxTime)); if (client == null || (client.stateMachine.GetCurrentState() != ClientState.AtWaitingArea && client.stateMachine.GetCurrentState() != ClientState.SittingInWaitingArea)) yield break; float choice = Random.value; if (choice < 0.25f) { client.stateMachine.DecideToVisitToilet(); } else if (choice < 0.65f) { RemoveClientFromQueue(client); client.stateMachine.SetState(ClientState.Confused); } else { StartPatienceTimer(client); } }
    
    public bool IsWaypointInWaitingZone(Waypoint wp)
    {
        if (mainWaitingZone == null || wp == null) return false;
        return mainWaitingZone.standingPoints.Any(p => p.GetComponent<Waypoint>() == wp);
    }
}