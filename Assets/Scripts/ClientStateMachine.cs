using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ClientStateMachine : MonoBehaviour
{
    private ClientPathfinding parent;
    private AgentMover agentMover;
    private CharacterStateLogger logger;

    [Header("Настройки")]
    [SerializeField] private float confusionWaitTime = 9f;
    [SerializeField] private float maxPaymentDistance = 3f;
    [SerializeField] private float zonePatienceTime = 15f;

    private ClientState currentState = ClientState.Spawning;
    private Waypoint currentGoal, previousGoal;
    private Coroutine mainActionCoroutine, zonePatienceCoroutine, millingCoroutine;

    private Transform seatTarget;

    public void Initialize(ClientPathfinding p) { parent = p; agentMover = GetComponent<AgentMover>(); logger = GetComponent<CharacterStateLogger>(); if (parent.movement == null || agentMover == null) { enabled = false; return; } StartCoroutine(MainLogicLoop()); }

    public void StopAllActionCoroutines() { if (mainActionCoroutine != null) StopCoroutine(mainActionCoroutine); mainActionCoroutine = null; if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine); zonePatienceCoroutine = null; if (millingCoroutine != null) StopCoroutine(millingCoroutine); millingCoroutine = null; }

    public void GetCalledToRegistrar() { StopAllActionCoroutines(); ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); SetGoal(ClientSpawner.GetRegistrationZone().insideWaypoints[0]); SetState(ClientState.MovingToGoal); }
    public void DecideToVisitToilet() { if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea) { StopAllActionCoroutines(); ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); previousGoal = currentGoal; SetGoal(ClientSpawner.GetToiletZone().waitingWaypoint); SetState(ClientState.MovingToGoal); } }
    public void GoToSeat(Transform seat) { StopAllActionCoroutines(); seatTarget = seat; SetState(ClientState.MovingToSeat); }

    public IEnumerator MainLogicLoop() { yield return new WaitForSeconds(0.1f); while (true) { if (parent.notification != null) parent.notification.UpdateNotification(); if (mainActionCoroutine == null) { mainActionCoroutine = StartCoroutine(HandleCurrentState()); } yield return null; } }

    private IEnumerator HandleCurrentState()
    {
        switch (currentState)
        {
            case ClientState.Spawning:
                Waypoint initialGoal = ClientQueueManager.Instance.ChooseNewGoal(parent);
                if(initialGoal != null) { SetGoal(initialGoal); SetState(ClientState.MovingToGoal); }
                else { Debug.LogError($"Не удалось найти начальную цель для {gameObject.name}. Зона ожидания настроена?"); SetState(ClientState.Confused); }
                break;

            case ClientState.MovingToGoal: case ClientState.GoingToCashier: case ClientState.Leaving: case ClientState.LeavingUpset:
                yield return StartCoroutine(MoveToGoalRoutine());
                if (currentState == ClientState.MovingToGoal) { HandleArrivalAfterMove(); }
                else if (currentState == ClientState.GoingToCashier) { SetState(ClientState.AtCashier); }
                else if (currentState == ClientState.Leaving || currentState == ClientState.LeavingUpset) { parent.OnClientExit(); }
                break;

            case ClientState.MovingToRegistrarImpolite:
                yield return StartCoroutine(MoveToGoalRoutine());
                if (currentState == ClientState.MovingToRegistrarImpolite) { yield return StartCoroutine(HandleImpoliteArrival()); }
                break;

            case ClientState.MovingToSeat: if (seatTarget != null) { parent.movement.StartStuckCheck(); agentMover.SetPath(BuildPathTo(seatTarget.position)); yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck(); } SetState(ClientState.SittingInWaitingArea); break;
            
            case ClientState.AtLimitedZoneEntrance: 
                LimitedCapacityZone zone = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(); 
                if (zone != null) { yield return StartCoroutine(EnterZoneRoutine(zone)); } 
                else { SetState(ClientState.Confused); } 
                break;

            case ClientState.InsideLimitedZone:
                yield return StartCoroutine(ServiceRoutineInsideZone());
                break;

            case ClientState.SittingInWaitingArea:
                 ClientQueueManager.Instance.StartPatienceTimer(parent);
                 yield return new WaitUntil(() => currentState != ClientState.SittingInWaitingArea);
                 break;
            case ClientState.AtWaitingArea:
                millingCoroutine = StartCoroutine(MillAroundAndWaitForSeat());
                yield return new WaitUntil(() => currentState != ClientState.AtWaitingArea);
                break;
                
            case ClientState.AtDesk1: yield return StartCoroutine(OfficeServiceRoutine(ClientSpawner.GetDesk1Zone())); break;
            case ClientState.AtDesk2: yield return StartCoroutine(OfficeServiceRoutine(ClientSpawner.GetDesk2Zone())); break;
            case ClientState.AtCashier: yield return StartCoroutine(PayBillRoutine()); break;

            case ClientState.AtRegistration: yield return StartCoroutine(RegistrationServiceRoutine()); break;
            case ClientState.Confused: yield return StartCoroutine(ConfusedRoutine()); break;
            case ClientState.PassedRegistration: yield return StartCoroutine(PassedRegistrationRoutine()); break;
            default: yield return null; break;
        }
        mainActionCoroutine = null;
    }

    private IEnumerator MoveToGoalRoutine() { if(currentGoal == null) { SetState(ClientState.Confused); yield break; } parent.movement.StartStuckCheck(); agentMover.SetPath(BuildPathTo(currentGoal.transform.position)); yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck(); }

    private void HandleArrivalAfterMove()
    {
        Waypoint dest = GetCurrentGoal();
        if (dest == null) { SetState(ClientState.Confused); return; }
        var parentZone = dest.GetComponentInParent<LimitedCapacityZone>();
        if (parentZone != null && ClientQueueManager.Instance.mainWaitingZone != null && parentZone.gameObject == ClientQueueManager.Instance.mainWaitingZone.gameObject) { ClientQueueManager.Instance.JoinQueue(parent); if (ClientQueueManager.Instance.FindSeatForClient(parent) is Transform seat) { GoToSeat(seat); } else { SetState(ClientState.AtWaitingArea); } return; }
        if (ClientSpawner.Instance.formTable != null && dest == ClientSpawner.Instance.formTable.tableWaypoint) { StartCoroutine(GetFormFromTableRoutine()); return; }
        if (parentZone != null && parentZone == ClientSpawner.GetRegistrationZone()) { SetState(ClientState.AtRegistration); return; }
        if (parentZone != null && dest.gameObject == parentZone.waitingWaypoint.gameObject) { SetState(ClientState.AtLimitedZoneEntrance); return; }
        SetState(ClientState.Confused);
    }
    
    private IEnumerator GetFormFromTableRoutine() { yield return new WaitForSeconds(1.5f); if (Random.value < 0.5f) { parent.docHolder.SetDocument(DocumentType.Form1); } else { parent.docHolder.SetDocument(DocumentType.Form2); } SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); }
    private IEnumerator RegistrationServiceRoutine() { yield return new WaitForSeconds(Random.Range(2f, 4f)); LimitedCapacityZone zone = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(); if (zone != null) { zone.ReleaseWaypoint(GetCurrentGoal()); } ClientQueueManager.Instance.ReleaseRegistrar(); SetState(ClientState.PassedRegistration); }
    private IEnumerator PassedRegistrationRoutine() { DocumentType docType = parent.docHolder.GetCurrentDocumentType(); if (docType == DocumentType.Form1) { SetGoal(ClientSpawner.GetDesk1Zone().waitingWaypoint); SetState(ClientState.MovingToGoal); } else if (docType == DocumentType.Form2) { SetGoal(ClientSpawner.GetDesk2Zone().waitingWaypoint); SetState(ClientState.MovingToGoal); } else if (docType == DocumentType.Certificate1 || docType == DocumentType.Certificate2) { SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint); SetState(ClientState.MovingToGoal); } else { parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; SetGoal(ClientSpawner.Instance.exitWaypoint); SetState(ClientState.Leaving); } yield return null; }
    
    private IEnumerator OfficeServiceRoutine(LimitedCapacityZone zone) { SetGoal(zone.waitingWaypoint); SetState(ClientState.MovingToGoal); yield return new WaitUntil(() => currentState != ClientState.MovingToGoal); if (currentState != ClientState.AtLimitedZoneEntrance) yield break; yield return StartCoroutine(EnterZoneRoutine(zone)); if (currentState != ClientState.InsideLimitedZone) yield break; }
    
    private IEnumerator ServiceRoutineInsideZone()
    {
        yield return StartCoroutine(MoveToGoalRoutine());
        LimitedCapacityZone currentZone = GetCurrentGoal().GetComponentInParent<LimitedCapacityZone>();

        if (currentZone == ClientSpawner.GetDesk1Zone() || currentZone == ClientSpawner.GetDesk2Zone())
        {
            int deskId = (currentZone == ClientSpawner.GetDesk1Zone()) ? 1 : 2;
            ClerkController clerk = ClientSpawner.GetClerkAtDesk(deskId);
            
            // --- ПОЛЁТ К КЛЕРКУ ---
            DocumentType docTypeInHand = parent.docHolder.GetCurrentDocumentType();
            GameObject prefabToFly = parent.docHolder.GetPrefabForType(docTypeInHand);
            parent.docHolder.SetDocument(DocumentType.None);
            
            bool transferToClerkComplete = false;
            if (prefabToFly != null && clerk != null && clerk.assignedServicePoint != null)
            {
                GameObject flyingDoc = Instantiate(prefabToFly, parent.docHolder.handPoint.position, Quaternion.identity);
                DocumentMover mover = flyingDoc.GetComponent<DocumentMover>();
                if (mover != null)
                {
                    mover.StartMove(clerk.assignedServicePoint.documentPointOnDesk, () => { transferToClerkComplete = true; });
                    yield return new WaitUntil(() => transferToClerkComplete);
                }
            }

            // Клерк "работает"
            if(parent.helpedByInternSound != null) AudioSource.PlayClipAtPoint(parent.helpedByInternSound, transform.position);
            yield return new WaitForSeconds(Random.Range(4f, 7f));
            
            // Решаем, какой документ выдать
            float choice = Random.value;
            DocumentType newDocType;
            if (choice < 0.8f) { newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2; parent.billToPay += 100; }
            else { newDocType = (deskId == 1) ? DocumentType.Form2 : DocumentType.Form1; }

            // --- ПОЛЁТ ОБРАТНО К КЛИЕНТУ ---
            GameObject newDocPrefab = parent.docHolder.GetPrefabForType(newDocType);
            bool transferToClientComplete = false;
            if (newDocPrefab != null && clerk != null && clerk.assignedServicePoint != null)
            {
                GameObject flyingDocBack = Instantiate(newDocPrefab, clerk.assignedServicePoint.documentPointOnDesk.position, Quaternion.identity);
                DocumentMover mover = flyingDocBack.GetComponent<DocumentMover>();
                if (mover != null)
                {
                    mover.StartMove(parent.docHolder.handPoint, () => 
                    {
                        parent.docHolder.ReceiveTransferredDocument(newDocType, flyingDocBack);
                        transferToClientComplete = true; 
                    });
                    yield return new WaitUntil(() => transferToClientComplete);
                }
            }
            
            currentZone.ReleaseWaypoint(GetCurrentGoal());
            SetState(ClientState.PassedRegistration);
        }
        else if (currentZone == ClientSpawner.GetCashierZone()) { yield return StartCoroutine(PayBillRoutine()); }
        else if (currentZone == ClientSpawner.GetToiletZone()) { yield return new WaitForSeconds(Random.Range(5f, 10f)); currentZone.ReleaseWaypoint(GetCurrentGoal()); SetGoal(ClientQueueManager.Instance.GetToiletReturnGoal(parent)); if (GetCurrentGoal() == ClientSpawner.Instance.exitWaypoint) SetState(ClientState.Leaving); else SetState(ClientState.MovingToGoal); }
    }

    private IEnumerator HandleImpoliteArrival() { agentMover.Stop(); LimitedCapacityZone zone = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(); if (zone == null) { SetState(ClientState.Confused); yield break; } float choice = Random.value; if (choice < 0.05f) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; SetState(ClientState.LeavingUpset); } else if (choice < 0.40f) { zone.JumpQueue(parent.gameObject); SetGoal(zone.waitingWaypoint); SetState(ClientState.AtLimitedZoneEntrance); } else { ClientQueueManager.Instance.JoinQueue(parent); SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); } }
    private IEnumerator EnterZoneRoutine(LimitedCapacityZone zone) { agentMover.Stop(); zone.JoinQueue(parent.gameObject); zonePatienceCoroutine = StartCoroutine(PatienceMonitorForZone(zone)); yield return new WaitUntil(() => zone.IsFirstInQueue(parent.gameObject)); Waypoint freeSpot = null; while (freeSpot == null) { if (currentState != ClientState.AtLimitedZoneEntrance) yield break; if(zone == null) { SetState(ClientState.Confused); yield break; } freeSpot = zone.RequestAndOccupyWaypoint(); if (freeSpot == null) { yield return new WaitForSeconds(0.5f); } } if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine); zonePatienceCoroutine = null; SetGoal(freeSpot); SetState(ClientState.InsideLimitedZone); }
    private IEnumerator PatienceMonitorForZone(LimitedCapacityZone zone) { yield return new WaitForSeconds(zonePatienceTime); if (currentState == ClientState.AtLimitedZoneEntrance) { zone.LeaveQueue(parent.gameObject); parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; SetState(ClientState.LeavingUpset); } }
    private IEnumerator PayBillRoutine() { agentMover.Stop(); ClerkController cashier = null; while(cashier == null || cashier.IsOnBreak() || Vector2.Distance(transform.position, cashier.transform.position) > maxPaymentDistance) { cashier = ClientSpawner.GetClerkAtDesk(3); if (cashier == null || cashier.IsOnBreak() || Vector2.Distance(transform.position, cashier.transform.position) > maxPaymentDistance) { yield return new WaitForSeconds(1f); } } if (parent.moneyPrefab != null) { int numberOfBanknotes = 5; float delayBetweenSpawns = 0.15f; for (int i = 0; i < numberOfBanknotes; i++) { Vector2 spawnPosition = (Vector2)transform.position + (Random.insideUnitCircle * 0.2f); GameObject moneyInstance = Instantiate(parent.moneyPrefab, spawnPosition, Quaternion.identity); MoneyMover mover = moneyInstance.GetComponent<MoneyMover>(); Transform moneyTarget = (cashier.assignedServicePoint != null) ? cashier.assignedServicePoint.documentPointOnDesk : cashier.transform; if (mover != null) { mover.StartMove(moneyTarget); } yield return new WaitForSeconds(delayBetweenSpawns); } } if (PlayerWallet.Instance != null && parent != null) { PlayerWallet.Instance.AddMoney(parent.billToPay, transform.position); if (parent.paymentSound != null) AudioSource.PlayClipAtPoint(parent.paymentSound, transform.position); parent.billToPay = 0; } parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>().ReleaseWaypoint(GetCurrentGoal()); SetGoal(ClientSpawner.Instance.exitWaypoint); SetState(ClientState.Leaving); }
    private IEnumerator ConfusedRoutine() { agentMover.Stop(); yield return new WaitForSeconds(confusionWaitTime); SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); previousGoal = null; SetState(ClientState.MovingToGoal); }
    private IEnumerator MillAroundAndWaitForSeat() { ClientQueueManager.Instance.StartPatienceTimer(parent); while (currentState == ClientState.AtWaitingArea) { Transform freeSeat = ClientQueueManager.Instance.FindSeatForClient(parent); if (freeSeat != null) { GoToSeat(freeSeat); yield break; } Waypoint randomStandingPoint = ClientQueueManager.Instance.ChooseNewGoal(parent); if(randomStandingPoint != null) { SetGoal(randomStandingPoint); yield return StartCoroutine(MoveToGoalRoutine()); } yield return new WaitForSeconds(Random.Range(2f, 4f)); } }
    public void GetHelpFromIntern(Waypoint newGoal = null) { if (parent.helpedByInternSound != null) { AudioSource.PlayClipAtPoint(parent.helpedByInternSound, transform.position); } if(currentState == ClientState.Confused) { } else { StopAllActionCoroutines(); if(newGoal != null) { if (newGoal == ClientSpawner.Instance.exitWaypoint) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Normal; SetState(ClientState.Leaving); } else { SetState(ClientState.MovingToGoal); } ClientQueueManager.Instance.RemoveClientFromQueue(parent); SetGoal(newGoal); } } }
    public void SetState(ClientState state) { if (state == currentState) return; if (state == ClientState.Confused && currentState == ClientState.Enraged) return; if (currentState == ClientState.AtLimitedZoneEntrance && GetCurrentGoal() != null) { LimitedCapacityZone zone = GetCurrentGoal().GetComponentInParent<LimitedCapacityZone>(); if (zone != null) { zone.LeaveQueue(parent.gameObject); } } StopAllActionCoroutines(); agentMover?.Stop(); if (state == ClientState.Confused) { previousGoal = currentGoal; if (parent.confusedSound != null) AudioSource.PlayClipAtPoint(parent.confusedSound, transform.position); } currentState = state; logger?.LogState(GetStatusInfo()); }
    public Queue<Waypoint> BuildPathTo(Vector2 targetPos) { var newPath = new Queue<Waypoint>(); Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); if (allWaypoints.Length == 0) return newPath; Waypoint start = FindNearestVisibleWaypoint(transform.position, allWaypoints); Waypoint goal = FindNearestVisibleWaypoint(targetPos, allWaypoints); if (start == null) { start = FindNearestWaypoint(transform.position, allWaypoints); } if (goal == null) { goal = FindNearestWaypoint(targetPos, allWaypoints); } if (start == null || goal == null) { SetState(ClientState.Confused); return newPath; } Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>(); PriorityQueue<Waypoint, float> queue = new PriorityQueue<Waypoint, float>(); foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; } distances[start] = 0; queue.Enqueue(start, 0); while (queue.Count > 0) { Waypoint current = queue.Dequeue(); if (current == goal) { ReconstructPath(previous, goal, newPath); return newPath; } if (current.neighbors == null || current.neighbors.Count == 0) continue; foreach (var neighbor in current.neighbors) { if (neighbor == null) continue; if (neighbor.type == Waypoint.WaypointType.StaffOnly && neighbor != ClientSpawner.Instance.exitWaypoint) continue; float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position); if (distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { distances[neighbor] = newDist; previous[neighbor] = current; queue.Enqueue(neighbor, newDist); } } } SetState(ClientState.Confused); return newPath; }
    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path) { List<Waypoint> pathList = new List<Waypoint>(); for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } pathList.Reverse(); path.Clear(); foreach (var wp in pathList) { path.Enqueue(wp); } }
    private Waypoint FindNearestVisibleWaypoint(Vector2 position, Waypoint[] wps) { if(wps == null || wps.Length == 0) return null; Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in wps) { if (wp.type == Waypoint.WaypointType.StaffOnly) continue; float distance = Vector2.Distance(position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles")); if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint; }
    private Waypoint FindNearestWaypoint(Vector2 p, Waypoint[] wps) { if(wps == null || wps.Length == 0) return null; return wps.Where(wp => wp.type != Waypoint.WaypointType.StaffOnly).OrderBy(wp => Vector2.Distance(p, wp.transform.position)).FirstOrDefault(); }
    public ClientState GetCurrentState() => currentState; public Waypoint GetCurrentGoal() => currentGoal; public void SetGoal(Waypoint g) => currentGoal = g;
    public string GetStatusInfo() { string statusText = ""; string goalName = (currentGoal != null) ? currentGoal.name : (seatTarget != null ? seatTarget.name : "неизвестно"); switch (currentState) { case ClientState.MovingToGoal: case ClientState.MovingToRegistrarImpolite: case ClientState.GoingToCashier: case ClientState.Leaving: case ClientState.LeavingUpset: statusText = $"Идет к: {goalName}"; break; case ClientState.MovingToSeat: statusText = $"Идет на место: {seatTarget.name}"; break; case ClientState.AtWaitingArea: statusText = "Ожидает стоя"; break; case ClientState.SittingInWaitingArea: statusText = "Ожидает сидя"; break; case ClientState.AtRegistration: statusText = "Обслуживается в регистратуре"; break; case ClientState.AtDesk1: statusText = "Обслуживается у Стойки 1"; break; case ClientState.AtDesk2: statusText = "Обслуживается у Стойки 2"; break; case ClientState.InsideLimitedZone: statusText = $"В зоне: {currentGoal?.GetComponentInParent<LimitedCapacityZone>()?.name}"; break; case ClientState.Enraged: statusText = "В ЯРОСТИ!"; break; case ClientState.AtCashier: statusText = "Оплачивает"; break; case ClientState.AtLimitedZoneEntrance: statusText = $"Ждет входа в зону: {currentGoal?.GetComponentInParent<LimitedCapacityZone>()?.name}"; break; default: statusText = currentState.ToString(); break; } if (parent != null && parent.docHolder != null) { DocumentType docType = parent.docHolder.GetCurrentDocumentType(); if (docType != DocumentType.None) { statusText += $" (несет {docType})"; } } return statusText; }
    private class PriorityQueue<T, U> where U : System.IComparable<U> { private SortedDictionary<U, Queue<T>> dictionary = new SortedDictionary<U, Queue<T>>(); public int Count => dictionary.Sum(p => p.Value.Count); public void Enqueue(T item, U priority) { if (!dictionary.ContainsKey(priority)) { dictionary[priority] = new Queue<T>(); } dictionary[priority].Enqueue(item); } public T Dequeue() { var pair = dictionary.First(); T item = pair.Value.Dequeue(); if (pair.Value.Count == 0) { dictionary.Remove(pair.Key); } return item; } }
}