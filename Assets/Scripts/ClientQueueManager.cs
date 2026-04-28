using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

public class ClientQueueManager : MonoBehaviour
{
    public static ClientQueueManager Instance { get; private set; }

    [Header("Настройки")]
    public WaitingZone mainWaitingZone;
    public float clientResponseTimeout = 45f; // Увеличили с 15 до 45 секунд!
    public float callCooldown = 5f;
    public AudioClip nextClientSound;
    public float patienceMinTime = 8f;
    public float patienceMaxTime = 15f;
    
    private Dictionary<Transform, ClientPathfinding> occupiedSeats = new Dictionary<Transform, ClientPathfinding>();
    public List<ClientPathfinding> standingClients = new List<ClientPathfinding>();
    public Dictionary<ClientPathfinding, int> queue = new Dictionary<ClientPathfinding, int>();
    private int nextQueueNumber = 1;
    private float lastCallTime = -100f;

    public Dictionary<int, float> clientsAwaitingResponse = new Dictionary<int, float>();
    /// <summary>Публичное свойство для доступа к clientsAwaitingResponse (только чтение)</summary>
    public int ClientsAwaitingResponseCount => clientsAwaitingResponse.Count;
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
    
    private IServiceProvider FindAvailableWorker()
    {
        // Проверяем все столы: Регистратура (0), Клерк 1 (1), Клерк 2 (2), Касса (-1)
        int[] deskIds = { 0, 1, 2, -1 };

        foreach (int deskId in deskIds)
        {
            // ШАГ 1: Пропускаем авто-пуш для регистратуры (deskId=0) - она работает по Pull-модели
            if (deskId == 0) continue;

            var worker = ClientSpawner.GetServiceProviderAtDesk(deskId);
            if (worker != null && worker.IsAvailableToServe)
            {
                var zone = ClientSpawner.GetZoneByDeskId(deskId);
                if (zone != null)
                {
                    // Считаем только тех клиентов, чей талон УЖЕ вызван И кто идет ИМЕННО к этому работнику
                    int clientsMovingToThisWorker = queue.Keys.Count(c =>
                        c != null &&
                        c.stateMachine != null &&
                        currentlyCalledNumbers.Contains(queue[c]) &&
                        c.stateMachine.MyServiceProvider == worker);

                    // Если к работнику идет меньше людей, чем вмещает его зона - он может взять еще одного
                    if (clientsMovingToThisWorker < zone.capacity)
                    {
                        return worker;
                    }
                }
            }
        }
        return null;
    }

    private bool CanCallClient()
    {
        if (queue.Count == 0) return false;
        // Если есть хоть один свободный работник, к которому не идет толпа — вызываем
        return FindAvailableWorker() != null;
    }

