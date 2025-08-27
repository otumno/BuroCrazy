using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ClientStateMachine : MonoBehaviour
{
    private ClientPathfinding parent;
    private float baseMoveSpeed;
    [Header("Настройки потеряшки")]
    [SerializeField] private float confusionWaitTime = 9f;
    [SerializeField] [Range(0f, 1f)] private float forgetfulnessChancePerWaypoint = 0.15f;
    
    [Header("Настройки поведения в зонах")]
    [SerializeField] private float personalSpaceRadius = 0.8f;
    [SerializeField] private float repositionSpeedMultiplier = 0.4f;
    [SerializeField] private float maxPaymentDistance = 3f; 
    [SerializeField] private float zonePatienceTime = 15f; 

    private Queue<Waypoint> path = new Queue<Waypoint>();
    private ClientState currentState = ClientState.Spawning;
    private Waypoint currentGoal, previousGoal;
    private Coroutine personalSpaceCoroutine, totalPatienceCoroutine, zonePatienceCoroutine;
    private Coroutine actionCoroutine;
    private Transform seatTarget;
    private bool isConfusionInterrupted = false;

    public void Initialize(ClientPathfinding p) { parent = p; if (p.movement == null) return; baseMoveSpeed = p.movement.moveSpeed; if (p.queueManager != null) StartCoroutine(MainLogicLoop()); }
    
    private void StopAllWaitingCoroutines() { StopPersonalSpaceCheck(); StopTotalPatienceMonitor(); if (totalPatienceCoroutine != null) StopCoroutine(totalPatienceCoroutine); totalPatienceCoroutine = null; if(actionCoroutine != null) StopCoroutine(actionCoroutine); actionCoroutine = null; if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine); zonePatienceCoroutine = null; }
    private void StopPersonalSpaceCheck() { if (personalSpaceCoroutine != null) StopCoroutine(personalSpaceCoroutine); personalSpaceCoroutine = null; }
    private void StopTotalPatienceMonitor() { if (totalPatienceCoroutine != null) StopCoroutine(totalPatienceCoroutine); totalPatienceCoroutine = null; }

    public void GoToSeat(Transform seat) { StopAllWaitingCoroutines(); if(actionCoroutine != null) StopCoroutine(actionCoroutine); seatTarget = seat; SetState(ClientState.MovingToSeat); }
    public void MillAroundWaitingZone() { SetState(ClientState.AtWaitingArea); }
    public void GetCalledToRegistrar() { StopAllWaitingCoroutines(); parent.queueManager.OnClientLeavesWaitingZone(parent); SetGoal(parent.queueManager.GetRegistrationZone().GetComponent<Waypoint>()); SetState(ClientState.MovingToGoal); RecalculatePath(); }
    public void DecideToVisitToilet() { if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea) { StopAllWaitingCoroutines(); parent.queueManager.OnClientLeavesWaitingZone(parent); parent.movement.SetLinearDrag(1f); previousGoal = parent.queueManager.GetWaitingZone().GetComponent<Waypoint>(); SetGoal(ClientSpawner.GetToiletZone().waitingWaypoint); SetState(ClientState.MovingToGoal); RecalculatePath(); } }
    
    public IEnumerator MainLogicLoop() { yield return new WaitForSeconds(0.1f); if(!ClientQueueManager.IsRegistrarBusy()) ClientQueueManager.CallNextClient(); while (true) { if (parent.notification != null) parent.notification.UpdateNotification(); switch (currentState) { case ClientState.Spawning: if (parent.queueManager != null) parent.queueManager.AssignInitialGoal(); break; case ClientState.MovingToGoal: case ClientState.MovingToRegistrarImpolite: if (path == null || path.Count == 0) { SetState(ClientState.Confused); break; } parent.movement.StartStuckCheck(); yield return StartCoroutine(FollowPath()); parent.movement.StopStuckCheck(); parent.movement.SetVelocity(Vector2.zero); if (currentState == ClientState.Confused) break; Waypoint dest = GetCurrentGoal(); if (dest != null) { if (dest.gameObject == parent.queueManager.GetWaitingZone()) { parent.queueManager.JoinQueue(parent); Transform seat = parent.queueManager.FindSeatForClient(parent); if (seat != null) { GoToSeat(seat); } else { MillAroundWaitingZone(); } } else if (dest == parent.queueManager.GetDesk1Waypoint()) { SetState(ClientState.AtDesk1); } else if (dest == parent.queueManager.GetDesk2Waypoint()) { SetState(ClientState.AtDesk2); } else if (dest.gameObject == parent.queueManager.GetRegistrationZone()) { SetState(ClientState.AtRegistration); } else if (dest.gameObject == parent.queueManager.GetToiletZone()) { previousGoal = GetCurrentGoal(); SetState(ClientState.AtToilet); } else { LimitedCapacityZone lz = dest.GetComponentInParent<LimitedCapacityZone>(); if (lz != null && dest == lz.waitingWaypoint) SetState(ClientState.AtLimitedZoneEntrance); else SetState(ClientState.Confused); } } else SetState(ClientState.Confused); break; case ClientState.MovingToSeat: if (seatTarget == null) { SetState(ClientState.Confused); break; } parent.movement.StartStuckCheck(); yield return StartCoroutine(MoveDirectlyToPosition(seatTarget.position)); parent.movement.StopStuckCheck(); SetState(ClientState.SittingInWaitingArea); break; 
        
        case ClientState.AtLimitedZoneEntrance: 
            LimitedCapacityZone zone = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(); 
            if (zone != null) { yield return StartCoroutine(EnterZoneRoutine(zone)); } 
            else { SetState(ClientState.Confused); }
            break; 
        
        case ClientState.InsideLimitedZone: 
            yield return StartCoroutine(FollowPath()); 
            parent.movement.SetLinearDrag(10f); 
            personalSpaceCoroutine = StartCoroutine(ManagePositionInZone(GetCurrentGoal().gameObject)); 
            yield return new WaitForSeconds(Random.Range(5f, 10f)); 
            StopPersonalSpaceCheck(); 
            parent.movement.SetLinearDrag(1f); 
            LimitedCapacityZone cz = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(); 
            if(cz != null) cz.ReleaseWaypoint(GetCurrentGoal());
            
            SetGoal(parent.queueManager.GetToiletReturnGoal(previousGoal)); 
            if (GetCurrentGoal() == parent.queueManager.GetExitWaypoint()) SetState(ClientState.Leaving); 
            else SetState(ClientState.MovingToGoal); 
            RecalculatePath();
            break; 
        
        case ClientState.AtWaitingArea: if(personalSpaceCoroutine == null) personalSpaceCoroutine = StartCoroutine(ManagePositionInZone(parent.queueManager.GetWaitingZone())); if(totalPatienceCoroutine == null) { parent.queueManager.StartPatienceTimer(); totalPatienceCoroutine = StartCoroutine(TotalPatienceMonitor()); } yield return new WaitUntil(() => currentState != ClientState.AtWaitingArea); StopAllWaitingCoroutines(); break; case ClientState.SittingInWaitingArea: parent.movement.SetLinearDrag(10f); if(totalPatienceCoroutine == null) { parent.queueManager.StartPatienceTimer(); totalPatienceCoroutine = StartCoroutine(TotalPatienceMonitor()); } yield return new WaitUntil(() => currentState != ClientState.SittingInWaitingArea); StopAllWaitingCoroutines(); parent.movement.SetLinearDrag(1f); break; case ClientState.AtDesk1: parent.movement.SetLinearDrag(10f); yield return StartCoroutine(ServiceAtDesk(1, 100)); parent.movement.SetLinearDrag(1f); break; case ClientState.AtDesk2: parent.movement.SetLinearDrag(10f); yield return StartCoroutine(ServiceAtDesk(2, 100)); parent.movement.SetLinearDrag(1f); break; case ClientState.AtRegistration: parent.movement.SetLinearDrag(10f); yield return StartCoroutine(ServiceAtTriage(50)); parent.movement.SetLinearDrag(1f); break; case ClientState.ReturningToWait: SetState(ClientState.MovingToGoal); RecalculatePath(); break; case ClientState.AtToilet: parent.movement.SetLinearDrag(10f); if (parent.toiletSound != null) AudioSource.PlayClipAtPoint(parent.toiletSound, transform.position); personalSpaceCoroutine = StartCoroutine(ManagePositionInZone(parent.queueManager.GetToiletZone())); yield return new WaitForSeconds(Random.Range(parent.queueManager.minWaitTime, parent.queueManager.maxWaitTime)); StopPersonalSpaceCheck(); parent.movement.SetLinearDrag(1f); SetGoal(parent.queueManager.GetToiletReturnGoal(previousGoal)); if (GetCurrentGoal() == parent.queueManager.GetExitWaypoint()) SetState(ClientState.Leaving); else SetState(ClientState.MovingToGoal); RecalculatePath(); break; case ClientState.Enraged: yield return StartCoroutine(EnragedBehavior()); break; 
        
        case ClientState.Leaving: 
        case ClientState.LeavingUpset:
            if (parent.reasonForLeaving == ClientPathfinding.LeaveReason.CalmedDown) { parent.movement.moveSpeed = parent.movement.moveSpeed * 1.5f; } if (parent.movement != null) parent.movement.SetColliderRadius(0.07f); SetGoal(parent.queueManager.GetExitWaypoint()); RecalculatePath(); yield return StartCoroutine(FollowPath()); parent.OnClientExit(); break; 
        
        case ClientState.Confused: if (parent.movement != null) parent.movement.SetColliderRadius(parent.movement.GetBaseColliderRadius()); yield return StartCoroutine(ConfusedBehavior(confusionWaitTime)); if (!isConfusionInterrupted) { if (previousGoal != null && Random.value < 0.85f) SetGoal(previousGoal); else SetGoal(parent.queueManager.ChooseNewGoal(currentState)); } isConfusionInterrupted = false; previousGoal = null; if (GetCurrentGoal() == parent.queueManager.GetExitWaypoint()) SetState(ClientState.Leaving); else SetState(ClientState.MovingToGoal); RecalculatePath(); break; case ClientState.PassedRegistration: if (parent.billToPay > 0 && parent.queueManager.GetCashierWaypoint() != null) { SetState(ClientState.GoingToCashier); Waypoint cashierWp = parent.queueManager.GetCashierWaypoint(); if (cashierWp != null) { SetGoal(cashierWp); RecalculatePath(); } else { SetState(ClientState.Confused); } } else { if (parent.successfulExitSound != null) AudioSource.PlayClipAtPoint(parent.successfulExitSound, transform.position); parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; yield return new WaitForSeconds(1f); SetGoal(parent.queueManager.GetExitWaypoint()); SetState(ClientState.Leaving); RecalculatePath(); } break; case ClientState.GoingToCashier: if (path == null || path.Count == 0) { SetState(ClientState.Confused); break; } parent.movement.StartStuckCheck(); yield return StartCoroutine(FollowPath()); parent.movement.StopStuckCheck(); SetState(ClientState.AtCashier); break; case ClientState.AtCashier: parent.movement.SetLinearDrag(10f); yield return StartCoroutine(PayBillRoutine()); parent.movement.SetLinearDrag(1f); break; } yield return null; } }

    private IEnumerator EnterZoneRoutine(LimitedCapacityZone zone)
    {
        parent.movement.SetVelocity(Vector2.zero);
        zone.JoinQueue(parent.gameObject);
        zonePatienceCoroutine = StartCoroutine(PatienceMonitorForZone(zone));

        yield return new WaitUntil(() => zone.IsFirstInQueue(parent.gameObject));

        Waypoint freeSpot = null;
        while (freeSpot == null)
        {
            if (currentState != ClientState.AtLimitedZoneEntrance) yield break;
            
            if(zone == null)
            {
                SetState(ClientState.Confused);
                yield break;
            }

            freeSpot = zone.RequestAndOccupyWaypoint();
            if (freeSpot == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine);
        zonePatienceCoroutine = null;

        SetGoal(freeSpot);
        RecalculatePath();
        SetState(ClientState.InsideLimitedZone);
    }
    
    private IEnumerator PatienceMonitorForZone(LimitedCapacityZone zone) { yield return new WaitForSeconds(zonePatienceTime); if (currentState == ClientState.AtLimitedZoneEntrance) { zone.LeaveQueue(parent.gameObject); parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; SetState(ClientState.LeavingUpset); } }
    private IEnumerator EnragedBehavior() { parent.movement.moveSpeed *= Random.Range(2f, 2.5f); parent.movement.StartStuckCheck(); while(currentState == ClientState.Enraged) { if (path == null || path.Count == 0) { SetGoal(parent.queueManager.GetRandomWaypoint_NoExit()); RecalculatePath(); } if (path.Count > 0) { yield return StartCoroutine(FollowPath()); } else { yield return new WaitForSeconds(0.5f); } } parent.movement.StopStuckCheck(); }
    private IEnumerator TotalPatienceMonitor() { yield return new WaitForSeconds(parent.totalPatienceTime); if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Angry; parent.queueManager.RemoveClientFromQueue(parent); ClientQueueManager.dissatisfiedClients.Add(parent); if (parent.dissatisfiedExitSound != null) AudioSource.PlayClipAtPoint(parent.dissatisfiedExitSound, transform.position); SetState(ClientState.Enraged); } }
    private IEnumerator FollowPath() { while (path.Count > 0) { if (parent.movement != null) { Waypoint targetWaypoint = path.Peek(); float distance = Vector2.Distance(transform.position, targetWaypoint.transform.position); if (distance < parent.movement.stoppingDistance) { path.Dequeue(); if (currentState != ClientState.Leaving && currentState != ClientState.LeavingUpset && currentState != ClientState.Enraged && Random.value < forgetfulnessChancePerWaypoint) { SetState(ClientState.Confused); yield break; } if (path.Count == 0) break; } float currentSpeed = parent.movement.moveSpeed; if (path.Count == 1 && distance < parent.movement.stoppingDistance * 2f) { currentSpeed *= Mathf.Clamp01(distance / (parent.movement.stoppingDistance * 1.5f)); } parent.movement.SetVelocity(((Vector2)targetWaypoint.transform.position - (Vector2)transform.position).normalized * currentSpeed); } yield return null; } if (parent.movement != null) parent.movement.SetVelocity(Vector2.zero); }
    private IEnumerator ConfusedBehavior(float duration) { float endTime = Time.time + duration; SpriteRenderer sr = parent.GetComponentInChildren<SpriteRenderer>(); while (Time.time < endTime && !isConfusionInterrupted) { Vector2 randomPoint = (Vector2)transform.position + Random.insideUnitCircle * 0.5f; float journeyTime = 1f; float elapsedTime = 0f; Vector2 startPos = transform.position; while(elapsedTime < journeyTime && !isConfusionInterrupted) { if (parent.movement == null) yield break; parent.movement.SetVelocity(((randomPoint - startPos) / journeyTime) * 0.5f); elapsedTime += Time.deltaTime; yield return null; } if(sr != null) sr.flipX = !sr.flipX; yield return new WaitForSeconds(Random.Range(0.5f, 1f)); } if (parent.movement != null) parent.movement.SetVelocity(Vector2.zero); }
    private IEnumerator ServiceAtTriage(int cost) { bool hasNumber = parent.queueManager.HasQueueNumber(); if (!hasNumber) { if (ClientQueueManager.IsRegistrarBusy()) { parent.queueManager.JoinQueue(parent); SetGoal(parent.queueManager.GetWaitingWaypoint()); SetState(ClientState.ReturningToWait); yield break; } if (Random.value < 0.80f) { parent.queueManager.JoinQueue(parent); SetGoal(parent.queueManager.GetWaitingWaypoint()); SetState(ClientState.ReturningToWait); yield break; } } else { if (ClientQueueManager.IsRegistrarBusyWithAnother(parent)) { yield return new WaitForSeconds(Random.Range(2f, 5f)); if (ClientQueueManager.IsRegistrarBusyWithAnother(parent)) SetGoal(parent.queueManager.GetWaitingWaypoint()); SetState(ClientState.ReturningToWait); yield break; } } yield return new WaitForSeconds(Random.Range(parent.queueManager.minWaitTime, parent.queueManager.maxWaitTime)); if (parent.successfulExitSound != null) AudioSource.PlayClipAtPoint(parent.successfulExitSound, transform.position); parent.billToPay += cost; ClientQueueManager.ReleaseRegistrar(); Waypoint nextGoal = null; float choice = Random.value; if (choice < 0.40f && ClientQueueManager.IsDesk1Available()) nextGoal = parent.queueManager.GetDesk1Waypoint(); else if (choice < 0.80f && ClientQueueManager.IsDesk2Available()) nextGoal = parent.queueManager.GetDesk2Waypoint(); else if (choice < 0.90f) { nextGoal = ClientSpawner.GetToiletZone().waitingWaypoint; } else { SetState(ClientState.PassedRegistration); yield break; } parent.queueManager.OnClientServedFromRegistrar(); SetGoal(nextGoal); SetState(ClientState.MovingToGoal); RecalculatePath(); }
    private IEnumerator ServiceAtDesk(int deskNum, int cost) { if (!parent.queueManager.HasQueueNumber() && currentState == ClientState.MovingToRegistrarImpolite) { if (Random.value > 0.15f) { SetGoal(parent.queueManager.GetWaitingWaypoint()); SetState(ClientState.ReturningToWait); RecalculatePath(); yield break; } } bool isMyDeskAvailable = (deskNum == 1) ? ClientQueueManager.IsDesk1Available() : ClientQueueManager.IsDesk2Available(); if (!isMyDeskAvailable) { SetGoal(parent.queueManager.GetWaitingWaypoint()); SetState(ClientState.ReturningToWait); RecalculatePath(); yield break; } yield return new WaitUntil(() => !(deskNum == 1 ? ClientQueueManager.IsDesk1Busy() : ClientQueueManager.IsDesk2Busy())); ClientQueueManager.OccupyDesk(deskNum, parent); yield return new WaitForSeconds(Random.Range(parent.queueManager.minWaitTime, parent.queueManager.maxWaitTime)); if (parent.successfulExitSound != null) AudioSource.PlayClipAtPoint(parent.successfulExitSound, transform.position); parent.billToPay += cost; float choice = Random.value; int otherDeskNum = (deskNum == 1) ? 2 : 1; bool isOtherDeskAvailable = (otherDeskNum == 1) ? ClientQueueManager.IsDesk1Available() : ClientQueueManager.IsDesk2Available(); if (choice < 0.25f && isOtherDeskAvailable) { Waypoint otherDesk = (deskNum == 1) ? parent.queueManager.GetDesk2Waypoint() : parent.queueManager.GetDesk1Waypoint(); SetGoal(otherDesk); SetState(ClientState.MovingToGoal); RecalculatePath(); } else { SetState(ClientState.PassedRegistration); } ClientQueueManager.ReleaseDesk(deskNum); }
    private IEnumerator ManagePositionInZone(GameObject zone) { float repositionSpeed = baseMoveSpeed * repositionSpeedMultiplier; Collider2D zoneCollider = zone.GetComponent<Collider2D>(); if (zoneCollider == null) yield break; Vector2 targetPosition = Vector2.zero; for (int i = 0; i < 20; i++) { Vector2 randomOffset = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (zoneCollider.bounds.size.x / 2.5f); targetPosition = (Vector2)zone.transform.position + randomOffset; if (zoneCollider.bounds.Contains(targetPosition) && Physics2D.OverlapCircle(targetPosition, personalSpaceRadius, LayerMask.GetMask("Client")) == null) break; } while (Vector2.Distance(transform.position, targetPosition) > 0.3f) { if (parent.movement == null) yield break; Vector2 direction = (targetPosition - (Vector2)transform.position).normalized; parent.movement.SetVelocity(direction * baseMoveSpeed * 0.5f); yield return null; } while (true) { ClientState s = GetCurrentState(); bool shouldAvoidOthers = (s == ClientState.AtWaitingArea || s == ClientState.AtToilet || s == ClientState.InsideLimitedZone); if (!shouldAvoidOthers) { parent.movement.SetVelocity(Vector2.zero); break; } Vector2 moveVector = Vector2.zero; if (!zoneCollider.bounds.Contains(transform.position)) { moveVector += ((Vector2)zone.transform.position - (Vector2)transform.position).normalized; } Collider2D[] neighbors = Physics2D.OverlapCircleAll(transform.position, personalSpaceRadius, LayerMask.GetMask("Client")); if (neighbors.Length > 1) { foreach (var n in neighbors) { if (n.gameObject == gameObject) continue; moveVector += ((Vector2)transform.position - (Vector2)n.transform.position).normalized; } } if (moveVector.sqrMagnitude > 0.01f) parent.movement.SetVelocity(moveVector.normalized * repositionSpeed); else parent.movement.SetVelocity(Vector2.zero); yield return new WaitForSeconds(0.2f); } yield break; }
    private IEnumerator MoveDirectlyToPosition(Vector2 target) { parent.movement.StartStuckCheck(); while (Vector2.Distance(transform.position, target) > parent.movement.stoppingDistance * 0.5f) { if(parent == null) yield break; float distance = Vector2.Distance(transform.position, target); float currentSpeed = parent.movement.moveSpeed * Mathf.Clamp(distance, 0.1f, 1f); parent.movement.SetVelocity((target - (Vector2)transform.position).normalized * currentSpeed); yield return null; } if(parent != null) { parent.movement.SetVelocity(Vector2.zero); parent.movement.StopStuckCheck(); } }
    
    private IEnumerator PayBillRoutine()
    {
        ClerkController cashier = null;
        while(cashier == null || cashier.IsOnBreak() || Vector2.Distance(transform.position, cashier.transform.position) > maxPaymentDistance)
        {
            cashier = ClientSpawner.GetClerkAtDesk(3);
            if (cashier == null || cashier.IsOnBreak() || Vector2.Distance(transform.position, cashier.transform.position) > maxPaymentDistance)
            {
                yield return new WaitForSeconds(1f);
            }
        }

        if (parent.moneyPrefab != null) { int numberOfBanknotes = 5; float delayBetweenSpawns = 0.15f; for (int i = 0; i < numberOfBanknotes; i++) { Vector2 spawnPosition = (Vector2)transform.position + (Random.insideUnitCircle * 0.2f); GameObject moneyInstance = Instantiate(parent.moneyPrefab, spawnPosition, Quaternion.identity); MoneyMover mover = moneyInstance.GetComponent<MoneyMover>(); if (mover != null) { mover.target = cashier.transform; } yield return new WaitForSeconds(delayBetweenSpawns); } } 
        if (PlayerWallet.Instance != null && parent != null) { PlayerWallet.Instance.AddMoney(parent.billToPay, transform.position); if (parent.paymentSound != null) AudioSource.PlayClipAtPoint(parent.paymentSound, transform.position); parent.billToPay = 0; } 
        
        parent.isLeavingSuccessfully = true; 
        parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; 
        SetGoal(parent.queueManager.GetExitWaypoint()); 
        SetState(ClientState.Leaving); 
        RecalculatePath();
    }

    public void GetHelpFromIntern(Waypoint newGoal = null) { if (parent.helpedByInternSound != null) { AudioSource.PlayClipAtPoint(parent.helpedByInternSound, transform.position); } if(currentState == ClientState.Confused) { isConfusionInterrupted = true; } else { StopAllWaitingCoroutines(); if(newGoal != null) { if (newGoal == parent.queueManager.GetExitWaypoint()) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Normal; SetState(ClientState.Leaving); } else { SetState(ClientState.MovingToGoal); } parent.queueManager.RemoveClientFromQueue(parent); SetGoal(newGoal); RecalculatePath(); } } }
    
    public void SetState(ClientState state) 
    { 
        if (state == ClientState.Confused && currentState == ClientState.Enraged) return; 
        if (currentState != state) 
        { 
            if(currentState == ClientState.AtLimitedZoneEntrance && state == ClientState.Confused) { Debug.LogWarning($"КЛИЕНТ {gameObject.name} стал ПОТЕРЯШКОЙ во время ожидания у зоны. ИСТОЧНИК:", this); Debug.LogWarning(new System.Diagnostics.StackTrace().ToString()); }
            if (currentState == ClientState.AtLimitedZoneEntrance && GetCurrentGoal() != null) { LimitedCapacityZone zone = GetCurrentGoal().GetComponentInParent<LimitedCapacityZone>(); if (zone != null) { zone.LeaveQueue(parent.gameObject); } } 
            if (currentState == ClientState.MovingToGoal || currentState == ClientState.Enraged || currentState == ClientState.MovingToSeat) parent.movement.StopStuckCheck(); 
            if (currentState == ClientState.Enraged) { parent.movement.moveSpeed = baseMoveSpeed; } 
            StopAllWaitingCoroutines(); 
            if (state == ClientState.Confused) { previousGoal = currentGoal; if (parent.confusedSound != null) AudioSource.PlayClipAtPoint(parent.confusedSound, transform.position); } 
            if (currentState != ClientState.Spawning) UpdateStateCounter(currentState, false); 
            UpdateStateCounter(state, true); 
            
            var logger = GetComponent<CharacterStateLogger>();
            if(logger != null) logger.LogState(state.ToString());

            currentState = state; 
        } 
    }

    public void RecalculatePath() { if (currentGoal == null || parent == null) return; path.Clear(); BuildPathToGoal(currentGoal); }
    private void BuildPathToGoal(Waypoint goal) { Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); if (allWaypoints.Length == 0) return; bool isEnragedCheck = (currentState == ClientState.Enraged); Waypoint start = FindNearestVisibleWaypoint(transform.position, allWaypoints, !isEnragedCheck); if (start == null) { start = FindNearestWaypoint(transform.position, allWaypoints); } if (start == null || goal == null) { SetState(ClientState.Confused); return; } Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>(); PriorityQueue<Waypoint, float> queue = new PriorityQueue<Waypoint, float>(); foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; } distances[start] = 0; queue.Enqueue(start, 0); while (queue.Count > 0) { Waypoint current = queue.Dequeue(); if (current == goal) { ReconstructPath(previous, goal); return; } if (current.neighbors == null || current.neighbors.Count == 0) continue; foreach (var neighbor in current.neighbors) { if (!isEnragedCheck && neighbor.type == Waypoint.WaypointType.StaffOnly && neighbor != parent.queueManager.GetExitWaypoint()) continue; float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position); if (distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { distances[neighbor] = newDist; previous[neighbor] = current; queue.Enqueue(neighbor, newDist); } } } SetState(ClientState.Confused); }
    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal) { List<Waypoint> pathList = new List<Waypoint>(); for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } pathList.Reverse(); path.Clear(); foreach (var wp in pathList) { path.Enqueue(wp); } }
    private Waypoint FindNearestVisibleWaypoint(Vector2 position, Waypoint[] wps, bool mustBeGeneral) { if(wps == null || wps.Length == 0) return null; Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in wps) { if (mustBeGeneral && wp.type == Waypoint.WaypointType.StaffOnly) continue; float distance = Vector2.Distance(position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles")); if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint; }
    private Waypoint FindNearestWaypoint(Vector2 p, Waypoint[] wps) { if(wps == null || wps.Length == 0) return null; return wps.OrderBy(wp => Vector2.Distance(p, wp.transform.position)).FirstOrDefault(); }
    public ClientState GetCurrentState() => currentState; public Waypoint GetCurrentGoal() => currentGoal; public void SetGoal(Waypoint g) => currentGoal = g;
    public string GetStatusInfo() { string goalName = (currentGoal != null) ? currentGoal.name : (seatTarget != null ? seatTarget.name : "неизвестно"); switch (currentState) { case ClientState.MovingToGoal: return $"Идет к: {goalName}"; case ClientState.MovingToSeat: return $"Идет на место: {goalName}"; case ClientState.AtWaitingArea: return "Ожидает стоя"; case ClientState.SittingInWaitingArea: return "Ожидает сидя"; case ClientState.AtRegistration: return "Обслуживается в регистратуре"; case ClientState.AtDesk1: return "Обслуживается у Стойки 1"; case ClientState.AtDesk2: return "Обслуживается у Стойки 2"; case ClientState.AtToilet: return "В туалете"; case ClientState.Enraged: return "В ЯРОСТИ!"; case ClientState.Leaving: return "Уходит"; case ClientState.GoingToCashier: return "Идет в кассу"; case ClientState.AtCashier: return "Оплачивает"; default: return currentState.ToString(); } }
    private void UpdateStateCounter(ClientState s, bool inc) { int v = inc ? 1 : -1; switch (s) { case ClientState.AtWaitingArea: case ClientState.SittingInWaitingArea: ClientPathfinding.clientsInWaiting += v; break; case ClientState.AtToilet: ClientPathfinding.clientsToToilet += v; break; case ClientState.AtRegistration: case ClientState.AtDesk1: case ClientState.AtDesk2: ClientPathfinding.clientsToRegistration += v; break; case ClientState.Confused: ClientPathfinding.clientsConfused += v; break; } }
    private class PriorityQueue<T, U> where U : System.IComparable<U> { private SortedDictionary<U, Queue<T>> d = new SortedDictionary<U, Queue<T>>(); public int Count => d.Sum(p => p.Value.Count); public void Enqueue(T i, U p) { if (!d.ContainsKey(p)) d[p] = new Queue<T>(); d[p].Enqueue(i); } public T Dequeue() { var p = d.First(); T i = p.Value.Dequeue(); if (p.Value.Count == 0) d.Remove(p.Key); return i; } }
}