using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ClientQueueManager : MonoBehaviour
{
    public static ClientQueueManager Instance { get; private set; }

    [Header("Настройки")]
    public WaitingZone mainWaitingZone;
    public float clientResponseTimeout = 15f;
    public float callCooldown = 5f;
    public AudioClip nextClientSound;
    public float patienceMinTime = 8f;
    public float patienceMaxTime = 15f;
    
    private Dictionary<Transform, ClientPathfinding> occupiedSeats = new Dictionary<Transform, ClientPathfinding>();
    private List<ClientPathfinding> standingClients = new List<ClientPathfinding>();
    public Dictionary<ClientPathfinding, int> queue = new Dictionary<ClientPathfinding, int>();
    private int nextQueueNumber = 1;
    private float lastCallTime = -100f;

    private Dictionary<int, float> clientsAwaitingResponse = new Dictionary<int, float>();
    public List<int> currentlyCalledNumbers = new List<int>();
    public List<ClientPathfinding> dissatisfiedClients = new List<ClientPathfinding>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }
    
    void Update()
    {
        // --- НОВАЯ ПРОАКТИВНАЯ ЛОГИКА ВЫЗОВА ---
        if (Time.time > lastCallTime + callCooldown && CanCallClient())
        {
            ProcessNextClientCall();
        }
        
        HandleTimedOutClients();
    }
    
    private void ProcessNextClientCall()
{
    IServiceProvider registrar = ClientSpawner.GetServiceProviderAtDesk(0);
    if (registrar == null || !registrar.IsAvailableToServe) return;
    
    var nextInQueue = queue
        .Where(c => c.Key != null && !currentlyCalledNumbers.Contains(c.Value))
        .OrderBy(kvp => kvp.Value)
        .FirstOrDefault();
    
    ClientPathfinding nextClient = nextInQueue.Key;

    if (nextClient != null)
    {
        // ----- НАЧАЛО ИЗМЕНЕНИЙ: БОЛЕЕ НАДЕЖНОЕ ПОЛУЧЕНИЕ ТОЧКИ НАЗНАЧЕНИЯ -----

        // 1. Сначала получаем рабочее место (ServicePoint) регистратора.
        ServicePoint workstation = registrar.GetWorkstation();
        if (workstation == null)
        {
            Debug.LogError($"[ClientQueueManager] Регистратор {((MonoBehaviour)registrar).name} доступен, но у него не назначено рабочее место (GetWorkstation вернул null)!", (MonoBehaviour)registrar);
            return;
        }

        // 2. Затем получаем точку назначения (Waypoint) с этого рабочего места.
        Waypoint destination = workstation.clientStandPoint;
        if (destination == null)
        {
            Debug.LogError($"[ClientQueueManager] У рабочего места {workstation.name} не назначена точка для клиента (clientStandPoint)!", workstation);
            return;
        }
        
        // ----- КОНЕЦ ИЗМЕНЕНИЙ -----

        lastCallTime = Time.time;
        if (nextClientSound != null) AudioSource.PlayClipAtPoint(nextClientSound, ((MonoBehaviour)registrar).transform.position);
        
        int calledNumber = nextInQueue.Value;
        currentlyCalledNumbers.Add(calledNumber);
        clientsAwaitingResponse.Add(calledNumber, Time.time);
        
        Debug.Log($"<color=yellow>ОЧЕРЕДЬ:</color> Работник {((MonoBehaviour)registrar).name} вызывает клиента #{calledNumber} ({nextClient.name})");

        // 3. Передаем клиенту гарантированно существующую точку назначения.
        nextClient.stateMachine.GetCalledToSpecificDesk(destination, calledNumber, registrar);
    }
}
    
    private bool CanCallClient()
    {
        if (queue.Count == 0) return false;

        IServiceProvider registrar = ClientSpawner.GetServiceProviderAtDesk(0);
        if (registrar == null || !registrar.IsAvailableToServe) return false;

        LimitedCapacityZone registrationZone = ClientSpawner.GetRegistrationZone();
        if (registrationZone != null)
        {
            int clientsMovingToZone = currentlyCalledNumbers.Count;
            if (clientsMovingToZone >= registrationZone.capacity)
            {
                return false;
            }
        }
        return true;
    }

    private void HandleTimedOutClients()
    {
        if (clientsAwaitingResponse.Count == 0) return;

        var timedOutNumbers = clientsAwaitingResponse
            .Where(kvp => Time.time - kvp.Value > clientResponseTimeout)
            .Select(kvp => kvp.Key).ToList();

        foreach (var ticketNumber in timedOutNumbers)
        {
            Debug.LogWarning($"Клиент с талоном #{ticketNumber} не подошел вовремя. Начинаем процедуру очистки.");
            ClientPathfinding client = queue.FirstOrDefault(kvp => kvp.Value == ticketNumber).Key;
            
            if (client != null)
            {
                RemoveClientFromQueue(client);
                client.stateMachine.SetState(ClientState.Confused);
                Debug.Log($"Клиент {client.name} (#{ticketNumber}) полностью удален из очереди и переведен в состояние Confused.");
            }
            else
            {
                clientsAwaitingResponse.Remove(ticketNumber);
                currentlyCalledNumbers.Remove(ticketNumber);
                Debug.LogWarning($"Клиент с номером #{ticketNumber} не найден. Номер очищен из системы.");
            }
        }
    }

    // --- НАЧАЛО БЛОКА ВОССТАНОВЛЕННЫХ МЕТОДОВ ---

    public void StartPatienceTimer(ClientPathfinding client) 
    { 
        StartCoroutine(PatienceCheck(client));
    }
    
    private IEnumerator PatienceCheck(ClientPathfinding client) 
    { 
        float minWait = patienceMinTime * (1f + client.babushkaFactor) * (1f - client.suetunFactor * 0.5f);
        float maxWait = patienceMaxTime * (1f + client.babushkaFactor) * (1f - client.suetunFactor * 0.5f);
        yield return new WaitForSeconds(Random.Range(minWait, maxWait));
        
        if (client == null || (client.stateMachine.GetCurrentState() != ClientState.AtWaitingArea && client.stateMachine.GetCurrentState() != ClientState.SittingInWaitingArea) || (queue.ContainsKey(client) && currentlyCalledNumbers.Contains(queue[client]))) 
            yield break;
        
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

    public Waypoint GetToiletReturnGoal(ClientPathfinding client) 
{ 
    if (queue.ContainsKey(client))
    {
        // GetRandomStandingPoint() возвращает Transform. Нам нужно получить с него компонент Waypoint.
        return mainWaitingZone.GetRandomStandingPoint().GetComponent<Waypoint>();
    }
    return ClientSpawner.Instance.exitWaypoint; 
}
    
    // --- КОНЕЦ БЛОКА ВОССТАНОВЛЕННЫХ МЕТОДОВ ---
    
    public void ClientArrivedAtDesk(int number) { if (clientsAwaitingResponse.ContainsKey(number)) { clientsAwaitingResponse.Remove(number); } }
    public void ServiceFinishedForNumber(int number) { if (currentlyCalledNumbers.Contains(number)) { currentlyCalledNumbers.Remove(number); } if (clientsAwaitingResponse.ContainsKey(number)) { clientsAwaitingResponse.Remove(number); } }
    public void ResetQueueNumber() { nextQueueNumber = 1; currentlyCalledNumbers.Clear(); clientsAwaitingResponse.Clear(); }
    public void JoinQueue(ClientPathfinding c) { if (!queue.ContainsKey(c)) { queue.Add(c, nextQueueNumber++); if (c.notification != null) c.notification.SetQueueNumber(queue[c]); StartPatienceTimer(c); } }
    public void RemoveClientFromQueue(ClientPathfinding c) { if (c != null && queue.ContainsKey(c)) { ServiceFinishedForNumber(queue[c]); if (c.notification != null) c.notification.SetQueueNumber(-1); OnClientLeavesWaitingZone(c); queue.Remove(c); } }
    public Transform FindSeatForClient(ClientPathfinding client) { if (mainWaitingZone == null || mainWaitingZone.seatPoints.Count == 0) return null; Transform freeSeat = mainWaitingZone.seatPoints.FirstOrDefault(s => s != null && !occupiedSeats.ContainsKey(s)); if (freeSeat != null) { occupiedSeats[freeSeat] = client; if(standingClients.Contains(client)) standingClients.Remove(client); return freeSeat; } else { if (!standingClients.Contains(client)) standingClients.Add(client); return null; } }
    public void OnClientLeavesWaitingZone(ClientPathfinding client) { if (standingClients.Contains(client)) standingClients.Remove(client); if (occupiedSeats.ContainsValue(client)) { Transform seatToFree = occupiedSeats.FirstOrDefault(kvp => kvp.Value == client).Key; if (seatToFree != null) { occupiedSeats.Remove(seatToFree); FindAndAssignNearestStandingClient(seatToFree); } } }
    private void FindAndAssignNearestStandingClient(Transform freeSeat) { if (occupiedSeats.ContainsKey(freeSeat) || standingClients.Count == 0) return; ClientPathfinding closestClient = standingClients.OrderBy(c => Vector2.Distance(c.transform.position, freeSeat.position)).FirstOrDefault(); if (closestClient != null) { standingClients.Remove(closestClient); occupiedSeats[freeSeat] = closestClient; closestClient.stateMachine.GoToSeat(freeSeat); } }
    public Waypoint ChooseNewGoal(ClientPathfinding client) { return mainWaitingZone.GetRandomStandingPoint().GetComponent<Waypoint>(); }
    public void AddAngryClient(ClientPathfinding client) { if (!dissatisfiedClients.Contains(client)) { dissatisfiedClients.Add(client); } }
	
	/// <summary>
/// Ищет в очереди первого клиента с указанной целью и вызывает его к указанному сотруднику.
/// </summary>
/// <returns>Возвращает true, если клиент был найден и вызван, иначе false.</returns>
public bool CallClientWithSpecificGoal(ClientGoal goal, IServiceProvider provider)
{
    if (provider == null || !provider.IsAvailableToServe) return false;

    // Ищем в очереди первого клиента, который соответствует цели и еще не был вызван.
    var nextInQueue = queue
        .Where(c => c.Key != null && c.Key.mainGoal == goal && !currentlyCalledNumbers.Contains(c.Value))
        .OrderBy(kvp => kvp.Value) // Сортируем по номеру, чтобы взять первого из подходящих
        .FirstOrDefault();

    ClientPathfinding targetClient = nextInQueue.Key;

    // Если такой клиент найден
    if (targetClient != null)
    {
        lastCallTime = Time.time;
        if (nextClientSound != null) AudioSource.PlayClipAtPoint(nextClientSound, (provider as MonoBehaviour).transform.position);

        int calledNumber = nextInQueue.Value;
        currentlyCalledNumbers.Add(calledNumber);
        clientsAwaitingResponse.Add(calledNumber, Time.time);

        Debug.Log($"<color=cyan>ОЧЕРЕДЬ (ПРИОРИТЕТ):</color> Работник {(provider as MonoBehaviour).name} вызывает клиента #{calledNumber} ({targetClient.name}) с целью '{goal}'");

        // Вызываем клиента к стойке того, кто инициировал действие
        targetClient.stateMachine.GetCalledToSpecificDesk(provider.GetClientStandPoint().GetComponent<Waypoint>(), calledNumber, provider);

        return true; // Сообщаем об успехе
    }

    return false; // Клиент с такой целью не найден
}
	
}