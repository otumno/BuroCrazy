// File: ClientStateMachine.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ClientStateMachine : MonoBehaviour
{
    private ClientPathfinding parent;
    private AgentMover agentMover;
    private CharacterStateLogger logger;

    [Header("Settings")]
    [SerializeField] private float zonePatienceTime = 15f;
    [SerializeField] private float rageDuration = 10f;
    [SerializeField] private float requiredServiceDistance = 1.5f;
    [SerializeField] private float clerkWaitTimeout = 20f;
    [Header("Mess Generation")]
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
    
    private LimitedCapacityZone targetZone;
    
    public void Initialize(ClientPathfinding p) 
    { 
        parent = p;
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
        if (parent.movement == null || agentMover == null) { enabled = false; return; } 
        StartCoroutine(MainLogicLoop());
        StartCoroutine(TrashGenerationRoutine());
    }

    private IEnumerator RegistrationServiceRoutine() 
    {
        ClerkController servingClerk = myClerk;
        if (servingClerk == null)
        {
             SetState(ClientState.Confused);
             yield break;
        }
        
        float waitTimer = 0f;
        while (servingClerk == null || servingClerk.IsOnBreak() || Vector2.Distance(servingClerk.transform.position, servingClerk.assignedServicePoint.clerkStandPoint.position) > requiredServiceDistance)
        {
            yield return new WaitForSeconds(0.5f);
            waitTimer += 0.5f;
            if (waitTimer > clerkWaitTimeout) { SetState(ClientState.Confused); yield break; }
            servingClerk = myClerk;
        }

        if (parent.mainGoal == ClientGoal.DirectorApproval)
        {
            Debug.Log($"[Registration] Client {parent.name} with documents for the Director! Sending to reception.");
            yield return new WaitForSeconds(Random.Range(1f, 2f));
            servingClerk.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage("Please proceed to the\ndirector's reception.", 3f, Color.cyan);
            
            LimitedCapacityZone receptionZone = ClientSpawner.Instance.directorReceptionZone;
            if (receptionZone != null && receptionZone.waitingWaypoint != null)
            {
                targetZone = receptionZone;
                SetGoal(receptionZone.waitingWaypoint);
                SetState(ClientState.MovingToGoal);
            }
            else
            {
                Debug.LogError("The director's reception zone or its entry point (directorReceptionZone) is not configured!");
                SetState(ClientState.Confused);
            }
            yield break;
        }

        if (servingClerk.skills != null)
        {
            float patiencePenalty = (1f - servingClerk.skills.softSkills) * 10f;
            parent.totalPatienceTime -= patiencePenalty;
            if (patiencePenalty > 0)
            {
                Debug.Log($"[Soft Skills] Registrar {servingClerk.name} reduced client's patience by {patiencePenalty:F1} sec.");
            }
        }

        Debug.Log($"[Registration] Regular client {parent.name} with registrar {servingClerk.name}.");
        yield return new WaitForSeconds(Random.Range(2f, 4f));
        
        Waypoint correctDestination = DetermineCorrectGoalAfterRegistration();
        Waypoint actualDestination = correctDestination;
        if (servingClerk.skills != null)
        {
            float chanceOfError = (1f - servingClerk.skills.softSkills) * 0.5f;
            if (Random.value < chanceOfError)
            {
                Debug.LogWarning($"[Registration] ERROR! Registrar {servingClerk.name} misdirected client {parent.name}.");
                List<Waypoint> possibleDestinations = new List<Waypoint> { ClientSpawner.GetDesk1Zone().waitingWaypoint, ClientSpawner.GetDesk2Zone().waitingWaypoint, ClientSpawner.GetCashierZone().waitingWaypoint };
                possibleDestinations.Remove(correctDestination);
                if(possibleDestinations.Count > 0) { actualDestination = possibleDestinations[Random.Range(0, possibleDestinations.Count)]; }
            }
        }

        string destinationName = string.IsNullOrEmpty(actualDestination.friendlyName) ? actualDestination.name : actualDestination.friendlyName;
        string directionMessage = $"Please proceed to\n'{destinationName}'";
        servingClerk.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage(directionMessage, 3f, Color.white);
        
        Debug.Log($"[Registration] Client {parent.name} directed to {actualDestination.name}.");
        if (myQueueNumber != -1) { ClientQueueManager.Instance.RemoveClientFromQueue(parent); }
        SetGoal(actualDestination);
        SetState(ClientState.MovingToGoal);
    }
    
    private Waypoint DetermineCorrectGoalAfterRegistration()
    {
        if (parent.billToPay > 0) return ClientSpawner.GetCashierZone().waitingWaypoint;
        switch (parent.mainGoal)
        {
            case ClientGoal.PayTax: return ClientSpawner.GetCashierZone().waitingWaypoint;
            case ClientGoal.GetCertificate1: return ClientSpawner.GetDesk1Zone().waitingWaypoint;
            case ClientGoal.GetCertificate2: return ClientSpawner.GetDesk2Zone().waitingWaypoint;
            case ClientGoal.AskAndLeave:
            default: 
                parent.isLeavingSuccessfully = true;
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                return ClientSpawner.Instance.exitWaypoint;
        }
    }

    private IEnumerator ServiceRoutineInsideZone() 
    { 
        yield return StartCoroutine(MoveToGoalRoutine());
        if (targetZone == null) { SetState(ClientState.Confused); yield break; }

        if (targetZone == ClientSpawner.GetDesk1Zone() || targetZone == ClientSpawner.GetDesk2Zone()) 
        { 
            int deskId = (targetZone == ClientSpawner.GetDesk1Zone()) ? 1 : 2;
            
            float waitTimer = 0f; 
            ClerkController clerk = ClientSpawner.GetClerkAtDesk(deskId);
            while (clerk == null || clerk.IsOnBreak() || Vector2.Distance(clerk.transform.position, clerk.assignedServicePoint.clerkStandPoint.position) > requiredServiceDistance) 
            { 
                yield return new WaitForSeconds(0.5f);
                clerk = ClientSpawner.GetClerkAtDesk(deskId); 
                waitTimer += 0.5f; 
                if (waitTimer > clerkWaitTimeout) { yield return StartCoroutine(ExitZoneAndSetNewGoal(null, ClientState.Confused)); yield break; } 
            }

            if (clerk.skills != null)
            {
                float patiencePenalty = (1f - clerk.skills.softSkills) * 10f;
                parent.totalPatienceTime -= patiencePenalty;
                if (patiencePenalty > 0)
                {
                    Debug.Log($"[Soft Skills] Clerk {clerk.name} reduced client's patience by {patiencePenalty:F1} sec.");
                }
            }
            
            if (parent.docHolder.GetCurrentDocumentType() == DocumentType.None)
            {
                Debug.Log($"[Clerk-{deskId}] Client {parent.name} has no documents. Sending to form table.");
                clerk.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage("Please get a form\nfrom the table!", 3f, Color.yellow);
                yield return new WaitForSeconds(2f);
                previousGoal = GetCurrentGoal();
                yield return StartCoroutine(ExitZoneAndSetNewGoal(ClientSpawner.Instance.formTable.tableWaypoint, ClientState.MovingToGoal));
                yield break;
            }

            float documentErrorPercent = (1f - parent.documentQuality) * 100f;
            Debug.Log($"[Clerk-{deskId}] Checking: {parent.name}, Quality: {parent.documentQuality:P0}, Error %: {documentErrorPercent:F1}%");

            if (documentErrorPercent > 25f)
            {
                clerk.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage("Rejected!\nToo many errors.", 3f, Color.red);
                yield return new WaitForSeconds(2f);
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                yield return StartCoroutine(ExitZoneAndSetNewGoal(ClientSpawner.Instance.exitWaypoint, ClientState.LeavingUpset));
                yield break;
            }

            if (documentErrorPercent > 10f)
            {
                float chanceToSpotErrors = clerk.skills != null ? clerk.skills.pedantry : 0.5f;
                if (Random.value < chanceToSpotErrors)
                {
                    clerk.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage("The document needs\nto be redone!", 3f, Color.yellow);
                    yield return new WaitForSeconds(3f);
					previousGoal = GetCurrentGoal();
                    yield return StartCoroutine(ExitZoneAndSetNewGoal(ClientSpawner.Instance.formTable.tableWaypoint, ClientState.MovingToGoal));
                    yield break;
                }
                else
                {
                    Debug.Log($"[Clerk-{deskId}] MISSED an error (Pedantry: {chanceToSpotErrors:P0}). Processing a poor-quality document!");
                }
            }
            
            Debug.Log($"[Clerk-{deskId}] Continuing to serve client {parent.name}.");
            DocumentType docTypeInHand = parent.docHolder.GetCurrentDocumentType();
            GameObject prefabToFly = parent.docHolder.GetPrefabForType(docTypeInHand); 
            parent.docHolder.SetDocument(DocumentType.None);
            bool transferToClerkComplete = false; 
            GameObject flyingDoc = null;
            if (prefabToFly != null && clerk.assignedServicePoint != null) { 
                flyingDoc = Instantiate(prefabToFly, parent.docHolder.handPoint.position, Quaternion.identity);
                DocumentMover mover = flyingDoc.GetComponent<DocumentMover>(); 
                if (mover != null) { mover.StartMove(clerk.assignedServicePoint.documentPointOnDesk, () => { transferToClerkComplete = true; });
                yield return new WaitUntil(() => transferToClerkComplete); } 
            } 
            yield return new WaitForSeconds(Random.Range(2f, 3f));
            if (flyingDoc != null) { Destroy(flyingDoc); } 
            
            float choice = Random.value - (parent.suetunFactor * 0.2f);
            DocumentType newDocType; 
            if (choice < 0.8f) 
            { 
                newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2; parent.billToPay += 100; 
            } 
            else 
            { 
                newDocType = (deskId == 1) ? DocumentType.Form2 : DocumentType.Form1; 
            } 
            GameObject newDocPrefab = parent.docHolder.GetPrefabForType(newDocType);
            bool transferToClientComplete = false; 
            if (newDocPrefab != null && clerk.assignedServicePoint != null) 
            { 
                GameObject newDocOnDesk = Instantiate(newDocPrefab, clerk.assignedServicePoint.documentPointOnDesk.position, Quaternion.identity);
                if (parent.stampSound != null) { AudioSource.PlayClipAtPoint(parent.stampSound, clerk.assignedServicePoint.documentPointOnDesk.position); } 
                yield return new WaitForSeconds(2.5f);
                DocumentMover mover = newDocOnDesk.GetComponent<DocumentMover>(); 
                if (mover != null) { mover.StartMove(parent.docHolder.handPoint, () => { parent.docHolder.ReceiveTransferredDocument(newDocType, newDocOnDesk); transferToClientComplete = true; });
                yield return new WaitUntil(() => transferToClientComplete); } 
            } 
            
            if (clerk != null && clerk.role == ClerkController.ClerkRole.Regular) { clerk.ServiceComplete(); } 
            yield return StartCoroutine(ExitZoneAndSetNewGoal(null, ClientState.PassedRegistration));
        } 
        else if (targetZone == ClientSpawner.GetCashierZone()) 
        { 
            yield return StartCoroutine(PayBillRoutine());
        } 
        else if (targetZone == ClientSpawner.GetToiletZone()) 
        { 
            yield return StartCoroutine(ToiletRoutineInZone());
        } 
    }

    private IEnumerator ToiletRoutineInZone() 
    {
        yield return new WaitForSeconds(Random.Range(5f, 10f));
        if (parent.toiletSound != null) AudioSource.PlayClipAtPoint(parent.toiletSound, transform.position);
        if (parent.puddlePrefabs != null && parent.puddlePrefabs.Count > 0 && Random.value < puddleChanceInToilet) 
        {
            if (MessManager.Instance.CanCreateMess()) 
            {
                GameObject randomPuddlePrefab = parent.puddlePrefabs[Random.Range(0, parent.puddlePrefabs.Count)];
                Instantiate(randomPuddlePrefab, transform.position, Quaternion.identity);
            }
        }
        Waypoint finalGoal;
        ClientState nextState;
        if (parent.mainGoal == ClientGoal.VisitToilet) 
        {
            parent.isLeavingSuccessfully = true;
            parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
            finalGoal = ClientSpawner.Instance.exitWaypoint;
            nextState = ClientState.Leaving;
        } 
        else 
        {
            finalGoal = ClientQueueManager.Instance.GetToiletReturnGoal(parent);
            nextState = (finalGoal == ClientSpawner.Instance.exitWaypoint) ? ClientState.Leaving : ClientState.MovingToGoal;
        }
        yield return StartCoroutine(ExitZoneAndSetNewGoal(finalGoal, nextState));
    }
    
    public void SetState(ClientState newState) 
    { 
        if (newState == currentState) return;
        if ((currentState == ClientState.InsideLimitedZone || currentState == ClientState.AtLimitedZoneEntrance) && newState != ClientState.InsideLimitedZone)
        {
            if (targetZone != null)
            {
                targetZone.LeaveQueue(parent.gameObject);
                targetZone.ReleaseWaypoint(GetCurrentGoal());
            }
        }

        if (newState == ClientState.Leaving || newState == ClientState.LeavingUpset || newState == ClientState.Confused || newState == ClientState.Enraged)
        {
            if (myQueueNumber != -1)
            {
                if(newState != ClientState.Leaving || parent.reasonForLeaving != ClientPathfinding.LeaveReason.Processed)
                 ClientQueueManager.Instance.ServiceFinishedForNumber(myQueueNumber);
            }
        }

        if (newState == ClientState.Confused && currentState == ClientState.Enraged) return;
        StopAllActionCoroutines();
        agentMover?.Stop();
        
        if (newState == ClientState.Confused) 
        { 
            previousGoal = currentGoal;
            if (parent.confusedSound != null) AudioSource.PlayClipAtPoint(parent.confusedSound, transform.position);
        } 
        
        currentState = newState;
        logger?.LogState(GetStatusInfo());

        var visuals = parent.GetVisuals();
        if (visuals == null) return;
        if (newState == ClientState.Leaving)
        {
            switch (parent.reasonForLeaving)
            {
                case ClientPathfinding.LeaveReason.Processed: visuals.SetEmotion(Emotion.Happy); break;
                case ClientPathfinding.LeaveReason.Angry: visuals.SetEmotion(Emotion.Angry); break;
                case ClientPathfinding.LeaveReason.Theft: visuals.SetEmotion(Emotion.Sly); break;
                default: visuals.SetEmotion(Emotion.Sad); break;
            }
        }
        else
        {
            visuals.SetEmotionForState(newState);
        }
    }

    public void StopAllActionCoroutines() 
    { 
        if (mainActionCoroutine != null) StopCoroutine(mainActionCoroutine);
        mainActionCoroutine = null; 
        if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine); 
        zonePatienceCoroutine = null; 
        if (millingCoroutine != null) StopCoroutine(millingCoroutine); 
        millingCoroutine = null;
    }

    public void GetCalledToSpecificDesk(Waypoint destination, int queueNumber, ClerkController clerk) 
    { 
        StopAllActionCoroutines();
        ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); 
        myQueueNumber = queueNumber; 
        myClerk = clerk; 
        SetGoal(destination); 
        SetState(ClientState.MovingToGoal); 
    }
    
    public void DecideToVisitToilet() 
    { 
        if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea) 
        { 
            StopAllActionCoroutines();
            ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); 
            previousGoal = currentGoal; 
            SetGoal(ClientSpawner.GetToiletZone().waitingWaypoint); 
            SetState(ClientState.MovingToGoal); 
        } 
    }

    public void GoToSeat(Transform seat) 
    { 
        StopAllActionCoroutines();
        seatTarget = seat; 
        SetState(ClientState.MovingToSeat); 
    }

    public IEnumerator MainLogicLoop() 
    { 
        yield return new WaitForSeconds(0.1f);
        while (true) 
        { 
            if (parent.notification != null) parent.notification.UpdateNotification();
            if (mainActionCoroutine == null) 
            { 
                mainActionCoroutine = StartCoroutine(HandleCurrentState());
            } 
            yield return null;
        } 
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
    
    private IEnumerator MoveToGoalRoutine()
    {
        if (currentGoal != null)
        {
            parent.movement.StartStuckCheck();
            agentMover.SetPath(BuildPathTo(currentGoal.transform.position));
            yield return new WaitUntil(() => !agentMover.IsMoving());
            parent.movement.StopStuckCheck();
        }
        else
        {
            Debug.LogWarning($"{name} wanted to move, but currentGoal was not set.");
            SetState(ClientState.Confused);
        }
    }

    private IEnumerator GetFormFromTableRoutine()
    {
        yield return new WaitForSeconds(Random.Range(2f, 4f));
        if (parent.mainGoal == ClientGoal.GetCertificate1)
        {
            parent.docHolder.SetDocument(DocumentType.Form1);
        }
        else if (parent.mainGoal == ClientGoal.GetCertificate2)
        {
            parent.docHolder.SetDocument(DocumentType.Form2);
        }

        SetGoal(previousGoal);
        SetState(ClientState.ReturningToRegistrar);
    }
    
    private void HandleArrivalAfterMove()
    {
        Waypoint dest = GetCurrentGoal();
        if (dest == null) { SetState(ClientState.Confused); return; }
        
        if (myClerk != null && myClerk.role == ClerkController.ClerkRole.Registrar && dest == myClerk.assignedServicePoint.clientStandPoint)
        {
            ClientQueueManager.Instance.ClientArrivedAtDesk(myQueueNumber);
            SetState(ClientState.AtRegistration);
            return;
        }
        
        if (targetZone != null && targetZone.waitingWaypoint == dest)
        {
             SetState(ClientState.AtLimitedZoneEntrance);
             return;
        }

        targetZone = FindObjectsByType<LimitedCapacityZone>(FindObjectsSortMode.None).FirstOrDefault(z => z.waitingWaypoint == dest);
        if (targetZone != null)
        {
            SetState(ClientState.AtLimitedZoneEntrance);
            return;
        }
        
        if (ClientQueueManager.Instance.IsWaypointInWaitingZone(dest))
        {
            ClientQueueManager.Instance.JoinQueue(parent);
            Transform seat = ClientQueueManager.Instance.FindSeatForClient(parent);
            if (seat != null) { GoToSeat(seat); } else { SetState(ClientState.AtWaitingArea); }
            return;
        }
        
        if (ClientSpawner.Instance.formTable != null && dest == ClientSpawner.Instance.formTable.tableWaypoint)
        {
            StartCoroutine(GetFormFromTableRoutine());
            return;
        }
        
        Debug.LogWarning($"{name} arrived at {dest.name}, but doesn't know what to do next. Assigned clerk: {(myClerk != null ? myClerk.name : "null")}");
        SetState(ClientState.Confused);
    }

    private IEnumerator EnterZoneRoutine() 
    { 
        if (targetZone == null) { SetState(ClientState.Confused); yield break; }
        
        agentMover.Stop();
        targetZone.JoinQueue(parent.gameObject);
        zonePatienceCoroutine = StartCoroutine(PatienceMonitorForZone(targetZone));
        yield return new WaitUntil(() => targetZone.IsFirstInQueue(parent.gameObject)); 
        
        Waypoint freeSpot = null;
        while (freeSpot == null) 
        { 
            if (currentState != ClientState.AtLimitedZoneEntrance) yield break;
            freeSpot = targetZone.RequestAndOccupyWaypoint(parent.gameObject); 
            if (freeSpot == null) { yield return new WaitForSeconds(0.5f); } 
        } 
        
        targetZone.LeaveQueue(parent.gameObject);
        if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine);
        zonePatienceCoroutine = null; 
        SetGoal(freeSpot); 
        SetState(ClientState.InsideLimitedZone);
        
		Debug.Log($"[CHECK] Client {parent.name} is entering zone {targetZone.name}. Their goal is: {parent.mainGoal}");
        if (parent.mainGoal == ClientGoal.DirectorApproval && targetZone == ClientSpawner.Instance.directorReceptionZone)
        {
            StartOfDayPanel directorsDesk = StartOfDayPanel.Instance;
            if (directorsDesk != null)
            {
                directorsDesk.RegisterDirectorDocument(parent);
            }
        }
    }

    private IEnumerator ExitZoneAndSetNewGoal(Waypoint finalDestination, ClientState stateAfterExit) 
    { 
        if (targetZone != null) 
        { 
            yield return new WaitForSeconds(1.0f);
            targetZone.ReleaseWaypoint(GetCurrentGoal());

            if (targetZone.exitWaypoint != null) 
            { 
                SetGoal(targetZone.exitWaypoint);
                yield return StartCoroutine(MoveToGoalRoutine()); 
            } 
        } 
        SetGoal(finalDestination);
        SetState(stateAfterExit); 
    }
    
    private IEnumerator PayBillRoutine() 
    { 
        agentMover.Stop();
        if (parent.billToPay == 0 && parent.mainGoal == ClientGoal.PayTax) { parent.billToPay = Random.Range(20, 121); } 
        
        float stealChance = (parent.prolazaFactor > 0.5f) ? (parent.prolazaFactor - 0.5f) * 0.5f : 0f;
        if (parent.billToPay > 0 && Random.value < stealChance) { 
            if (parent.theftAttemptSound != null) AudioSource.PlayClipAtPoint(parent.theftAttemptSound, transform.position);
            yield return StartCoroutine(AttemptToStealRoutine()); 
            yield break;
        } 
        
        float waitTimer = 0f;
        ClerkController cashier = null;
        while(cashier == null || cashier.IsOnBreak() || Vector2.Distance(cashier.transform.position, cashier.assignedServicePoint.clerkStandPoint.position) > requiredServiceDistance) 
        { 
            cashier = ClientSpawner.GetClerkAtDesk(-1);
            yield return new WaitForSeconds(0.5f);
            waitTimer += 0.5f; 
            if (waitTimer > clerkWaitTimeout) { 
                Debug.LogWarning($"Client {parent.name} didn't wait for the cashier and became 'Confused'");
                yield return StartCoroutine(ExitZoneAndSetNewGoal(null, ClientState.Confused)); 
                yield break; 
            } 
        } 

        string billMessage = $"That will be: ${parent.billToPay}";
        cashier.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage(billMessage, 3f, new Color(0.1f, 0.4f, 0.1f));

        if (parent.moneyPrefab != null) { 
            int numberOfBanknotes = 5;
            float delayBetweenSpawns = 0.15f;
            for (int i = 0; i < numberOfBanknotes; i++) { 
                Vector2 spawnPosition = (Vector2)transform.position + (Random.insideUnitCircle * 0.2f);
                GameObject moneyInstance = Instantiate(parent.moneyPrefab, spawnPosition, Quaternion.identity);
                MoneyMover mover = moneyInstance.GetComponent<MoneyMover>(); 
                Transform moneyTarget = (cashier.assignedServicePoint != null) ? cashier.assignedServicePoint.documentPointOnDesk : cashier.transform;
                if (mover != null) { mover.StartMove(moneyTarget); }
                yield return new WaitForSeconds(delayBetweenSpawns);
            } 
        } 
        
        if (PlayerWallet.Instance != null && parent != null) { 
            PlayerWallet.Instance.AddMoney(parent.billToPay, transform.position);
            if (parent.paymentSound != null) AudioSource.PlayClipAtPoint(parent.paymentSound, transform.position);
            parent.billToPay = 0; 
        } 
        cashier?.ServiceComplete();
        if (parent.mainGoal == ClientGoal.DirectorApproval)
        {
            LimitedCapacityZone receptionZone = ClientSpawner.Instance.directorReceptionZone;
            if (receptionZone != null && receptionZone.archiveDropOffStack != null)
            {
                receptionZone.archiveDropOffStack.AddDocumentToStack();
                Debug.Log("A copy of the director's document has been created in the reception for archiving.");
            }
        }
        
        parent.isLeavingSuccessfully = true;
        parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
        yield return StartCoroutine(ExitZoneAndSetNewGoal(ClientSpawner.Instance.exitWaypoint, ClientState.Leaving));
    }

    private IEnumerator PassedRegistrationRoutine() 
    {
        if (myQueueNumber != -1)
        {
            ClientQueueManager.Instance.RemoveClientFromQueue(parent);
        }

        if (parent.billToPay > 0)
        {
            SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
            SetState(ClientState.MovingToGoal);
            yield break;
        }

        DocumentType docType = parent.docHolder.GetCurrentDocumentType();
        ClientGoal goal = parent.mainGoal;
        Waypoint nextGoal = null;
        ClientState nextState = ClientState.MovingToGoal;

        switch (goal)
        {
            case ClientGoal.PayTax:
                nextGoal = ClientSpawner.Instance.exitWaypoint;
                parent.isLeavingSuccessfully = true;
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                break;
            case ClientGoal.GetCertificate1:
                nextGoal = (docType == DocumentType.Form1) ? ClientSpawner.GetDesk1Zone().waitingWaypoint : ClientSpawner.Instance.exitWaypoint;
                break;
            case ClientGoal.GetCertificate2:
                nextGoal = (docType == DocumentType.Form2) ? ClientSpawner.GetDesk2Zone().waitingWaypoint : ClientSpawner.Instance.exitWaypoint;
                break;
            default: 
                nextGoal = ClientSpawner.Instance.exitWaypoint;
                parent.isLeavingSuccessfully = true;
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                break;
        }

        if (nextGoal == ClientSpawner.Instance.exitWaypoint)
        {
            nextState = ClientState.Leaving;
            if (parent.reasonForLeaving == ClientPathfinding.LeaveReason.Normal) 
            {
                 parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            }
        }
        
        SetGoal(nextGoal);
        SetState(nextState);

        yield return null;
    }
    
    private IEnumerator HandleImpoliteArrival()
    {
        agentMover.Stop();
        ClientQueueManager.Instance.JoinQueue(parent);
        SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent));
        SetState(ClientState.MovingToGoal);
        yield return null;
    }
    
    private IEnumerator HandleCurrentState() 
    { 
        switch (currentState) 
        { 
            case ClientState.Spawning: 
                targetZone = null;
                if (Random.value < parent.prolazaFactor) { 
                    if (parent.impoliteSound != null) AudioSource.PlayClipAtPoint(parent.impoliteSound, transform.position);
                    Waypoint impoliteGoal = null;
                    switch (parent.mainGoal) { 
                        case ClientGoal.GetCertificate1: impoliteGoal = ClientSpawner.GetDesk1Zone().waitingWaypoint; break; 
                        case ClientGoal.GetCertificate2: impoliteGoal = ClientSpawner.GetDesk2Zone().waitingWaypoint; break;
                        case ClientGoal.PayTax: impoliteGoal = ClientSpawner.GetCashierZone().waitingWaypoint; break; 
                        case ClientGoal.AskAndLeave: impoliteGoal = ClientSpawner.GetRegistrationZone().insideWaypoints[0]; break;
                        case ClientGoal.VisitToilet: impoliteGoal = ClientSpawner.GetToiletZone().waitingWaypoint; break;
                    } 
                    if (impoliteGoal != null) { SetGoal(impoliteGoal); SetState(ClientState.MovingToRegistrarImpolite); break; } 
                } 
                Waypoint initialGoal = null;
                if (parent.mainGoal == ClientGoal.VisitToilet) { initialGoal = ClientSpawner.GetToiletZone().waitingWaypoint; } else { initialGoal = ClientQueueManager.Instance.ChooseNewGoal(parent); } if(initialGoal != null) { SetGoal(initialGoal); SetState(ClientState.MovingToGoal); } else { Debug.LogError($"Could not find an initial goal for {gameObject.name}. Is the waiting zone configured?"); SetState(ClientState.Confused); } 
                break;
            case ClientState.ReturningToRegistrar: 
                yield return StartCoroutine(MoveToGoalRoutine()); 
                if (currentState == ClientState.ReturningToRegistrar) { SetState(ClientState.AtRegistration); } 
                break;
            case ClientState.MovingToGoal: 
            case ClientState.GoingToCashier: 
            case ClientState.Leaving: 
            case ClientState.LeavingUpset: 
                yield return StartCoroutine(MoveToGoalRoutine()); 
                if (currentState == ClientState.MovingToGoal) { HandleArrivalAfterMove(); } 
                else if (currentState == ClientState.GoingToCashier) { SetState(ClientState.AtCashier); } 
                else if (currentState == ClientState.Leaving || currentState == ClientState.LeavingUpset) { parent.OnClientExit(); } 
                break;
            case ClientState.MovingToRegistrarImpolite: 
                yield return StartCoroutine(MoveToGoalRoutine()); 
                if (currentState == ClientState.MovingToRegistrarImpolite) { yield return StartCoroutine(HandleImpoliteArrival()); } 
                break;
            case ClientState.MovingToSeat: 
                if (seatTarget != null) { parent.movement.StartStuckCheck(); agentMover.SetPath(BuildPathTo(seatTarget.position)); yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck(); } 
                SetState(ClientState.SittingInWaitingArea); 
                break;
            case ClientState.AtLimitedZoneEntrance: 
                yield return StartCoroutine(EnterZoneRoutine()); 
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
            case ClientState.AtDesk1: 
                yield return StartCoroutine(OfficeServiceRoutine(ClientSpawner.GetDesk1Zone())); 
                break;
            case ClientState.AtDesk2: 
                yield return StartCoroutine(OfficeServiceRoutine(ClientSpawner.GetDesk2Zone()));
                break;
            case ClientState.AtCashier: 
                yield return StartCoroutine(PayBillRoutine()); 
                break;
            case ClientState.AtRegistration: 
                yield return StartCoroutine(RegistrationServiceRoutine()); 
                break;
            case ClientState.Confused: 
                yield return StartCoroutine(ConfusedRoutine()); 
                break;
            case ClientState.Enraged: 
                yield return StartCoroutine(EnragedRoutine()); 
                break;
            case ClientState.PassedRegistration: 
                yield return StartCoroutine(PassedRegistrationRoutine()); 
                break;
            default: 
                yield return null; 
                break;
        } 
        mainActionCoroutine = null;
    }

    private IEnumerator OfficeServiceRoutine(LimitedCapacityZone zone) 
    { 
        if (currentState == ClientState.AtDesk1 || currentState == ClientState.AtDesk2) { 
            targetZone = zone;
            SetState(ClientState.AtLimitedZoneEntrance); 
            yield return StartCoroutine(EnterZoneRoutine()); 
            if (currentState != ClientState.InsideLimitedZone) yield break; 
        } else { 
            SetState(ClientState.Confused); 
            yield break;
        } 
    }

    private IEnumerator PatienceMonitorForZone(LimitedCapacityZone zone) 
    { 
        yield return new WaitForSeconds(zonePatienceTime); 
        if (currentState == ClientState.AtLimitedZoneEntrance) { 
            zone.LeaveQueue(parent.gameObject);
            if (parent.puddlePrefabs != null && parent.puddlePrefabs.Count > 0 && zone == ClientSpawner.GetToiletZone() && Random.value < puddleChanceOnUpset) { 
                if (MessManager.Instance.CanCreateMess()) { 
                    GameObject randomPuddlePrefab = parent.puddlePrefabs[Random.Range(0, parent.puddlePrefabs.Count)];
                    Instantiate(randomPuddlePrefab, transform.position, Quaternion.identity); 
                } 
            } 
            if (Random.value < 0.5f) { SetState(ClientState.Enraged); } 
            else { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; SetState(ClientState.LeavingUpset); } 
        } 
    }

    private IEnumerator AttemptToStealRoutine() 
    { 
        parent.GetVisuals()?.SetEmotion(Emotion.Sly); 
        yield return new WaitForSeconds(Random.Range(1f, 2f)); 
        GuardManager.Instance.ReportTheft(parent); 
        parent.reasonForLeaving = ClientPathfinding.LeaveReason.Theft;
        yield return StartCoroutine(ExitZoneAndSetNewGoal(ClientSpawner.Instance.exitWaypoint, ClientState.Leaving)); 
    }

    private IEnumerator ConfusedRoutine() 
    { 
        agentMover.Stop(); 
        isBeingHelped = false;
        while(!isBeingHelped) { 
            yield return new WaitForSeconds(3f); 
            if (isBeingHelped) break; 
            float recoveryChance = 0.3f * (1f - parent.babushkaFactor);
            float choice = Random.value; 
            if (choice < recoveryChance) { SetGoal(previousGoal); SetState(ClientState.MovingToGoal); yield break; } 
            else if (choice < recoveryChance + 0.2f) { SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); yield break; } 
        } 
    }

    private IEnumerator EnragedRoutine() 
    { 
        ClientQueueManager.Instance.AddAngryClient(parent);
        if (parent.trashPrefabs != null && parent.trashPrefabs.Count > 0) { 
            int trashCount = Random.Range(4, 8);
            for(int i = 0; i < trashCount; i++) { 
                if (MessManager.Instance.CanCreateMess()) { 
                    GameObject randomTrashPrefab = parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)];
                    Vector2 randomOffset = Random.insideUnitCircle * 0.5f; 
                    Instantiate(randomTrashPrefab, (Vector2)transform.position + randomOffset, Quaternion.identity); 
                } 
            } 
        } 
        var thoughtController = GetComponent<ThoughtBubbleController>();
        if (thoughtController != null) { 
            thoughtController.StopThinking(); 
            StartCoroutine(RageThinkLoop(thoughtController)); 
        } 
        float rageTimer = Time.time + rageDuration;
        while (Time.time < rageTimer) { 
            Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); 
            if(allWaypoints.Length > 0) { 
                Waypoint randomTarget = allWaypoints[Random.Range(0, allWaypoints.Length)]; 
                agentMover.SetPath(BuildPathTo(randomTarget.transform.position));
                yield return new WaitUntil(() => !agentMover.IsMoving() || Vector2.Distance(transform.position, randomTarget.transform.position) < 2f); 
            } else { 
                yield return new WaitForSeconds(1f);
            } 
        } 
        parent.reasonForLeaving = ClientPathfinding.LeaveReason.Angry; 
        SetGoal(ClientSpawner.Instance.exitWaypoint); 
        SetState(ClientState.Leaving); 
    }

    private IEnumerator RageThinkLoop(ThoughtBubbleController controller) 
    { 
        while (GetCurrentState() == ClientState.Enraged) { 
            controller.TriggerCriticalThought($"Client_{ClientState.Enraged}");
            yield return new WaitForSeconds(Random.Range(2f, 4f)); 
        } 
    }

    private IEnumerator MillAroundAndWaitForSeat() 
    { 
        ClientQueueManager.Instance.StartPatienceTimer(parent);
        while (currentState == ClientState.AtWaitingArea) { 
            Transform freeSeat = ClientQueueManager.Instance.FindSeatForClient(parent); 
            if (freeSeat != null) { GoToSeat(freeSeat); yield break; } 
            Waypoint randomStandingPoint = ClientQueueManager.Instance.ChooseNewGoal(parent); 
            if(randomStandingPoint != null) { 
                SetGoal(randomStandingPoint); 
                yield return StartCoroutine(MoveToGoalRoutine()); 
            } 
            yield return new WaitForSeconds(Random.Range(2f, 4f));
        } 
    }

    public void GetHelpFromIntern(Waypoint newGoal = null) 
    { 
        if (parent.helpedByInternSound != null) AudioSource.PlayClipAtPoint(parent.helpedByInternSound, transform.position);
        if(currentState == ClientState.Confused) { 
            isBeingHelped = true; 
            SetGoal(newGoal ?? ClientQueueManager.Instance.ChooseNewGoal(parent)); 
            SetState(ClientState.MovingToGoal); 
        } else { 
            StopAllActionCoroutines();
            if(newGoal != null) { 
                if (newGoal == ClientSpawner.Instance.exitWaypoint) { 
                    parent.reasonForLeaving = ClientPathfinding.LeaveReason.Normal; 
                    SetState(ClientState.Leaving); 
                } else { 
                    SetState(ClientState.MovingToGoal); 
                } 
                ClientQueueManager.Instance.RemoveClientFromQueue(parent); 
                SetGoal(newGoal);
            } 
        } 
    }

    public Queue<Waypoint> BuildPathTo(Vector2 targetPos) 
    { 
        var newPath = new Queue<Waypoint>(); 
        Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        if (allWaypoints.Length == 0) return newPath; 
        Waypoint start = FindNearestVisibleWaypoint(transform.position, allWaypoints); 
        Waypoint goal = allWaypoints.FirstOrDefault(wp => Vector2.Distance(wp.transform.position, targetPos) < 0.01f);
        if (goal == null) { goal = FindNearestVisibleWaypoint(targetPos, allWaypoints); } 
        if (start == null) { start = FindNearestWaypoint(transform.position, allWaypoints); } 
        if (goal == null) { goal = FindNearestWaypoint(targetPos, allWaypoints); } 
        if (start == null || goal == null) { SetState(ClientState.Confused); return newPath; } 
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); 
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        PriorityQueue<Waypoint, float> queue = new PriorityQueue<Waypoint, float>(); 
        foreach (var wp in allWaypoints) { 
            distances[wp] = float.MaxValue; 
            previous[wp] = null;
        } 
        distances[start] = 0; 
        queue.Enqueue(start, 0); 
        while (queue.Count > 0) { 
            Waypoint current = queue.Dequeue();
            if (current == goal) { ReconstructPath(previous, goal, newPath); return newPath; } 
            if (current.neighbors == null || current.neighbors.Count == 0) continue;
            foreach (var neighbor in current.neighbors) { 
                if (neighbor == null) continue; 
                if (neighbor.type == Waypoint.WaypointType.StaffOnly && neighbor != ClientSpawner.Instance.exitWaypoint) continue;
                float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position); 
                if (distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { 
                    distances[neighbor] = newDist; 
                    previous[neighbor] = current;
                    queue.Enqueue(neighbor, newDist); 
                } 
            } 
        } 
        SetState(ClientState.Confused); 
        return newPath; 
    }

    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path) 
    { 
        List<Waypoint> pathList = new List<Waypoint>();
        for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } 
        pathList.Reverse(); 
        path.Clear();
        foreach (var wp in pathList) { path.Enqueue(wp); } 
    }

    private Waypoint FindNearestVisibleWaypoint(Vector2 position, Waypoint[] wps) 
    { 
        if(wps == null || wps.Length == 0) return null;
        Waypoint bestWaypoint = null; 
        float minDistance = float.MaxValue; 
        foreach (var wp in wps) { 
            if (wp.type == Waypoint.WaypointType.StaffOnly) continue;
            float distance = Vector2.Distance(position, wp.transform.position); 
            if (distance < minDistance) { 
                RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles"));
                if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } 
            } 
        } 
        return bestWaypoint;
    }

    private Waypoint FindNearestWaypoint(Vector2 p, Waypoint[] wps) 
    { 
        if(wps == null || wps.Length == 0) return null;
        return wps.Where(wp => wp.type != Waypoint.WaypointType.StaffOnly).OrderBy(wp => Vector2.Distance(p, wp.transform.position)).FirstOrDefault(); 
    }
    
    public ClientState GetCurrentState() => currentState;
    
    public Waypoint GetCurrentGoal() => currentGoal; 
    
    public void SetGoal(Waypoint g) => currentGoal = g;
    
    public string GetStatusInfo() 
    { 
        if (parent == null) return "No data";
        string traits = $"B: {parent.babushkaFactor:F2} | S: {parent.suetunFactor:F2} | P: {parent.prolazaFactor:F2}"; 
        string goal = $"Goal: {parent.mainGoal}"; 
        string statusText = "";
        string goalName = (currentGoal != null) ? currentGoal.name : (seatTarget != null ? seatTarget.name : "unknown");
        switch (currentState) { 
            case ClientState.MovingToGoal: 
            case ClientState.MovingToRegistrarImpolite: 
            case ClientState.GoingToCashier: 
            case ClientState.Leaving: 
            case ClientState.LeavingUpset: 
                statusText += $"Going to: {goalName}"; 
                break;
            case ClientState.MovingToSeat: 
                statusText += $"Going to seat: {seatTarget.name}"; 
                break; 
            case ClientState.AtWaitingArea: 
                statusText += "Waiting while standing"; 
                break;
            case ClientState.SittingInWaitingArea: 
                statusText += "Waiting while sitting"; 
                break; 
            case ClientState.AtRegistration: 
                statusText += "Being served at registration"; 
                break;
            case ClientState.AtDesk1: 
                statusText += "Being served at Desk 1"; 
                break; 
            case ClientState.AtDesk2: 
                statusText += "Being served at Desk 2"; 
                break;
            case ClientState.InsideLimitedZone: 
                statusText += $"In zone: {targetZone?.name}"; 
                break; 
            case ClientState.Enraged: 
                statusText += "ENRAGED!"; 
                break; 
            case ClientState.AtCashier: 
                statusText += "Paying";
                break; 
            case ClientState.AtLimitedZoneEntrance: 
                statusText += $"Waiting to enter zone: {targetZone?.name}"; 
                break; 
            default: 
                statusText += currentState.ToString(); 
                break;
        } 
        if (parent.docHolder != null) { 
            DocumentType docType = parent.docHolder.GetCurrentDocumentType(); 
            if (docType != DocumentType.None) { 
                statusText += $" (carrying {docType})";
            } 
        } 
        return $"{traits}\n{goal}\n{statusText}"; 
    }
    
    private class PriorityQueue<T, U> where U : System.IComparable<U> 
    { 
        private SortedDictionary<U, Queue<T>> dictionary = new SortedDictionary<U, Queue<T>>();
        public int Count => dictionary.Sum(p => p.Value.Count); 
        public void Enqueue(T item, U priority) { 
            if (!dictionary.ContainsKey(priority)) { 
                dictionary[priority] = new Queue<T>();
            } 
            dictionary[priority].Enqueue(item); 
        } 
        public T Dequeue() { 
            var pair = dictionary.First(); 
            T item = pair.Value.Dequeue();
            if (pair.Value.Count == 0) { 
                dictionary.Remove(pair.Key); 
            } 
            return item; 
        } 
    }
}