    private void ProcessNextClientCall()
    {
        IServiceProvider availableWorker = FindAvailableWorker();
        if (availableWorker == null) return;

        var nextInQueue = queue
            .Where(c => c.Key != null && !currentlyCalledNumbers.Contains(c.Value))
            .OrderBy(kvp => kvp.Value)
            .FirstOrDefault();

        ClientPathfinding nextClient = nextInQueue.Key;

        if (nextClient != null)
        {
            ServicePoint workstation = availableWorker.GetWorkstation();
            if (workstation == null) return;

            // === НОВАЯ СИСТЕМА: Назначаем клиента на стойку ===
            workstation.AssignClient(nextClient);

            Waypoint destination = workstation.clientStandPoint;
            if (destination == null) return;

            lastCallTime = Time.time;
            if (nextClientSound != null) Managers.AudioManager.Instance?.PlayAudioClip2D(nextClientSound);

            int calledNumber = nextInQueue.Value;
            currentlyCalledNumbers.Add(calledNumber);
            clientsAwaitingResponse.Add(calledNumber, Time.time);
            
            // --- Логирование вызова клиента в его ActionDiary ---
            nextClient?.GetComponent<ActionDiary>()?.LogEvent($"Вызван к окну №{calledNumber}");

            // --- ОЗВУЧКА ВЫЗОВА ТАЛОНА ---
            var workerMono = availableWorker as MonoBehaviour;
            if (workerMono != null)
            {
                var bubble = workerMono.GetComponent<ThoughtBubbleController>();
                if (bubble != null) bubble.ShowPriorityMessage($"Талон №{calledNumber}, подходите!", 3f, Color.green);
            }
            // ------------------------------

            nextClient.stateMachine.GetCalledToSpecificDesk(destination, calledNumber, availableWorker);
        }
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
                // --- НОВАЯ БЛОКИРОВКА ТАЙМАУТА ---
                var cs = client.stateMachine?.GetCurrentState();
                bool isBeingServed = cs == ClientState.AtRegistration ||
                                     cs == ClientState.AtDesk1 ||
                                     cs == ClientState.AtDesk2 ||
                                     cs == ClientState.AtCashier ||
                                     cs == ClientState.InsideLimitedZone;
                
                if (isBeingServed)
                {
                    // Если клиент уже у стойки, просто удаляем его из списка ожидания ответа, не трогая его ИИ
                    clientsAwaitingResponse.Remove(ticketNumber);
                    continue;
                }
                // ---------------------------------
                
                // === Очищаем стойку, если клиент отвалился по таймауту ===
                if (client.stateMachine != null && client.stateMachine.MyServiceProvider != null)
                {
                    var sp = client.stateMachine.MyServiceProvider.GetWorkstation();
                    if (sp != null && sp.CurrentClient == client)
                    {
                        sp.ClearClient();
                    }
                }
                // ================================================================

                RemoveClientFromQueue(client);
                
                // Очищаем клиента из всех зон перед переходом в Confused
                var targetZone = client.stateMachine?.GetTargetZone();
                if (targetZone != null)
                {
                    targetZone.LeaveQueue(client.gameObject);
                    var goal = client.stateMachine?.GetCurrentGoal();
                    if (goal != null) targetZone.ReleaseWaypoint(goal);
                }
                
                // Очищаем из всех зон
                var allZones = FindObjectsOfType<LimitedCapacityZone>();
                foreach (var zone in allZones)
                {
                    zone.LeaveQueue(client.gameObject);
                }
                
                client.stateMachine?.SetState(ClientState.Confused);
                // Клиент удален из очереди - лог убран
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
        
        // Ранняя проверка: клиент вызван к столу — не проверяем терпение
        if (client == null) yield break;
        if (queue.ContainsKey(client) && currentlyCalledNumbers.Contains(queue[client])) yield break;
        
        // Проверка состояния очереди
        if (client.stateMachine.GetCurrentState() != ClientState.AtWaitingArea &&
            client.stateMachine.GetCurrentState() != ClientState.SittingInWaitingArea)
            yield break;
        
        float confusedChance = 0.65f;
        float choice = Random.value;
        if (choice < 0.15f) 
        { 
            client.stateMachine.DecideToVisitToilet();
        } 
        else if (choice < confusedChance)
        {
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
            return mainWaitingZone.GetRandomStandingPoint().GetComponent<Waypoint>();
        }
        return ClientSpawner.Instance?.exitWaypoint ?? null; 
    }
    
    // --- КОНЕЦ БЛОКА ВОССТАНОВЛЕННЫХ МЕТОДОВ ---
    
    public void ClientArrivedAtDesk(int number) { if (clientsAwaitingResponse.ContainsKey(number)) { clientsAwaitingResponse.Remove(number); } }
    public void ServiceFinishedForNumber(int number) { if (currentlyCalledNumbers.Contains(number)) { currentlyCalledNumbers.Remove(number); } if (clientsAwaitingResponse.ContainsKey(number)) { clientsAwaitingResponse.Remove(number); } }
    public void ResetQueueNumber() { nextQueueNumber = 1; currentlyCalledNumbers.Clear(); clientsAwaitingResponse.Clear(); }
    public void JoinQueue(ClientPathfinding c)
    {
        if (!queue.ContainsKey(c))
        {
            // Если это наглец (QueueJumper) - даем скрытый номер >= 10000
            if (c.isQueueJumper)
            {
                queue.Add(c, 10000 + Random.Range(1, 1000));
                if (c.notification != null) c.notification.SetQueueNumber(-1); // Скрытый номер не показываем
            }
            else
            {
                queue.Add(c, nextQueueNumber++);
                if (c.notification != null) c.notification.SetQueueNumber(queue[c]);
            }
            StartPatienceTimer(c);
        }
    }
    public void RemoveClientFromQueue(ClientPathfinding c) { if (c != null && queue.ContainsKey(c)) { ServiceFinishedForNumber(queue[c]); if (c.notification != null) c.notification.SetQueueNumber(-1); OnClientLeavesWaitingZone(c); queue.Remove(c); } }
    public Transform FindSeatForClient(ClientPathfinding client) { if (mainWaitingZone == null || mainWaitingZone.seatPoints.Count == 0) return null; Transform freeSeat = mainWaitingZone.seatPoints.FirstOrDefault(s => s != null && !occupiedSeats.ContainsKey(s)); if (freeSeat != null) { occupiedSeats[freeSeat] = client; if(standingClients.Contains(client)) standingClients.Remove(client); return freeSeat; } else { if (!standingClients.Contains(client)) standingClients.Add(client); return null; } }
    public void OnClientLeavesWaitingZone(ClientPathfinding client) { if (standingClients.Contains(client)) standingClients.Remove(client); if (occupiedSeats.ContainsValue(client)) { Transform seatToFree = occupiedSeats.FirstOrDefault(kvp => kvp.Value == client).Key; if (seatToFree != null) { occupiedSeats.Remove(seatToFree); FindAndAssignNearestStandingClient(seatToFree); } } }
    private void FindAndAssignNearestStandingClient(Transform freeSeat) { if (occupiedSeats.ContainsKey(freeSeat) || standingClients.Count == 0) return; ClientPathfinding closestClient = standingClients.OrderBy(c => Vector2.Distance(c.transform.position, freeSeat.position)).FirstOrDefault(); if (closestClient != null) { standingClients.Remove(closestClient); occupiedSeats[freeSeat] = closestClient; closestClient.stateMachine.GoToSeat(freeSeat); } }
    public Waypoint ChooseNewGoal(ClientPathfinding client) {
        // ChooseNewGoal логи убраны
        var result = mainWaitingZone?.GetRandomStandingPoint()?.GetComponent<Waypoint>();
        return result;
    }
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
	           .OrderBy(kvp => kvp.Value)
	           .FirstOrDefault();

