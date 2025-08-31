// Файл: ClientStateMachine.cs
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
    [SerializeField] private float zonePatienceTime = 15f;
    [SerializeField] private float rageDuration = 10f;
    [SerializeField] private float requiredServiceDistance = 1.5f;
    [SerializeField] private float clerkWaitTimeout = 20f;

    [Header("Генерация беспорядка")]
    [SerializeField] private float baseTrashChancePerSecond = 0.01f;
    [SerializeField] private float puddleChanceInToilet = 0.2f;
    [SerializeField] private float puddleChanceOnUpset = 0.3f;
    
    private ClientState currentState = ClientState.Spawning;
    private Waypoint currentGoal, previousGoal;
    private int myQueueNumber = -1;
    private ClerkController myClerk;
    private Coroutine mainActionCoroutine, zonePatienceCoroutine, millingCoroutine;
    private Transform seatTarget;
    private bool isBeingHelped = false;

    public void Initialize(ClientPathfinding p) 
    { 
        parent = p; 
        agentMover = GetComponent<AgentMover>(); 
        logger = GetComponent<CharacterStateLogger>();
        if (parent.movement == null || agentMover == null) { enabled = false; return; } 
        StartCoroutine(MainLogicLoop()); 
        StartCoroutine(TrashGenerationRoutine());
    }

    private IEnumerator TrashGenerationRoutine()
    {
        while (true)
        {
            if (currentState != ClientState.Leaving && currentState != ClientState.LeavingUpset && parent.trashPrefabs != null && parent.trashPrefabs.Count > 0)
            {
                if (MessManager.Instance.CanCreateMess())
                {
                    float chance = baseTrashChancePerSecond + (parent.suetunFactor * 0.02f) - (parent.babushkaFactor * 0.005f);
                    if (Random.value < chance)
                    {
                        GameObject randomTrashPrefab = parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)];
                        Vector2 randomOffset = Random.insideUnitCircle * 0.2f;
                        Instantiate(randomTrashPrefab, (Vector2)transform.position + randomOffset, Quaternion.identity);
                    }
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator RegistrationServiceRoutine() 
    { 
        if (parent.mainGoal == ClientGoal.AskAndLeave)
        {
            yield return new WaitForSeconds(Random.Range(2f, 4f));

            if (myClerk != null) myClerk.ServiceComplete();
            ClientQueueManager.Instance.ServiceFinishedForNumber(myQueueNumber);
            
            LimitedCapacityZone zone = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>();
            if (zone != null) { zone.ReleaseWaypoint(GetCurrentGoal()); }

            parent.isLeavingSuccessfully = true;
            parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
            SetGoal(ClientSpawner.Instance.exitWaypoint);
            SetState(ClientState.Leaving);
            yield break;
        }

        DocumentType currentDoc = parent.docHolder.GetCurrentDocumentType();
        ClientGoal goal = parent.mainGoal;

        bool needsForm = (goal == ClientGoal.GetCertificate1 || goal == ClientGoal.GetCertificate2);
        bool hasWrongForm = (goal == ClientGoal.GetCertificate1 && currentDoc == DocumentType.Form2) || 
                              (goal == ClientGoal.GetCertificate2 && currentDoc == DocumentType.Form1);
        if (needsForm && (currentDoc == DocumentType.None || hasWrongForm))
        {
            if (myClerk != null) myClerk.ServiceComplete();
            ClientQueueManager.Instance.ServiceFinishedForNumber(myQueueNumber);
            SetGoal(ClientSpawner.Instance.formTable.tableWaypoint);
            SetState(ClientState.MovingToGoal);
            yield break;
        }
        
        float waitTimer = 0f;
        ClerkController clerk = myClerk;
        while (clerk == null || clerk.IsOnBreak() || Vector2.Distance(clerk.transform.position, clerk.assignedServicePoint.clerkStandPoint.position) > requiredServiceDistance)
        {
            yield return new WaitForSeconds(0.5f);
            waitTimer += 0.5f;

            if (waitTimer > clerkWaitTimeout)
            {
                Debug.LogWarning($"Клиент {parent.name} не дождался клерка-регистратора и ушел в 'Confused'");
                if (myClerk != null) myClerk.ServiceComplete();
                ClientQueueManager.Instance.ServiceFinishedForNumber(myQueueNumber);
                SetState(ClientState.Confused);
                yield break;
            }
            clerk = ClientSpawner.GetClerkAtDesk(0);
        }

        yield return new WaitForSeconds(Random.Range(2f, 4f) * (1f + parent.babushkaFactor));
        switch (goal)
        {
            case ClientGoal.GetCertificate1: 
                if(currentDoc == DocumentType.None) parent.docHolder.SetDocument(DocumentType.Form1);
                break;
            case ClientGoal.GetCertificate2: 
                if(currentDoc == DocumentType.None) parent.docHolder.SetDocument(DocumentType.Form2);
                break;
            case ClientGoal.PayTax: 
                parent.billToPay += Random.Range(20, 121);
                break;
        }

        LimitedCapacityZone routineZone = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>();
        if (routineZone != null) { routineZone.ReleaseWaypoint(GetCurrentGoal()); } 

        if (myClerk != null) myClerk.ServiceComplete();
        ClientQueueManager.Instance.ServiceFinishedForNumber(myQueueNumber);
        SetState(ClientState.PassedRegistration);
    }

    private IEnumerator PassedRegistrationRoutine() 
    {
        if (parent.billToPay > 0)
        {
            SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
            SetState(ClientState.MovingToGoal);
            yield break;
        }

        DocumentType docType = parent.docHolder.GetCurrentDocumentType();
        ClientGoal goal = parent.mainGoal;
        switch (goal)
        {
            case ClientGoal.PayTax:
                parent.isLeavingSuccessfully = true;
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                SetGoal(ClientSpawner.Instance.exitWaypoint);
                SetState(ClientState.Leaving);
                break;

            case ClientGoal.GetCertificate1:
                if (docType == DocumentType.Form1) { SetGoal(ClientSpawner.GetDesk1Zone().waitingWaypoint);
                    SetState(ClientState.MovingToGoal); }
                else
                { parent.isLeavingSuccessfully = true;
                    parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; SetGoal(ClientSpawner.Instance.exitWaypoint); SetState(ClientState.Leaving); }
                break;
            case ClientGoal.GetCertificate2:
                if (docType == DocumentType.Form2) { SetGoal(ClientSpawner.GetDesk2Zone().waitingWaypoint);
                    SetState(ClientState.MovingToGoal); }
                else
                { parent.isLeavingSuccessfully = true;
                    parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; SetGoal(ClientSpawner.Instance.exitWaypoint); SetState(ClientState.Leaving); }
                break;
            default:
                parent.isLeavingSuccessfully = true;
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; SetGoal(ClientSpawner.Instance.exitWaypoint); SetState(ClientState.Leaving);
                break;
        }
        yield return null;
    }

    // --- ИЗМЕНЕНИЕ ЗДЕСЬ ---
    private IEnumerator HandleImpoliteArrival() 
    { 
        agentMover.Stop();
        LimitedCapacityZone zone = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>();
        if (zone == null) { SetState(ClientState.Confused); yield break; }
        
        // Теперь невежливый клиент в любом случае отправляется в общую очередь.
        // Его "пролазность" заключается в том, что он сразу попал к регистратуре, а не шёл пешком.
        ClientQueueManager.Instance.JoinQueue(parent);
        SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent));
        SetState(ClientState.MovingToGoal);
        yield return null;
    }
    
    // --- ОСТАЛЬНЫЕ МЕТОДЫ СКРИПТА БЕЗ ИЗМЕНЕНИЙ ---
    public void StopAllActionCoroutines() { if (mainActionCoroutine != null) StopCoroutine(mainActionCoroutine); mainActionCoroutine = null; if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine); zonePatienceCoroutine = null; if (millingCoroutine != null) StopCoroutine(millingCoroutine); millingCoroutine = null; }
    public void GetCalledToSpecificDesk(Waypoint destination, int queueNumber, ClerkController clerk) { StopAllActionCoroutines(); ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); myQueueNumber = queueNumber; myClerk = clerk; SetGoal(destination); SetState(ClientState.MovingToGoal); }
    public void DecideToVisitToilet() { if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea) { StopAllActionCoroutines(); ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); previousGoal = currentGoal; SetGoal(ClientSpawner.GetToiletZone().waitingWaypoint); SetState(ClientState.MovingToGoal); } }
    public void GoToSeat(Transform seat) { StopAllActionCoroutines(); seatTarget = seat; SetState(ClientState.MovingToSeat); }
    public IEnumerator MainLogicLoop() { yield return new WaitForSeconds(0.1f); while (true) { if (parent.notification != null) parent.notification.UpdateNotification(); if (mainActionCoroutine == null) { mainActionCoroutine = StartCoroutine(HandleCurrentState()); } yield return null; } }
    private IEnumerator HandleCurrentState() { switch (currentState) { case ClientState.Spawning: if (Random.value < parent.prolazaFactor) { if (parent.impoliteSound != null) AudioSource.PlayClipAtPoint(parent.impoliteSound, transform.position); Waypoint impoliteGoal = null; switch (parent.mainGoal) { case ClientGoal.GetCertificate1: impoliteGoal = ClientSpawner.GetDesk1Zone().waitingWaypoint; break; case ClientGoal.GetCertificate2: impoliteGoal = ClientSpawner.GetDesk2Zone().waitingWaypoint; break; case ClientGoal.PayTax: impoliteGoal = ClientSpawner.GetCashierZone().waitingWaypoint; break; case ClientGoal.AskAndLeave: impoliteGoal = ClientSpawner.GetRegistrationZone().insideWaypoints[0]; break; case ClientGoal.VisitToilet: impoliteGoal = ClientSpawner.GetToiletZone().waitingWaypoint; break; } if (impoliteGoal != null) { SetGoal(impoliteGoal); SetState(ClientState.MovingToRegistrarImpolite); break; } } Waypoint initialGoal = null; if (parent.mainGoal == ClientGoal.VisitToilet) { initialGoal = ClientSpawner.GetToiletZone().waitingWaypoint; } else { initialGoal = ClientQueueManager.Instance.ChooseNewGoal(parent); } if(initialGoal != null) { SetGoal(initialGoal); SetState(ClientState.MovingToGoal); } else { Debug.LogError($"Не удалось найти начальную цель для {gameObject.name}. Зона ожидания настроена?"); SetState(ClientState.Confused); } break; case ClientState.MovingToGoal: case ClientState.GoingToCashier: case ClientState.Leaving: case ClientState.LeavingUpset: yield return StartCoroutine(MoveToGoalRoutine()); if (currentState == ClientState.MovingToGoal) { HandleArrivalAfterMove(); } else if (currentState == ClientState.GoingToCashier) { SetState(ClientState.AtCashier); } else if (currentState == ClientState.Leaving || currentState == ClientState.LeavingUpset) { parent.OnClientExit(); } break; case ClientState.MovingToRegistrarImpolite: yield return StartCoroutine(MoveToGoalRoutine()); if (currentState == ClientState.MovingToRegistrarImpolite) { yield return StartCoroutine(HandleImpoliteArrival()); } break; case ClientState.MovingToSeat: if (seatTarget != null) { parent.movement.StartStuckCheck(); agentMover.SetPath(BuildPathTo(seatTarget.position)); yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck(); } SetState(ClientState.SittingInWaitingArea); break; case ClientState.AtLimitedZoneEntrance: LimitedCapacityZone zone = GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(); if (zone != null) { yield return StartCoroutine(EnterZoneRoutine(zone)); } else { SetState(ClientState.Confused); } break; case ClientState.InsideLimitedZone: yield return StartCoroutine(ServiceRoutineInsideZone()); break; case ClientState.SittingInWaitingArea: ClientQueueManager.Instance.StartPatienceTimer(parent); yield return new WaitUntil(() => currentState != ClientState.SittingInWaitingArea); break; case ClientState.AtWaitingArea: millingCoroutine = StartCoroutine(MillAroundAndWaitForSeat()); yield return new WaitUntil(() => currentState != ClientState.AtWaitingArea); break; case ClientState.AtDesk1: yield return StartCoroutine(OfficeServiceRoutine(ClientSpawner.GetDesk1Zone())); break; case ClientState.AtDesk2: yield return StartCoroutine(OfficeServiceRoutine(ClientSpawner.GetDesk2Zone())); break; case ClientState.AtCashier: yield return StartCoroutine(PayBillRoutine()); break; case ClientState.AtRegistration: yield return StartCoroutine(RegistrationServiceRoutine()); break; case ClientState.Confused: yield return StartCoroutine(ConfusedRoutine()); break; case ClientState.Enraged: yield return StartCoroutine(EnragedRoutine()); break; case ClientState.PassedRegistration: yield return StartCoroutine(PassedRegistrationRoutine()); break; default: yield return null; break; } mainActionCoroutine = null; }
    private IEnumerator MoveToGoalRoutine() { if(currentGoal == null) { SetState(ClientState.Confused); yield break; } parent.movement.StartStuckCheck(); var path = BuildPathTo(currentGoal.transform.position); if (path.Count == 0 && Vector2.Distance(transform.position, currentGoal.transform.position) > agentMover.stoppingDistance) { parent.movement.StopStuckCheck(); Debug.LogWarning($"Клиент {gameObject.name} не смог построить путь к {currentGoal.name}."); SetState(ClientState.Confused); yield break; } agentMover.SetPath(path); yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck(); }
    private void HandleArrivalAfterMove() { Waypoint dest = GetCurrentGoal(); if (dest == null) { SetState(ClientState.Confused); return; } if (ClientQueueManager.Instance.IsWaypointInWaitingZone(dest)) { ClientQueueManager.Instance.JoinQueue(parent); if (ClientQueueManager.Instance.FindSeatForClient(parent) is Transform seat) { GoToSeat(seat); } else { SetState(ClientState.AtWaitingArea); } return; } var parentZone = dest.GetComponentInParent<LimitedCapacityZone>(); if (ClientSpawner.Instance.formTable != null && dest == ClientSpawner.Instance.formTable.tableWaypoint) { StartCoroutine(GetFormFromTableRoutine()); return; } if (parentZone != null && parentZone == ClientSpawner.GetRegistrationZone()) { SetState(ClientState.AtRegistration); return; } if (parentZone != null && dest.gameObject == parentZone.waitingWaypoint.gameObject) { SetState(ClientState.AtLimitedZoneEntrance); return; } SetState(ClientState.Confused); }
    private IEnumerator GetFormFromTableRoutine() { yield return new WaitForSeconds(1.5f); if (parent.mainGoal == ClientGoal.GetCertificate1) { parent.docHolder.SetDocument(DocumentType.Form1); } else if (parent.mainGoal == ClientGoal.GetCertificate2) { parent.docHolder.SetDocument(DocumentType.Form2); } else { if (Random.value < 0.5f) { parent.docHolder.SetDocument(DocumentType.Form1); } else { parent.docHolder.SetDocument(DocumentType.Form2); } } SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); }
    private IEnumerator OfficeServiceRoutine(LimitedCapacityZone zone) { SetGoal(zone.waitingWaypoint); SetState(ClientState.MovingToGoal); yield return new WaitUntil(() => currentState != ClientState.MovingToGoal); if (currentState != ClientState.AtLimitedZoneEntrance) yield break; yield return StartCoroutine(EnterZoneRoutine(zone)); if (currentState != ClientState.InsideLimitedZone) yield break; }
    private IEnumerator ServiceRoutineInsideZone() { yield return StartCoroutine(MoveToGoalRoutine()); LimitedCapacityZone currentZone = GetCurrentGoal().GetComponentInParent<LimitedCapacityZone>(); if (currentZone == ClientSpawner.GetDesk1Zone() || currentZone == ClientSpawner.GetDesk2Zone()) { int deskId = (currentZone == ClientSpawner.GetDesk1Zone()) ? 1 : 2; float waitTimer = 0f; ClerkController clerk = ClientSpawner.GetClerkAtDesk(deskId); while (clerk == null || clerk.IsOnBreak() || Vector2.Distance(clerk.transform.position, clerk.assignedServicePoint.clerkStandPoint.position) > requiredServiceDistance) { yield return new WaitForSeconds(0.5f); clerk = ClientSpawner.GetClerkAtDesk(deskId); waitTimer += 0.5f; if (waitTimer > clerkWaitTimeout) { Debug.LogWarning($"Клиент {parent.name} не дождался клерка у стойки {deskId} и ушел в 'Confused'"); yield return StartCoroutine(ExitZoneAndSetNewGoal(currentZone, null, ClientState.Confused)); yield break; } } DocumentType docTypeInHand = parent.docHolder.GetCurrentDocumentType(); GameObject prefabToFly = parent.docHolder.GetPrefabForType(docTypeInHand); parent.docHolder.SetDocument(DocumentType.None); bool transferToClerkComplete = false; GameObject flyingDoc = null; if (prefabToFly != null && clerk.assignedServicePoint != null) { flyingDoc = Instantiate(prefabToFly, parent.docHolder.handPoint.position, Quaternion.identity); DocumentMover mover = flyingDoc.GetComponent<DocumentMover>(); if (mover != null) { mover.StartMove(clerk.assignedServicePoint.documentPointOnDesk, () => { transferToClerkComplete = true; }); yield return new WaitUntil(() => transferToClerkComplete); } } yield return new WaitForSeconds(Random.Range(2f, 3f)); if (flyingDoc != null) { Destroy(flyingDoc); } float choice = Random.value - (parent.suetunFactor * 0.2f); DocumentType newDocType; if (choice < 0.8f) { newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2; parent.billToPay += 100; } else { newDocType = (deskId == 1) ? DocumentType.Form2 : DocumentType.Form1; } GameObject newDocPrefab = parent.docHolder.GetPrefabForType(newDocType); bool transferToClientComplete = false; if (newDocPrefab != null && clerk.assignedServicePoint != null) { GameObject newDocOnDesk = Instantiate(newDocPrefab, clerk.assignedServicePoint.documentPointOnDesk.position, Quaternion.identity); if (parent.stampSound != null) { AudioSource.PlayClipAtPoint(parent.stampSound, clerk.assignedServicePoint.documentPointOnDesk.position); } yield return new WaitForSeconds(2.5f); DocumentMover mover = newDocOnDesk.GetComponent<DocumentMover>(); if (mover != null) { mover.StartMove(parent.docHolder.handPoint, () => { parent.docHolder.ReceiveTransferredDocument(newDocType, newDocOnDesk); transferToClientComplete = true; }); yield return new WaitUntil(() => transferToClientComplete); } } yield return StartCoroutine(ExitZoneAndSetNewGoal(currentZone, null, ClientState.PassedRegistration)); } else if (currentZone == ClientSpawner.GetCashierZone()) { yield return StartCoroutine(PayBillRoutine()); } else if (currentZone == ClientSpawner.GetToiletZone()) { yield return new WaitForSeconds(Random.Range(5f, 10f)); if (parent.toiletSound != null) AudioSource.PlayClipAtPoint(parent.toiletSound, transform.position); if(parent.puddlePrefabs != null && parent.puddlePrefabs.Count > 0 && Random.value < puddleChanceInToilet) { if (MessManager.Instance.CanCreateMess()) { GameObject randomPuddlePrefab = parent.puddlePrefabs[Random.Range(0, parent.puddlePrefabs.Count)]; Instantiate(randomPuddlePrefab, transform.position, Quaternion.identity); } } Waypoint finalGoal; ClientState nextState; if (parent.mainGoal == ClientGoal.VisitToilet) { parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; finalGoal = ClientSpawner.Instance.exitWaypoint; nextState = ClientState.Leaving; } else { finalGoal = ClientQueueManager.Instance.GetToiletReturnGoal(parent); nextState = (finalGoal == ClientSpawner.Instance.exitWaypoint) ? ClientState.Leaving : ClientState.MovingToGoal; } yield return StartCoroutine(ExitZoneAndSetNewGoal(currentZone, finalGoal, nextState)); } }
    private IEnumerator EnterZoneRoutine(LimitedCapacityZone zone) { agentMover.Stop(); zone.JoinQueue(parent.gameObject); zonePatienceCoroutine = StartCoroutine(PatienceMonitorForZone(zone)); yield return new WaitUntil(() => zone.IsFirstInQueue(parent.gameObject)); Waypoint freeSpot = null; while (freeSpot == null) { if (currentState != ClientState.AtLimitedZoneEntrance) yield break; if(zone == null) { SetState(ClientState.Confused); yield break; } freeSpot = zone.RequestAndOccupyWaypoint(); if (freeSpot == null) { yield return new WaitForSeconds(0.5f); } } if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine); zonePatienceCoroutine = null; SetGoal(freeSpot); SetState(ClientState.InsideLimitedZone); }
    private IEnumerator PatienceMonitorForZone(LimitedCapacityZone zone) { yield return new WaitForSeconds(zonePatienceTime); if (currentState == ClientState.AtLimitedZoneEntrance) { zone.LeaveQueue(parent.gameObject); if (parent.puddlePrefabs != null && parent.puddlePrefabs.Count > 0 && zone == ClientSpawner.GetToiletZone() && Random.value < puddleChanceOnUpset) { if (MessManager.Instance.CanCreateMess()) { GameObject randomPuddlePrefab = parent.puddlePrefabs[Random.Range(0, parent.puddlePrefabs.Count)]; Instantiate(randomPuddlePrefab, transform.position, Quaternion.identity); } } if (Random.value < 0.5f) { SetState(ClientState.Enraged); } else { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; SetState(ClientState.LeavingUpset); } } }
    private IEnumerator PayBillRoutine() { agentMover.Stop(); if (parent.billToPay == 0 && parent.mainGoal == ClientGoal.PayTax) { parent.billToPay = Random.Range(20, 121); } float stealChance = (parent.prolazaFactor > 0.5f) ? (parent.prolazaFactor - 0.5f) * 0.5f : 0f; if (parent.billToPay > 0 && Random.value < stealChance) { if (parent.theftAttemptSound != null) AudioSource.PlayClipAtPoint(parent.theftAttemptSound, transform.position); yield return StartCoroutine(AttemptToStealRoutine()); yield break; } float waitTimer = 0f; ClerkController cashier = ClientSpawner.GetClerkAtDesk(-1); while(cashier == null || cashier.IsOnBreak() || Vector2.Distance(cashier.transform.position, cashier.assignedServicePoint.clerkStandPoint.position) > requiredServiceDistance) { cashier = ClientSpawner.GetClerkAtDesk(-1); yield return new WaitForSeconds(0.5f); waitTimer += 0.5f; if (waitTimer > clerkWaitTimeout) { Debug.LogWarning($"Клиент {parent.name} не дождался кассира и ушел в 'Confused'"); yield return StartCoroutine(ExitZoneAndSetNewGoal(GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(), null, ClientState.Confused)); yield break; } } if (parent.moneyPrefab != null) { int numberOfBanknotes = 5; float delayBetweenSpawns = 0.15f; for (int i = 0; i < numberOfBanknotes; i++) { Vector2 spawnPosition = (Vector2)transform.position + (Random.insideUnitCircle * 0.2f); GameObject moneyInstance = Instantiate(parent.moneyPrefab, spawnPosition, Quaternion.identity); MoneyMover mover = moneyInstance.GetComponent<MoneyMover>(); Transform moneyTarget = (cashier.assignedServicePoint != null) ? cashier.assignedServicePoint.documentPointOnDesk : cashier.transform; if (mover != null) { mover.StartMove(moneyTarget); } yield return new WaitForSeconds(delayBetweenSpawns); } } if (PlayerWallet.Instance != null && parent != null) { PlayerWallet.Instance.AddMoney(parent.billToPay, transform.position); if (parent.paymentSound != null) AudioSource.PlayClipAtPoint(parent.paymentSound, transform.position); parent.billToPay = 0; } parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; yield return StartCoroutine(ExitZoneAndSetNewGoal(GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(), ClientSpawner.Instance.exitWaypoint, ClientState.Leaving)); }
    private IEnumerator AttemptToStealRoutine() { yield return new WaitForSeconds(Random.Range(1f, 2f)); GuardManager.Instance.ReportTheft(parent); parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; yield return StartCoroutine(ExitZoneAndSetNewGoal(GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>(), ClientSpawner.Instance.exitWaypoint, ClientState.Leaving)); }
    private IEnumerator ExitZoneAndSetNewGoal(LimitedCapacityZone zone, Waypoint finalDestination, ClientState stateAfterExit) { if (zone != null) { zone.ReleaseWaypoint(GetCurrentGoal()); if (zone.exitWaypoint != null) { SetGoal(zone.exitWaypoint); yield return StartCoroutine(MoveToGoalRoutine()); } } SetGoal(finalDestination); SetState(stateAfterExit); }
    private IEnumerator ConfusedRoutine() { agentMover.Stop(); isBeingHelped = false; while(!isBeingHelped) { yield return new WaitForSeconds(3f); if (isBeingHelped) break; float recoveryChance = 0.3f * (1f - parent.babushkaFactor); float choice = Random.value; if (choice < recoveryChance) { SetGoal(previousGoal); SetState(ClientState.MovingToGoal); yield break; } else if (choice < recoveryChance + 0.2f) { SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); yield break; } } }
    private IEnumerator EnragedRoutine() { ClientQueueManager.Instance.AddAngryClient(parent); if (parent.trashPrefabs != null && parent.trashPrefabs.Count > 0) { int trashCount = Random.Range(4, 8); for(int i = 0; i < trashCount; i++) { if (MessManager.Instance.CanCreateMess()) { GameObject randomTrashPrefab = parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)]; Vector2 randomOffset = Random.insideUnitCircle * 0.5f; Instantiate(randomTrashPrefab, (Vector2)transform.position + randomOffset, Quaternion.identity); } } } var thoughtController = GetComponent<ThoughtBubbleController>(); if (thoughtController != null) { thoughtController.StopThinking(); StartCoroutine(RageThinkLoop(thoughtController)); } float rageTimer = Time.time + rageDuration; while (Time.time < rageTimer) { Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); if(allWaypoints.Length > 0) { Waypoint randomTarget = allWaypoints[Random.Range(0, allWaypoints.Length)]; agentMover.SetPath(BuildPathTo(randomTarget.transform.position)); yield return new WaitUntil(() => !agentMover.IsMoving() || Vector2.Distance(transform.position, randomTarget.transform.position) < 2f); } else { yield return new WaitForSeconds(1f); } } parent.reasonForLeaving = ClientPathfinding.LeaveReason.Angry; SetGoal(ClientSpawner.Instance.exitWaypoint); SetState(ClientState.Leaving); }
    private IEnumerator RageThinkLoop(ThoughtBubbleController controller) { while (GetCurrentState() == ClientState.Enraged) { controller.TriggerCriticalThought($"Client_{ClientState.Enraged}"); yield return new WaitForSeconds(Random.Range(2f, 4f)); } }
    private IEnumerator MillAroundAndWaitForSeat() { ClientQueueManager.Instance.StartPatienceTimer(parent); while (currentState == ClientState.AtWaitingArea) { Transform freeSeat = ClientQueueManager.Instance.FindSeatForClient(parent); if (freeSeat != null) { GoToSeat(freeSeat); yield break; } Waypoint randomStandingPoint = ClientQueueManager.Instance.ChooseNewGoal(parent); if(randomStandingPoint != null) { SetGoal(randomStandingPoint); yield return StartCoroutine(MoveToGoalRoutine()); } yield return new WaitForSeconds(Random.Range(2f, 4f)); } }
    public void GetHelpFromIntern(Waypoint newGoal = null) { if (parent.helpedByInternSound != null) AudioSource.PlayClipAtPoint(parent.helpedByInternSound, transform.position); if(currentState == ClientState.Confused) { isBeingHelped = true; SetGoal(newGoal ?? ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); } else { StopAllActionCoroutines(); if(newGoal != null) { if (newGoal == ClientSpawner.Instance.exitWaypoint) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Normal; SetState(ClientState.Leaving); } else { SetState(ClientState.MovingToGoal); } ClientQueueManager.Instance.RemoveClientFromQueue(parent); SetGoal(newGoal); } } }
    public void SetState(ClientState state) { if (state == currentState) return; if (state == ClientState.Confused && currentState == ClientState.Enraged) return; if (currentState == ClientState.AtLimitedZoneEntrance && GetCurrentGoal() != null) { LimitedCapacityZone zone = GetCurrentGoal().GetComponentInParent<LimitedCapacityZone>(); if (zone != null) { zone.LeaveQueue(parent.gameObject); } } StopAllActionCoroutines(); agentMover?.Stop(); if (state == ClientState.Confused) { previousGoal = currentGoal; if (parent.confusedSound != null) AudioSource.PlayClipAtPoint(parent.confusedSound, transform.position); } currentState = state; logger?.LogState(GetStatusInfo()); }
    public Queue<Waypoint> BuildPathTo(Vector2 targetPos) { var newPath = new Queue<Waypoint>(); Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); if (allWaypoints.Length == 0) return newPath; Waypoint start = FindNearestVisibleWaypoint(transform.position, allWaypoints); Waypoint goal = allWaypoints.FirstOrDefault(wp => Vector2.Distance(wp.transform.position, targetPos) < 0.01f); if (goal == null) { goal = FindNearestVisibleWaypoint(targetPos, allWaypoints); } if (start == null) { start = FindNearestWaypoint(transform.position, allWaypoints); } if (goal == null) { goal = FindNearestWaypoint(targetPos, allWaypoints); } if (start == null || goal == null) { SetState(ClientState.Confused); return newPath; } Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>(); PriorityQueue<Waypoint, float> queue = new PriorityQueue<Waypoint, float>(); foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; } distances[start] = 0; queue.Enqueue(start, 0); while (queue.Count > 0) { Waypoint current = queue.Dequeue(); if (current == goal) { ReconstructPath(previous, goal, newPath); return newPath; } if (current.neighbors == null || current.neighbors.Count == 0) continue; foreach (var neighbor in current.neighbors) { if (neighbor == null) continue; if (neighbor.type == Waypoint.WaypointType.StaffOnly && neighbor != ClientSpawner.Instance.exitWaypoint) continue; float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position); if (distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { distances[neighbor] = newDist; previous[neighbor] = current; queue.Enqueue(neighbor, newDist); } } } SetState(ClientState.Confused); return newPath; }
    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path) { List<Waypoint> pathList = new List<Waypoint>(); for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } pathList.Reverse(); path.Clear(); foreach (var wp in pathList) { path.Enqueue(wp); } }
    private Waypoint FindNearestVisibleWaypoint(Vector2 position, Waypoint[] wps) { if(wps == null || wps.Length == 0) return null; Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in wps) { if (wp.type == Waypoint.WaypointType.StaffOnly) continue; float distance = Vector2.Distance(position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles")); if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint; }
    private Waypoint FindNearestWaypoint(Vector2 p, Waypoint[] wps) { if(wps == null || wps.Length == 0) return null; return wps.Where(wp => wp.type != Waypoint.WaypointType.StaffOnly).OrderBy(wp => Vector2.Distance(p, wp.transform.position)).FirstOrDefault(); }
    public ClientState GetCurrentState() => currentState;
    public Waypoint GetCurrentGoal() => currentGoal; public void SetGoal(Waypoint g) => currentGoal = g;
    public string GetStatusInfo() { if (parent == null) return "Нет данных"; string traits = $"Б: {parent.babushkaFactor:F2} | C: {parent.suetunFactor:F2} | П: {parent.prolazaFactor:F2}"; string goal = $"Цель: {parent.mainGoal}"; string statusText = ""; string goalName = (currentGoal != null) ? currentGoal.name : (seatTarget != null ? seatTarget.name : "неизвестно"); switch (currentState) { case ClientState.MovingToGoal: case ClientState.MovingToRegistrarImpolite: case ClientState.GoingToCashier: case ClientState.Leaving: case ClientState.LeavingUpset: statusText += $"Идет к: {goalName}"; break; case ClientState.MovingToSeat: statusText += $"Идет на место: {seatTarget.name}"; break; case ClientState.AtWaitingArea: statusText += "Ожидает стоя"; break; case ClientState.SittingInWaitingArea: statusText += "Ожидает сидя"; break; case ClientState.AtRegistration: statusText += "Обслуживается в регистратуре"; break; case ClientState.AtDesk1: statusText += "Обслуживается у Стойки 1"; break; case ClientState.AtDesk2: statusText += "Обслуживается у Стойки 2"; break; case ClientState.InsideLimitedZone: statusText += $"В зоне: {currentGoal?.GetComponentInParent<LimitedCapacityZone>()?.name}"; break; case ClientState.Enraged: statusText += "В ЯРОСТИ!"; break; case ClientState.AtCashier: statusText += "Оплачивает"; break; case ClientState.AtLimitedZoneEntrance: statusText += $"Ждет входа в зону: {currentGoal?.GetComponentInParent<LimitedCapacityZone>()?.name}"; break; default: statusText += currentState.ToString(); break; } if (parent.docHolder != null) { DocumentType docType = parent.docHolder.GetCurrentDocumentType(); if (docType != DocumentType.None) { statusText += $" (несет {docType})"; } } return $"{traits}\n{goal}\n{statusText}"; }
    private class PriorityQueue<T, U> where U : System.IComparable<U> { private SortedDictionary<U, Queue<T>> dictionary = new SortedDictionary<U, Queue<T>>(); public int Count => dictionary.Sum(p => p.Value.Count); public void Enqueue(T item, U priority) { if (!dictionary.ContainsKey(priority)) { dictionary[priority] = new Queue<T>(); } dictionary[priority].Enqueue(item); } public T Dequeue() { var pair = dictionary.First(); T item = pair.Value.Dequeue(); if (pair.Value.Count == 0) { dictionary.Remove(pair.Key); } return item; } }
}