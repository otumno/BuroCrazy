using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ClientQueueManager : MonoBehaviour
{
    private ClientPathfinding parent;
    [Header("Настройки времени")]
    [SerializeField] public float minWaitTime = 2f;
    [SerializeField] public float maxWaitTime = 5f;
    [SerializeField] public float patienceMinTime = 8f;
    [SerializeField] public float patienceMaxTime = 15f;
    
    [Header("Настройки толпы")]
    [Tooltip("Количество клиентов, которое считается толпой")]
    [SerializeField] public int crowdThreshold = 10;
    [Tooltip("Время в секундах, через которое толпа может вызвать недовольство")]
    [SerializeField] public float crowdTimeout = 5f;
    [Tooltip("Шанс (0-1), что клиент разозлится из-за толпы")]
    [SerializeField] public float crowdDissatisfactionChance = 0.2f;

    // --- Общая очередь к регистратору ---
    private static Dictionary<ClientPathfinding, int> queue = new Dictionary<ClientPathfinding, int>();
    private static int nextQueueNumber = 1;
    private static bool isRegistrationBusy = false;
    private static ClientPathfinding clientBeingServed;
    private static float noShowTimer = 0f;
    private static float noShowTimeout = 20f;

    // --- Управление столами ---
    private static bool isDesk1Busy = false;
    private static bool isDesk2Busy = false;
    private static bool isRegistrarAvailable = true;
    private static bool isDesk1Available = true;
    private static bool isDesk2Available = true;
    private static ClientPathfinding clientAtDesk1;
    private static ClientPathfinding clientAtDesk2;

    public static List<ClientPathfinding> dissatisfiedClients = new List<ClientPathfinding>();
    public static int currentlyCalledNumber = -1;
    private float crowdTimer = 0f;
    private bool isCrowdActive = false;
    private Coroutine patienceCoroutine;
    private static Waypoint _registrationWaypoint, _desk1Waypoint, _desk2Waypoint;
    private Waypoint registrationWaitingWaypoint, toiletWaypoint, exitWaypoint;
    private GameObject waitingZoneObject, toiletZoneObject, registrationZoneObject, desk1Object, desk2Object;
    
    private static Waypoint[] allWaypointsCache;

    public void Initialize(ClientPathfinding p, GameObject rZ, GameObject wZ, GameObject tZ, GameObject d1, GameObject d2, Waypoint eW)
    {
        parent = p;
        registrationZoneObject = rZ;
        waitingZoneObject = wZ;
        toiletZoneObject = tZ;
        desk1Object = d1;
        desk2Object = d2;
        _registrationWaypoint = rZ.GetComponent<Waypoint>();
        _desk1Waypoint = d1.GetComponent<Waypoint>();
        _desk2Waypoint = d2.GetComponent<Waypoint>();
        registrationWaitingWaypoint = wZ.GetComponent<Waypoint>();
        toiletWaypoint = tZ.GetComponent<Waypoint>();
        exitWaypoint = eW;

        if (allWaypointsCache == null)
        {
            allWaypointsCache = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        }
    }

    void Update() { CheckCrowd(); }

    public static bool CallNextClient()
    {
        if (isRegistrationBusy || queue.Count == 0 || !isRegistrarAvailable) return false;
        
        var nextInQueue = queue.Where(c => c.Key != null && c.Key.stateMachine.GetCurrentState() == ClientState.AtWaitingArea).OrderBy(kvp => kvp.Value).FirstOrDefault();
        ClientPathfinding nextClient = nextInQueue.Key;
        if (nextClient != null)
        {
            isRegistrationBusy = true;
            clientBeingServed = nextClient;
            currentlyCalledNumber = nextInQueue.Value;
            noShowTimer = Time.time + noShowTimeout;
            nextClient.stateMachine.SetGoal(_registrationWaypoint);
            nextClient.stateMachine.SetState(ClientState.MovingToGoal);
            nextClient.stateMachine.RecalculatePath();
            return true;
        }
        return false;
    }
    
    public static void CheckForStalledRegistrar()
    {
        if (!isRegistrationBusy) return;

        if (clientBeingServed == null || Time.time > noShowTimer)
        {
            if (clientBeingServed != null)
            {
                Debug.LogWarning($"Регистратор не дождался {clientBeingServed.name}. Вызываю следующего.");
                clientBeingServed.stateMachine.SetState(ClientState.Confused);
            }
            else
            {
                Debug.LogWarning($"Регистратор завис (обслуживаемый клиент исчез). Принудительно освобождаю и вызываю следующего.");
            }
            ReleaseRegistrar();
            CallNextClient();
        }
    }
    
    public static void ReleaseRegistrar() { isRegistrationBusy = false; clientBeingServed = null; currentlyCalledNumber = -1; }

    public void OnClientServedFromRegistrar() { RemoveClientFromQueue(parent); }

    private IEnumerator PatienceCheck() { yield return new WaitForSeconds(Random.Range(patienceMinTime, patienceMaxTime)); if (parent == null || parent.stateMachine.GetCurrentState() != ClientState.AtWaitingArea) yield break; float choice = Random.value; Waypoint goal = null; if (choice < 0.25f) { goal = toiletWaypoint; } else if (choice < 0.50f) { goal = _registrationWaypoint; } else if (choice < 0.65f) { RemoveClientFromQueue(parent); parent.stateMachine.SetState(ClientState.Confused); yield break; } else { StartPatienceTimer(); yield break; } if (goal != null) { RemoveClientFromQueue(parent); parent.stateMachine.SetGoal(goal); if (goal == exitWaypoint) parent.stateMachine.SetState(ClientState.Leaving); else parent.stateMachine.SetState(ClientState.MovingToGoal); parent.stateMachine.RecalculatePath(); } }
    
    private void CheckCrowd() {
        if (waitingZoneObject == null) return;
        Collider2D[] clients = Physics2D.OverlapCircleAll(waitingZoneObject.transform.position, 2f, LayerMask.GetMask("Client"));
        
        if (clients.Length > crowdThreshold) { 
            if (!isCrowdActive) {
                isCrowdActive = true;
                crowdTimer = Time.time;
            } else if (Time.time - crowdTimer > crowdTimeout) {
                if (Random.value < crowdDissatisfactionChance && dissatisfiedClients.Count < 2) {
                    var clientToEnrage = queue.Where(kvp => kvp.Key != null && kvp.Key.stateMachine.GetCurrentState() == ClientState.AtWaitingArea && clients.Any(c => c.gameObject == kvp.Key.gameObject)).OrderBy(kvp => kvp.Value).FirstOrDefault().Key;
                    if (clientToEnrage != null) {
                        if (clientToEnrage.dissatisfiedExitSound != null) AudioSource.PlayClipAtPoint(clientToEnrage.dissatisfiedExitSound, clientToEnrage.transform.position);
                        clientToEnrage.reasonForLeaving = ClientPathfinding.LeaveReason.Angry;
                        dissatisfiedClients.Add(clientToEnrage);
                        clientToEnrage.stateMachine.SetState(ClientState.Enraged);
                        RemoveClientFromQueue(clientToEnrage);
                    }
                }
                crowdTimer = Time.time;
            }
        } else {
            isCrowdActive = false;
        }
    }
    
    public static void SetServicePointAvailability(int servicePointId, bool isAvailable)
    {
        switch (servicePointId)
        {
            case 0: isRegistrarAvailable = isAvailable; break;
            case 1: isDesk1Available = isAvailable; break;
            case 2: isDesk2Available = isAvailable; break;
        }
        Debug.Log($"Service Point {servicePointId} availability set to {isAvailable}");
    }

    public static bool IsRegistrarAvailable() => isRegistrarAvailable;
    public static bool IsDesk1Available() => isDesk1Available;
    public static bool IsDesk2Available() => isDesk2Available;
    
    public bool HasQueueNumber() => queue.ContainsKey(parent);
    public static bool IsRegistrarBusy() => isRegistrationBusy;
    public static bool IsRegistrarBusyWithAnother(ClientPathfinding client) => isRegistrationBusy && clientBeingServed != client;
    public static bool IsDesk1Busy() => isDesk1Busy;
    public static bool IsDesk2Busy() => isDesk2Busy;
    public static void OccupyDesk(int n, ClientPathfinding c) { if (n == 1) { isDesk1Busy = true; clientAtDesk1 = c; } else { isDesk2Busy = true; clientAtDesk2 = c; } }
    public static void ReleaseDesk(int n) { if (n == 1) isDesk1Busy = false; else isDesk2Busy = false; }
    public Waypoint GetWaitingWaypoint() => registrationWaitingWaypoint;
    public Waypoint GetDesk1Waypoint() => _desk1Waypoint;
    public Waypoint GetDesk2Waypoint() => _desk2Waypoint;
    public Waypoint GetToiletWaypoint() => toiletWaypoint;
    public Waypoint GetExitWaypoint() => exitWaypoint;
    public GameObject GetDesk1Zone() => desk1Object;
    public GameObject GetDesk2Zone() => desk2Object;
    public GameObject GetWaitingZone() => waitingZoneObject;
    public GameObject GetToiletZone() => toiletZoneObject;
    public GameObject GetRegistrationZone() => registrationZoneObject;
    
    public void AssignInitialGoal()
    {
        float choice = Random.value;
        Waypoint goal = null;
        if (choice < 0.6f) goal = registrationWaitingWaypoint; 
        else if (choice < 0.9f) goal = _registrationWaypoint; 
        else goal = toiletWaypoint;
        parent.stateMachine.SetGoal(goal);
        parent.stateMachine.SetState(ClientState.MovingToGoal);
        parent.stateMachine.RecalculatePath();
    }
    
    public void JoinQueue(ClientPathfinding c) { if (queue.ContainsKey(c)) queue.Remove(c); queue.Add(c, nextQueueNumber++); if (c.notification != null) c.notification.SetQueueNumber(queue[c]); }
    public void RemoveClientFromQueue(ClientPathfinding c) { if (c != null && queue.ContainsKey(c)) { if (c.notification != null) c.notification.SetQueueNumber(-1); queue.Remove(c); } }
    public Waypoint GetToiletReturnGoal(Waypoint prevGoal) { if (prevGoal != null && prevGoal != toiletWaypoint) return prevGoal; if (queue.ContainsKey(parent)) return registrationWaitingWaypoint; return exitWaypoint; }
    public Waypoint GetRandomWaypoint_NoExit() { if (allWaypointsCache == null || allWaypointsCache.Length == 0) return null; Waypoint randomWp; do { randomWp = allWaypointsCache[Random.Range(0, allWaypointsCache.Length)]; } while (randomWp == exitWaypoint); return randomWp; }
    public Waypoint ChooseNewGoal(ClientState lastState) { float choice = Random.value; if (lastState == ClientState.AtToilet) { if (choice < 0.6f && queue.ContainsKey(parent)) return registrationWaitingWaypoint; else if (choice < 0.8f) return _registrationWaypoint; return exitWaypoint; } else { if (choice < 0.5f && queue.ContainsKey(parent)) return registrationWaitingWaypoint; else if (choice < 0.58f) return _registrationWaypoint; else if (choice < 0.6f) return toiletWaypoint; return exitWaypoint; } }
    public void StartPatienceTimer() { if (patienceCoroutine != null) StopCoroutine(patienceCoroutine); patienceCoroutine = StartCoroutine(PatienceCheck()); }
}