	       ClientPathfinding targetClient = nextInQueue.Key;

	       // Если такой клиент найден
	       if (targetClient != null)
	       {
	           lastCallTime = Time.time;
	           if (nextClientSound != null) Managers.AudioManager.Instance?.PlayAudioClip2D(nextClientSound);

	           int calledNumber = nextInQueue.Value;
	           currentlyCalledNumbers.Add(calledNumber);
	           clientsAwaitingResponse.Add(calledNumber, Time.time);

	           // --- ОЗВУЧКА ВЫЗОВА ВНЕ ОЧЕРЕДИ ---
	           var workerMono = provider as MonoBehaviour;
	           if (workerMono != null)
	           {
	               var bubble = workerMono.GetComponent<ThoughtBubbleController>();
	               string message = calledNumber > 10000 ? "Следующий!" : $"Вне очереди, №{calledNumber}!";
	               if (bubble != null) bubble.ShowPriorityMessage(message, 3f, new Color(1f, 0.5f, 0f));
	           }
	           // ----------------------------------

	           Debug.Log($"<color=cyan>ОЧЕРЕДЬ (ПРИОРИТЕТ):</color> Работник {(provider as MonoBehaviour).name} вызывает клиента #{calledNumber} ({targetClient.name}) с целью '{goal}'");

	           // Вызываем клиента к стойке того, кто инициировал действие
	           targetClient.stateMachine.GetCalledToSpecificDesk(provider.GetClientStandPoint().GetComponent<Waypoint>(), calledNumber, provider);

	           return true;
	       }

	       return false;
	   }
	
	/// <summary>
	/// VIP-вызов конкретного клиента (например, когда принесли его документ из архива)
	/// </summary>
	public bool CallSpecificClient(ClientPathfinding targetClient, IServiceProvider provider)
	{
	    if (provider == null || targetClient == null) return false;

	    // Проверяем, есть ли он в очереди
	    if (queue.ContainsKey(targetClient))
	    {
	        int calledNumber = queue[targetClient];
	        
	        if (!currentlyCalledNumbers.Contains(calledNumber))
	        {
	            currentlyCalledNumbers.Add(calledNumber);
	            clientsAwaitingResponse.Add(calledNumber, Time.time);
	        }

	        lastCallTime = Time.time;
	        if (nextClientSound != null) Managers.AudioManager.Instance?.PlayAudioClip2D(nextClientSound);

	        var workerMono = provider as MonoBehaviour;
	        if (workerMono != null)
	        {
	            var bubble = workerMono.GetComponent<ThoughtBubbleController>();
	            string message = calledNumber > 10000 ? "Следующий!" : $"Талон №{calledNumber}, документ готов!";
	            if (bubble != null) bubble.ShowPriorityMessage(message, 3f, new Color(0f, 1f, 0f));
	        }

	        Debug.Log($"<color=cyan>ОЧЕРЕДЬ (VIP):</color> Работник {workerMono?.name} вызывает клиента #{calledNumber} ({targetClient.name}) вне очереди");

	        targetClient.stateMachine.GetCalledToSpecificDesk(provider.GetClientStandPoint().GetComponent<Waypoint>(), calledNumber, provider);
	        return true;
	    }
	    return false;
	}
}