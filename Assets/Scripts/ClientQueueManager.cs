using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ClientQueueManager : MonoBehaviour
{
    private ClientPathfinding parent;
    [Header("Настройки времени")]
    public float minWaitTime = 2f;
    public float maxWaitTime = 5f;
    public float patienceMinTime = 8f;
    public float patienceMaxTime = 15f;
    [Header("Настройки толпы")]
    public int crowdThreshold = 10;
    public float crowdTimeout = 5f;
    public float crowdDissatisfactionChance = 0.2f;

    private static List<Transform> seats;
    private static Dictionary<Transform, ClientPathfinding> occupiedSeats = new Dictionary<Transform, ClientPathfinding>();
    private static List<ClientPathfinding> standingClients = new List<ClientPathfinding>();
    private static Dictionary<ClientPathfinding, int> queue = new Dictionary<ClientPathfinding, int>();
    private static int nextQueueNumber = 1;
    private static bool isRegistrationBusy = false;
    private static ClientPathfinding clientBeingServed;
    private static float noShowTimer = 0f;
    private static float noShowTimeout = 20f;
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
    private static Waypoint _registrationWaypoint, _desk1Waypoint, _desk2Waypoint, _exitWaypoint;
    private Waypoint registrationWaitingWaypoint, toiletWaypoint;
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
        _exitWaypoint = eW;
        registrationWaitingWaypoint = wZ.GetComponent<Waypoint>();
        toiletWaypoint = tZ.GetComponent<Waypoint>();
        if (allWaypointsCache == null) { allWaypointsCache = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); }
        if (seats == null) { seats = new List<Transform>(); GameObject[] seatObjects = GameObject.FindGameObjectsWithTag("Seat"); foreach (GameObject seatObj in seatObjects) { seats.Add(seatObj.transform); } }
    }

    void Update() { CheckCrowd(); }
    public static void ResetQueueNumber() { nextQueueNumber = 1; }
    private IEnumerator PatienceCheck() { yield return new WaitForSeconds(Random.Range(patienceMinTime, patienceMaxTime)); if (parent == null || (parent.stateMachine.GetCurrentState() != ClientState.AtWaitingArea && parent.stateMachine.GetCurrentState() != ClientState.SittingInWaitingArea)) yield break; float choice = Random.value; if (choice < 0.25f) { parent.stateMachine.DecideToVisitToilet(); } else if (choice < 0.65f) { RemoveClientFromQueue(parent); parent.stateMachine.SetState(ClientState.Confused); } else { StartPatienceTimer(); } }
    public Transform FindSeatForClient(ClientPathfinding client) { Transform freeSeat = seats.FirstOrDefault(s => !occupiedSeats.ContainsKey(s)); if (freeSeat != null) { occupiedSeats[freeSeat] = client; return freeSeat; } else { if (!standingClients.Contains(client)) standingClients.Add(client); return null; } }
    public void OnClientLeavesWaitingZone(ClientPathfinding client) { if (standingClients.Contains(client)) standingClients.Remove(client); if (occupiedSeats.ContainsValue(client)) { Transform seatToFree = occupiedSeats.FirstOrDefault(kvp => kvp.Value == client).Key; if (seatToFree != null) { occupiedSeats.Remove(seatToFree); FindAndAssignNearestStandingClient(seatToFree); } } }
    private void FindAndAssignNearestStandingClient(Transform freeSeat) { if (standingClients.Count == 0) return; ClientPathfinding closestClient = standingClients.OrderBy(c => Vector2.Distance(c.transform.position, freeSeat.position)).FirstOrDefault(); if (closestClient != null) { standingClients.Remove(closestClient); occupiedSeats[freeSeat] = closestClient; closestClient.stateMachine.GoToSeat(freeSeat); } }
    public static bool CallNextClient() { if (isRegistrationBusy || queue.Count == 0 || !isRegistrarAvailable) return false; var nextInQueue = queue.Where(c => c.Key != null && (c.Key.stateMachine.GetCurrentState() == ClientState.AtWaitingArea || c.Key.stateMachine.GetCurrentState() == ClientState.SittingInWaitingArea)).OrderBy(kvp => kvp.Value).FirstOrDefault(); ClientPathfinding nextClient = nextInQueue.Key; if (nextClient != null) { isRegistrationBusy = true; clientBeingServed = nextClient; currentlyCalledNumber = nextInQueue.Value; noShowTimer = Time.time + noShowTimeout; nextClient.stateMachine.GetCalledToRegistrar(); return true; } return false; }
    public void RemoveClientFromQueue(ClientPathfinding c) { if (c != null && queue.ContainsKey(c)) { if (c.notification != null) c.notification.SetQueueNumber(-1); OnClientLeavesWaitingZone(c); queue.Remove(c); } }
    public static void CheckForStalledRegistrar() { if (!isRegistrationBusy) return; if (clientBeingServed == null || Time.time > noShowTimer) { if (clientBeingServed != null) { clientBeingServed.stateMachine.SetState(ClientState.Confused); } ReleaseRegistrar(); CallNextClient(); } }
    public static void ReleaseRegistrar() { isRegistrationBusy = false; clientBeingServed = null; currentlyCalledNumber = -1; }
    public void OnClientServedFromRegistrar() { RemoveClientFromQueue(parent); }
    private void CheckCrowd() { if (waitingZoneObject == null) return; Collider2D[] clients = Physics2D.OverlapCircleAll(waitingZoneObject.transform.position, 2f, LayerMask.GetMask("Client")); if (clients.Length > crowdThreshold) { if (!isCrowdActive) { isCrowdActive = true; crowdTimer = Time.time; } else if (Time.time - crowdTimer > crowdTimeout) { if (Random.value < crowdDissatisfactionChance && dissatisfiedClients.Count < 2) { var clientToEnrage = queue.Where(kvp => kvp.Key != null && kvp.Key.stateMachine.GetCurrentState() == ClientState.AtWaitingArea && clients.Any(c => c.gameObject == kvp.Key.gameObject)).OrderBy(kvp => kvp.Value).FirstOrDefault().Key; if (clientToEnrage != null) { if (clientToEnrage.dissatisfiedExitSound != null) AudioSource.PlayClipAtPoint(clientToEnrage.dissatisfiedExitSound, clientToEnrage.transform.position); clientToEnrage.reasonForLeaving = ClientPathfinding.LeaveReason.Angry; dissatisfiedClients.Add(clientToEnrage); clientToEnrage.stateMachine.SetState(ClientState.Enraged); RemoveClientFromQueue(clientToEnrage); } } crowdTimer = Time.time; } } else { isCrowdActive = false; } }
    public static void SetServicePointAvailability(int servicePointId, bool isAvailable) { switch (servicePointId) { case 0: isRegistrarAvailable = isAvailable; break; case 1: isDesk1Available = isAvailable; break; case 2: isDesk2Available = isAvailable; break; } }
    public static ClientPathfinding GetRandomClientFromQueue() { return queue.Keys.FirstOrDefault(c => c.stateMachine.GetCurrentState() == ClientState.AtWaitingArea || c.stateMachine.GetCurrentState() == ClientState.SittingInWaitingArea); }
    public static Transform GetServicePointTransform(int servicePointId) { switch (servicePointId) { case 0: return _registrationWaypoint.transform; case 1: return _desk1Waypoint.transform; case 2: return _desk2Waypoint.transform; default: return null; } }
    public static Waypoint GetExitWaypointStatic() => _exitWaypoint;
    public static bool IsRegistrarAvailable() => isRegistrarAvailable;
    public static bool IsDesk1Available() => isDesk1Available;
    public static bool IsDesk2Available() => isDesk2Available;
    public static bool IsDeskAvailable(int deskNum) => deskNum == 1 ? IsDesk1Available() : IsDesk2Available();
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
    public Waypoint GetExitWaypoint() => _exitWaypoint;
    public GameObject GetDesk1Zone() => desk1Object;
    public GameObject GetDesk2Zone() => desk2Object;
    public GameObject GetWaitingZone() => waitingZoneObject;
    public GameObject GetToiletZone() => toiletZoneObject;
    public GameObject GetRegistrationZone() => registrationZoneObject;
    public void AssignInitialGoal() { float choice = Random.value; Waypoint goal; ClientState initialState = ClientState.MovingToGoal; if (choice < 0.6f) { goal = registrationWaitingWaypoint; } else if (choice < 0.9f) { goal = _registrationWaypoint; initialState = ClientState.MovingToRegistrarImpolite; } else { goal = toiletWaypoint; } parent.stateMachine.SetGoal(goal); parent.stateMachine.SetState(initialState); parent.stateMachine.RecalculatePath(); }
    public void JoinQueue(ClientPathfinding c) { if (queue.ContainsKey(c)) queue.Remove(c); queue.Add(c, nextQueueNumber++); if (c.notification != null) c.notification.SetQueueNumber(queue[c]); }
    public Waypoint GetToiletReturnGoal(Waypoint prevGoal) { if (prevGoal != null && prevGoal != toiletWaypoint) return prevGoal; if (queue.ContainsKey(parent)) return registrationWaitingWaypoint; return _exitWaypoint; }
    public Waypoint GetRandomWaypoint_NoExit() { if (allWaypointsCache == null || allWaypointsCache.Length == 0) return null; Waypoint randomWp; do { randomWp = allWaypointsCache[Random.Range(0, allWaypointsCache.Length)]; } while (randomWp == _exitWaypoint); return randomWp; }
    public Waypoint ChooseNewGoal(ClientState lastState) { float choice = Random.value; if (lastState == ClientState.AtToilet) { if (choice < 0.6f && queue.ContainsKey(parent)) return registrationWaitingWaypoint; else if (choice < 0.8f) return _registrationWaypoint; return _exitWaypoint; } else { if (choice < 0.5f && queue.ContainsKey(parent)) return registrationWaitingWaypoint; else if (choice < 0.58f) return _registrationWaypoint; else if (choice < 0.6f) return toiletWaypoint; return _exitWaypoint; } }
    public void StartPatienceTimer() { if (patienceCoroutine != null) StopCoroutine(patienceCoroutine); patienceCoroutine = StartCoroutine(PatienceCheck()); }
}