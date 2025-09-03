// Файл: ClientQueueManager.cs
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
    
    [Tooltip("Сколько секунд клерк ждет, пока вызванный клиент дойдет до стойки, прежде чем вызвать следующего.")]
    public float clientResponseTimeout = 15f;
    private Dictionary<int, float> clientsAwaitingResponse = new Dictionary<int, float>();

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
    
    void Update()
    {
        if (clientsAwaitingResponse.Count > 0)
        {
            var timedOutNumbers = clientsAwaitingResponse
                .Where(kvp => Time.time - kvp.Value > clientResponseTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var ticketNumber in timedOutNumbers)
            {
                // --- ИСПРАВЛЕНО: Более надежная логика обработки "потерявшихся" клиентов ---
                Debug.LogWarning($"Клиент с талоном #{ticketNumber} не подошел вовремя. Начинаем процедуру очистки.");

                // 1. Ищем клиента, который связан с этим номером
                ClientPathfinding client = queue.FirstOrDefault(kvp => kvp.Value == ticketNumber).Key;

                // 2. Проверяем, существует ли еще этот клиент на сцене
                if (client != null)
                {
                    // 2а. Если существует - полностью удаляем его из системы очередей
                    RemoveClientFromQueue(client);
                    // И только потом меняем его состояние, чтобы он мог сориентироваться
                    client.stateMachine.SetState(ClientState.Confused);
                    Debug.Log($"Клиент {client.name} (#{ticketNumber}) полностью удален из очереди и переведен в состояние Confused.");
                }
                else
                {
                    // 2б. Если клиента уже нет (уничтожен), просто чистим "осиротевшие" номера
                    clientsAwaitingResponse.Remove(ticketNumber);
                    currentlyCalledNumbers.Remove(ticketNumber);
                    Debug.LogWarning($"Клиент с номером #{ticketNumber} не найден (возможно, был удален). Номер очищен из системы.");
                }
                // --- КОНЕЦ ИСПРАВЛЕНИЯ ---
            }
        }
    }

    public void ResetQueueNumber() 
    { 
        nextQueueNumber = 1;
        currentlyCalledNumbers.Clear();
        clientsAwaitingResponse.Clear();
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
        
        int activeClientsInZone = registrationZone.GetOccupyingClients()
            .Count(c => c != null && queue.ContainsKey(c) && currentlyCalledNumbers.Contains(queue[c]));

        if (activeClientsInZone >= registrationZone.capacity)
        {
            return false;
        }

        // --- ИСПРАВЛЕНО: Добавлена проверка c.Key != null, чтобы исключить уничтоженных клиентов из выборки ---
        var nextInQueue = queue
            .Where(c => c.Key != null && !currentlyCalledNumbers.Contains(c.Value) && !clientsAwaitingResponse.ContainsKey(c.Value))
            .OrderBy(kvp => kvp.Value)
            .FirstOrDefault();
            
        ClientPathfinding nextClient = nextInQueue.Key;

        // Здесь происходит проверка на существование клиента перед вызовом
        if (nextClient != null)
        {
            if (nextClientSound != null) AudioSource.PlayClipAtPoint(nextClientSound, clerk.transform.position);
            int calledNumber = nextInQueue.Value;
            
            currentlyCalledNumbers.Add(calledNumber);
            clientsAwaitingResponse.Add(calledNumber, Time.time);
            
            Debug.Log($"Клерк {clerk.name} вызывает клиента #{calledNumber} ({nextClient.name}) к стойке {clerk.assignedServicePoint.name}");

            nextClient.stateMachine.GetCalledToSpecificDesk(clerk.assignedServicePoint.clientStandPoint, calledNumber, clerk);
            return true;
        }
        return false;
    }
    
    public void ClientArrivedAtDesk(int number)
    {
        if (clientsAwaitingResponse.ContainsKey(number))
        {
            clientsAwaitingResponse.Remove(number);
        }
    }

    public void ServiceFinishedForNumber(int number)
    {
        if (currentlyCalledNumbers.Contains(number))
        {
            currentlyCalledNumbers.Remove(number);
        }
        if (clientsAwaitingResponse.ContainsKey(number))
        {
            clientsAwaitingResponse.Remove(number);
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
        // Добавим проверку, чтобы не вернуть уничтоженного клиента
        var validClients = queue.Keys.Where(c => c != null).ToList();
        if (validClients.Count == 0) return null;
        return validClients[Random.Range(0, validClients.Count)];
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
        if (client == null || (client.stateMachine.GetCurrentState() != ClientState.AtWaitingArea && client.stateMachine.GetCurrentState() != ClientState.SittingInWaitingArea) || currentlyCalledNumbers.Contains(queue.ContainsKey(client) ? queue[client] : -1)) 
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
}