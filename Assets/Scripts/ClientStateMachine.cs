// Файл: Assets/Scripts/ClientStateMachine.cs - ФИНАЛЬНАЯ ВЕРСИЯ
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
    [SerializeField] private float clerkWaitTimeout = 20f; // Оставим это как тайм-аут, если сотрудник "завис"
    [Header("Mess Generation")]
    [SerializeField] private float baseTrashChancePerSecond = 0.01f;
    [SerializeField] private float puddleChanceInToilet = 0.2f;
    [SerializeField] private float puddleChanceOnUpset = 0.3f;
	
	private LimitedCapacityZone zoneToReturnTo;
    
    private ClientState currentState = ClientState.Spawning;
    private Waypoint currentGoal, previousGoal;
    private int myQueueNumber = -1;
	public int MyQueueNumber => myQueueNumber;
    
    private IServiceProvider myServiceProvider;
    public IServiceProvider MyServiceProvider => myServiceProvider;	
    
    private Coroutine mainActionCoroutine, zonePatienceCoroutine, millingCoroutine;
    private Transform seatTarget;
    private bool isBeingHelped = false;
    private LimitedCapacityZone targetZone;
	private Waypoint occupiedWaypoint = null;

    public void Initialize(ClientPathfinding p) 
    { 
        parent = p;
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
        if (parent.movement == null || agentMover == null) { enabled = false; return; } 
        StartCoroutine(MainLogicLoop());
        StartCoroutine(TrashGenerationRoutine());
    }
	
	public void DecideToVisitToilet()
{
    // This method is called by ClientQueueManager when a client's patience runs out.
    if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea)
    {
        // Stop whatever milling/waiting the client was doing.
        StopAllActionCoroutines();
        // Notify the queue manager that we're leaving our spot.
        ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent);
        
        // Remember where we were, in case we need to come back.
        previousGoal = currentGoal;
        
        // Set the new goal to the toilet zone entrance.
        SetGoal(ClientSpawner.GetToiletZone().waitingWaypoint);
        
        // Start moving towards the new goal.
        SetState(ClientState.MovingToGoal);
    }
}

    // Этот метод теперь вызывается извне (клерком или директором),
    // когда клиента нужно отправить за новым бланком.
	public void GoGetFormAndReturn()
    {
        // 1. Запоминаем, в какую зону нужно вернуться
        zoneToReturnTo = targetZone;
        // 2. Устанавливаем флаг для прыжка в очереди
        parent.hasBeenSentForRevision = true;
        // 3. Освобождаем текущее место у стойки
        if (targetZone != null)
        {
            targetZone.ReleaseWaypoint(GetCurrentGoal());
            Debug.Log($"<color=cyan>[ClientStateMachine]</color> Клиент {parent.name} ушел за бланком. Место {GetCurrentGoal().name} в зоне {targetZone.name} освобождено.");
        }
        // 4. Отправляем клиента к столу с бланками
        SetGoal(ClientSpawner.Instance.formTable.tableWaypoint);
        SetState(ClientState.MovingToGoal);
    }
    
    // ----- НАЧАЛО ИЗМЕНЕНИЙ: УДАЛЕНИЕ СТАРОЙ ЛОГИКИ -----
    // Корутины RegistrationServiceRoutine, PayBillRoutine и ServiceRoutineInsideZone
    // больше не нужны. Вся их логика теперь находится в Executor-скриптах сотрудников.
    // Вместо них мы будем использовать одну простую корутину ожидания.
    // ----- КОНЕЦ ИЗМЕНЕНИЙ -----

    private IEnumerator WaitForServiceRoutine()
    {
        Debug.Log($"[Client AI] {parent.name} находится в состоянии {currentState} и ждет обслуживания.");
        // Просто ждем, пока наше состояние не изменится извне (сотрудником).
        // Это может быть WaitingForDocument, Leaving, MovingToGoal и т.д.
        yield return new WaitUntil(() => 
            currentState != ClientState.AtRegistration && 
            currentState != ClientState.AtCashier &&
            currentState != ClientState.InsideLimitedZone
        );
        Debug.Log($"[Client AI] {parent.name} дождался! Новое состояние: {currentState}.");
    }

    private IEnumerator HandleCurrentState() 
    { 
        switch (currentState) 
        { 
            // --- Нижеследующие кейсы остаются без изменений ---
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
                if (parent.mainGoal == ClientGoal.VisitToilet) { initialGoal = ClientSpawner.GetToiletZone().waitingWaypoint; } else { initialGoal = ClientQueueManager.Instance.ChooseNewGoal(parent); } if(initialGoal != null) { SetGoal(initialGoal);
                SetState(ClientState.MovingToGoal); } else { Debug.LogError($"Could not find an initial goal for {gameObject.name}. Is the waiting zone configured?"); SetState(ClientState.Confused);
                } 
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
                else if (currentState == ClientState.GoingToCashier) { SetState(ClientState.AtCashier);
                } 
                else if (currentState == ClientState.Leaving || currentState == ClientState.LeavingUpset) { parent.OnClientExit();
                } 
                break;
            case ClientState.MovingToRegistrarImpolite: 
                yield return StartCoroutine(MoveToGoalRoutine());
                if (currentState == ClientState.MovingToRegistrarImpolite) { yield return StartCoroutine(HandleImpoliteArrival()); } 
                break;
            case ClientState.MovingToSeat: 
                if (seatTarget != null) { parent.movement.StartStuckCheck();
                agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, seatTarget.position, this.gameObject)); yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck();
                } 
                SetState(ClientState.SittingInWaitingArea); 
                break;
            case ClientState.AtLimitedZoneEntrance: 
                yield return StartCoroutine(EnterZoneRoutine());
                break;
            
            // ----- НАЧАЛО ИЗМЕНЕНИЙ: КЛИЕНТ БОЛЬШЕ НЕ АКТИВЕН -----
            // Теперь в этих состояниях клиент просто ждет, пока сотрудник не изменит его состояние
            case ClientState.AtRegistration:
            case ClientState.AtCashier:
            case ClientState.InsideLimitedZone: 
                yield return StartCoroutine(WaitForServiceRoutine());
                break;
            // ----- КОНЕЦ ИЗМЕНЕНИЙ -----

            // --- Нижеследующие кейсы остаются без изменений ---
            case ClientState.SittingInWaitingArea: 
                ClientQueueManager.Instance.StartPatienceTimer(parent);
                yield return new WaitUntil(() => currentState != ClientState.SittingInWaitingArea); 
                break;
            case ClientState.AtWaitingArea: 
                millingCoroutine = StartCoroutine(MillAroundAndWaitForSeat());
                yield return new WaitUntil(() => currentState != ClientState.AtWaitingArea); 
                break;
            case ClientState.Confused: 
                yield return StartCoroutine(ConfusedRoutine());
                break;
            case ClientState.Enraged: 
                yield return StartCoroutine(EnragedRoutine());
                break;
			case ClientState.WaitingForDocument:
				parent.GetVisuals()?.SetEmotion(Emotion.Waiting);
				yield return new WaitUntil(() => currentState != ClientState.WaitingForDocument);
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

    // --- Логика остальных корутин остается без изменений ---
    // (EnterZoneRoutine, GetFormFromTableRoutine, ConfusedRoutine и т.д. остаются здесь)
    #region Unchanged Coroutines and Methods
    private IEnumerator GetFormFromTableRoutine()
    {
        yield return new WaitForSeconds(Random.Range(2f, 4f));
        DocumentType docToGet = DocumentType.None;
        if (parent.mainGoal == ClientGoal.GetCertificate1) { docToGet = DocumentType.Form1; }
        else if (parent.mainGoal == ClientGoal.GetCertificate2) { docToGet = DocumentType.Form2; }

        if (docToGet != DocumentType.None)
        {
            parent.docHolder.SetDocument(docToGet);
        }

        if (zoneToReturnTo != null && zoneToReturnTo.waitingWaypoint != null)
        {
            SetGoal(zoneToReturnTo.waitingWaypoint);
            zoneToReturnTo = null; 
        }
        else
        {
            Debug.LogWarning($"Клиент {parent.name} взял бланк, но не знает, в какую зону вернуться. Идет в общую очередь.");
            SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent));
        }
        SetState(ClientState.MovingToGoal);
    }
	
	
    private IEnumerator EnterZoneRoutine()
    {
        // 1. Check if a target zone is set. If not, the client gets confused.
        if (targetZone == null)
        {
            SetState(ClientState.Confused);
            yield break; // Exit the coroutine
        }

        // 2. Stop any current movement.
        agentMover.Stop();

        // 3. Handle queue joining or jumping.
        // If the client was sent back for revision, they jump to the front.
        if (parent.hasBeenSentForRevision)
        {
            targetZone.JumpQueue(parent.gameObject);
            parent.hasBeenSentForRevision = false; // Reset the flag
        }
        else // Otherwise, join the regular queue.
        {
            targetZone.JoinQueue(parent.gameObject);
        }

        // 4. Start monitoring patience for this zone.
        zonePatienceCoroutine = StartCoroutine(PatienceMonitorForZone(targetZone));

        // 5. Wait until this client is the first in the queue.
        yield return new WaitUntil(() => targetZone.IsFirstInQueue(parent.gameObject));

        // 6. Loop until a free spot inside the zone is found and occupied.
        Waypoint freeSpot = null;
        while (freeSpot == null)
        {
            // Check if the client's state changed while waiting (e.g., got confused, left).
            if (currentState != ClientState.AtLimitedZoneEntrance)
            {
                // If state changed, ensure the patience monitor is stopped if it's still running.
                if (zonePatienceCoroutine != null)
                {
                    StopCoroutine(zonePatienceCoroutine);
                    zonePatienceCoroutine = null;
                }
                yield break; // Exit the coroutine
            }


            // Determine the desk ID associated with this zone.
            int deskId = GetDeskIdFromZone(targetZone);
            bool canEnterDirectly = false; // Flag to allow entry

            // Check if entry is allowed:
            // - If the zone has no ID (like a toilet), entry is allowed if space permits.
            // - If the zone has an ID, check if the corresponding service provider is available.
            if (deskId == int.MinValue) // Zone without a specific service provider (e.g., toilet)
            {
                canEnterDirectly = true; // Allow entry if space is available
            }
            else // Zone with a service provider (desk, cashier)
            {
                IServiceProvider provider = ClientSpawner.GetServiceProviderAtDesk(deskId);
                // Allow entry if a provider exists and is available.
                if (provider != null && provider.IsAvailableToServe)
                {
                    canEnterDirectly = true;
                }
            }

            // If entry is allowed (based on the checks above), try to request and occupy a spot.
            if (canEnterDirectly)
            {
                // Attempt to get a free waypoint inside the zone.
                freeSpot = targetZone.RequestAndOccupyWaypoint(parent.gameObject);
                if (freeSpot != null)
                {
                     // Successfully got a spot! Log it.
                    Debug.Log($"<color=lightblue>[EnterZone]</color> {parent.name} входит в зону '{targetZone.name}' (Место: {freeSpot.name}).");
                }
            }

            // If a spot was NOT found (either entry not allowed yet, or zone is full)
            if (freeSpot == null)
            {
                // Wait briefly before checking again.
                yield return new WaitForSeconds(0.5f);
            }
        } // End of the while loop (spot has been found)

        // 7. Spot found! Clean up: leave the waiting queue and stop the patience monitor.
        targetZone.LeaveQueue(parent.gameObject);
        if (zonePatienceCoroutine != null)
        {
            StopCoroutine(zonePatienceCoroutine);
            zonePatienceCoroutine = null; // Reset coroutine reference
        }

        // 8. Set the occupied spot as the client's next goal and change state to move towards it.
        SetGoal(freeSpot);
        SetState(ClientState.MovingToGoal);

        // 9. Special logic for Director Approval goal when entering the reception zone.
        if (parent.mainGoal == ClientGoal.DirectorApproval && targetZone == ClientSpawner.Instance.directorReceptionZone)
        {
            StartOfDayPanel directorsDesk = StartOfDayPanel.Instance;
            if (directorsDesk != null)
            {
                // Register the document with the director's desk UI.
                directorsDesk.RegisterDirectorDocument(parent);
            }
        }
    } // End of EnterZoneRoutine
	
	
    private IEnumerator HandleImpoliteArrival() { agentMover.Stop(); ClientQueueManager.Instance.JoinQueue(parent); SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); yield return null;
    }
    private IEnumerator ConfusedRoutine() { agentMover.Stop(); isBeingHelped = false;
        while(!isBeingHelped) { yield return new WaitForSeconds(3f); if (isBeingHelped) break; float recoveryChance = 0.3f * (1f - parent.babushkaFactor);
        float choice = Random.value; if (choice < recoveryChance) { SetGoal(previousGoal); SetState(ClientState.MovingToGoal); yield break;
        } else if (choice < recoveryChance + 0.2f) { SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); yield break;
        } } }
    public void StopAllActionCoroutines() { if (mainActionCoroutine != null) StopCoroutine(mainActionCoroutine);
        mainActionCoroutine = null; if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine); zonePatienceCoroutine = null; if (millingCoroutine != null) StopCoroutine(millingCoroutine); millingCoroutine = null;
    }
    public IEnumerator MainLogicLoop() { yield return new WaitForSeconds(0.1f); while (true) { if (parent.notification != null) parent.notification.UpdateNotification();
        if (mainActionCoroutine == null) { mainActionCoroutine = StartCoroutine(HandleCurrentState()); } yield return null;
    } }
    private IEnumerator MoveToGoalRoutine() { if (currentGoal != null) { parent.movement.StartStuckCheck(); agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, currentGoal.transform.position, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck(); } else { Debug.LogWarning($"{name} wanted to move, but currentGoal was not set."); SetState(ClientState.Confused);
    } }
    public ClientState GetCurrentState() => currentState; public Waypoint GetCurrentGoal() => currentGoal;
    public void SetGoal(Waypoint g) => currentGoal = g;
    public LimitedCapacityZone GetTargetZone() { return targetZone; }
    private int GetDeskIdFromZone(LimitedCapacityZone zone)
    {
        if (zone == null) return int.MinValue;
        if (zone == ClientSpawner.GetDesk1Zone()) return 1;
        if (zone == ClientSpawner.GetDesk2Zone()) return 2;
        if (zone == ClientSpawner.GetCashierZone()) return -1;
        if (zone == ClientSpawner.GetRegistrationZone()) return 0;
        return int.MinValue;
    }

    public void GetCalledToSpecificDesk(Waypoint destination, int queueNumber, IServiceProvider provider) 
    { 
        StopAllActionCoroutines();
        ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); 
        myQueueNumber = queueNumber; 
        myServiceProvider = provider;
        SetGoal(destination);
        // ----- ИЗМЕНЕНИЕ: Просто идем к цели, не меняя состояние на специфическое -----
        SetState(ClientState.MovingToGoal); 
    }
    
    // ----- ИЗМЕНЕНИЕ: Этот метод теперь вызывается, когда мы подходим к любой точке назначения -----
    private void HandleArrivalAfterMove()
{
    Waypoint dest = GetCurrentGoal();
    if (dest == null) { SetState(ClientState.Confused); return; }

    // --- НОВАЯ УНИВЕРСАЛЬНАЯ ПРОВЕРКА ---
    // Проверяем, является ли точка, куда мы пришли, местом обслуживания (например, стул у стола клерка)
    if (dest.isServicePoint)
    {
        // Находим зону, к которой относится эта точка
        var owningZone = dest.GetComponentInParent<LimitedCapacityZone>();
        if (owningZone != null)
        {
            // "Прописываемся" в этой зоне на этом конкретном месте
            owningZone.ManuallyOccupyWaypoint(dest, parent.gameObject);
            
            // Запоминаем, в какой зоне мы теперь находимся
            targetZone = owningZone;
			
			occupiedWaypoint = dest; // Запоминаем, что мы заняли эту точку
            
            // Теперь мы внутри и готовы к обслуживанию
            SetState(ClientState.InsideLimitedZone);
            return;
        }
    }
    // --- КОНЕЦ НОВОЙ ПРОВЕРКИ ---

    // Проверяем, не прибыли ли мы ко входу в очередь зоны
    var newTargetZone = FindObjectsByType<LimitedCapacityZone>(FindObjectsSortMode.None).FirstOrDefault(z => z.waitingWaypoint == dest);
    if (newTargetZone != null)
    {
        targetZone = newTargetZone;
        SetState(ClientState.AtLimitedZoneEntrance);
        return;
    }
    
    // (Остальная часть метода для сидений, стола с бланками и т.д. остается без изменений)
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
    
    Debug.LogWarning($"{name} прибыл в {dest.name}, но не знает, что делать дальше.");
    SetState(ClientState.Confused);
}
    public void SetState(ClientState newState)
    {
        if (newState == currentState) return;
        bool wasInsideZone = currentState == ClientState.InsideLimitedZone || currentState == ClientState.AtLimitedZoneEntrance || currentState == ClientState.AtRegistration || currentState == ClientState.AtDesk1 || currentState == ClientState.AtDesk2 || currentState == ClientState.AtCashier;
        bool willBeOutsideZone = newState != ClientState.InsideLimitedZone;
        if (wasInsideZone && willBeOutsideZone)
        {
            if (targetZone != null)
            {
                targetZone.LeaveQueue(parent.gameObject);
                // targetZone.ReleaseWaypoint(GetCurrentGoal());
				targetZone.ReleaseWaypoint(occupiedWaypoint);
				occupiedWaypoint = null;
                Debug.Log($"<color=orange>[StateMachine Cleanup]</color> Client {parent.name} left zone '{targetZone.name}' from state '{currentState}'. Waypoint released.");
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
        else { visuals.SetEmotionForState(newState); }
    }
    // ... (остальные неизменные корутины и методы)
    private IEnumerator TrashGenerationRoutine() { while (true) { if (currentState != ClientState.Leaving && currentState != ClientState.LeavingUpset && parent.trashPrefabs != null && parent.trashPrefabs.Count > 0) { if (MessManager.Instance.CanCreateMess()) { float chance = baseTrashChancePerSecond + (parent.suetunFactor * 0.02f) - (parent.babushkaFactor * 0.005f); if (Random.value < chance) { GameObject randomTrashPrefab = parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)]; Vector2 spawnPosition = (Vector2)transform.position; TrashCan closestCan = null; float minDistance = float.MaxValue; foreach (var can in TrashCan.AllTrashCans) { if (can.IsFull) continue; float distance = Vector2.Distance(transform.position, can.transform.position); if (distance < minDistance) { minDistance = distance; closestCan = can; } } if (closestCan != null && minDistance <= closestCan.attractionRadius) { spawnPosition = (Vector2)closestCan.transform.position + (Random.insideUnitCircle * 0.5f); closestCan.AddTrash(); } else { spawnPosition += Random.insideUnitCircle * 0.2f; } Instantiate(randomTrashPrefab, spawnPosition, Quaternion.identity); } } } yield return new WaitForSeconds(1f); } }
    public void GoToSeat(Transform seat) { StopAllActionCoroutines(); seatTarget = seat; SetState(ClientState.MovingToSeat); }
    public string GetStatusInfo() { if (parent == null) return "No data"; string traits = $"B: {parent.babushkaFactor:F2} | S: {parent.suetunFactor:F2} | P: {parent.prolazaFactor:F2}"; string goal = $"Goal: {parent.mainGoal}"; string statusText = ""; string goalName = (currentGoal != null) ? currentGoal.name : (seatTarget != null ? seatTarget.name : "unknown"); switch (currentState) { case ClientState.MovingToGoal: case ClientState.MovingToRegistrarImpolite: case ClientState.GoingToCashier: case ClientState.Leaving: case ClientState.LeavingUpset: statusText += $"Going to: {goalName}"; break; case ClientState.MovingToSeat: statusText += $"Going to seat: {seatTarget.name}"; break; case ClientState.AtWaitingArea: statusText += "Waiting while standing"; break; case ClientState.SittingInWaitingArea: statusText += "Waiting while sitting"; break; case ClientState.AtRegistration: statusText += $"Being served by {(myServiceProvider as MonoBehaviour)?.name ?? "unknown"}"; break; case ClientState.AtDesk1: statusText += "Being served at Desk 1"; break; case ClientState.AtDesk2: statusText += "Being served at Desk 2"; break; case ClientState.InsideLimitedZone: statusText += $"In zone: {targetZone?.name}, waiting for service"; break; case ClientState.Enraged: statusText += "ENRAGED!"; break; case ClientState.AtCashier: statusText += "Paying"; break; case ClientState.AtLimitedZoneEntrance: statusText += $"Waiting to enter zone: {targetZone?.name}"; break; default: statusText += currentState.ToString(); break; } if (parent.docHolder != null) { DocumentType docType = parent.docHolder.GetCurrentDocumentType(); if (docType != DocumentType.None) { statusText += $" (carrying {docType})"; } } return $"{traits}\n{goal}\n{statusText}"; }
    private IEnumerator PassedRegistrationRoutine() { if (myQueueNumber != -1) { ClientQueueManager.Instance.RemoveClientFromQueue(parent); } if (parent.billToPay > 0) { SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint); SetState(ClientState.MovingToGoal); yield break; } DocumentType docType = parent.docHolder.GetCurrentDocumentType(); ClientGoal goal = parent.mainGoal; Waypoint nextGoal = null; ClientState nextState = ClientState.MovingToGoal; switch (goal) { case ClientGoal.PayTax: nextGoal = ClientSpawner.Instance.exitWaypoint; parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; break; case ClientGoal.GetCertificate1: nextGoal = (docType == DocumentType.Form1) ? ClientSpawner.GetDesk1Zone().waitingWaypoint : ClientSpawner.Instance.exitWaypoint; break; case ClientGoal.GetCertificate2: nextGoal = (docType == DocumentType.Form2) ? ClientSpawner.GetDesk2Zone().waitingWaypoint : ClientSpawner.Instance.exitWaypoint; break; default: nextGoal = ClientSpawner.Instance.exitWaypoint; parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; break; } if (nextGoal == ClientSpawner.Instance.exitWaypoint) { nextState = ClientState.Leaving; if (parent.reasonForLeaving == ClientPathfinding.LeaveReason.Normal) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; } } SetGoal(nextGoal); SetState(nextState); yield return null; }
    private IEnumerator PatienceMonitorForZone(LimitedCapacityZone zone) { yield return new WaitForSeconds(zonePatienceTime); if (currentState == ClientState.AtLimitedZoneEntrance) { zone.LeaveQueue(parent.gameObject); if (parent.puddlePrefabs != null && parent.puddlePrefabs.Count > 0 && zone == ClientSpawner.GetToiletZone() && Random.value < puddleChanceOnUpset) { if (MessManager.Instance.CanCreateMess()) { GameObject randomPuddlePrefab = parent.puddlePrefabs[Random.Range(0, parent.puddlePrefabs.Count)]; Instantiate(randomPuddlePrefab, transform.position, Quaternion.identity); } } if (Random.value < 0.5f) { SetState(ClientState.Enraged); } else { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; SetState(ClientState.LeavingUpset); } } }
    private IEnumerator EnragedRoutine() { GuardManager.Instance.ReportViolator(parent); ClientQueueManager.Instance.AddAngryClient(parent); if (parent.trashPrefabs != null && parent.trashPrefabs.Count > 0) { int trashCount = Random.Range(4, 8); for(int i = 0; i < trashCount; i++) { if (MessManager.Instance.CanCreateMess()) { GameObject randomTrashPrefab = parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)]; Vector2 randomOffset = Random.insideUnitCircle * 0.5f; Instantiate(randomTrashPrefab, (Vector2)transform.position + randomOffset, Quaternion.identity); } } } var thoughtController = GetComponent<ThoughtBubbleController>(); if (thoughtController != null) { thoughtController.StopThinking(); StartCoroutine(RageThinkLoop(thoughtController)); } float rageTimer = Time.time + rageDuration; while (Time.time < rageTimer) { Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); if(allWaypoints.Length > 0) { Waypoint randomTarget = allWaypoints[Random.Range(0, allWaypoints.Length)]; agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, currentGoal.transform.position, this.gameObject)); yield return new WaitUntil(() => !agentMover.IsMoving() || Vector2.Distance(transform.position, randomTarget.transform.position) < 2f); } else { yield return new WaitForSeconds(1f); } } parent.reasonForLeaving = ClientPathfinding.LeaveReason.Angry; SetGoal(ClientSpawner.Instance.exitWaypoint); SetState(ClientState.Leaving); }
    private IEnumerator RageThinkLoop(ThoughtBubbleController controller) { while (GetCurrentState() == ClientState.Enraged) { controller.TriggerCriticalThought($"Client_{ClientState.Enraged}"); yield return new WaitForSeconds(Random.Range(2f, 4f)); } }
    private IEnumerator MillAroundAndWaitForSeat() { ClientQueueManager.Instance.StartPatienceTimer(parent); while (currentState == ClientState.AtWaitingArea) { Transform freeSeat = ClientQueueManager.Instance.FindSeatForClient(parent); if (freeSeat != null) { GoToSeat(freeSeat); yield break; } Waypoint randomStandingPoint = ClientQueueManager.Instance.ChooseNewGoal(parent); if(randomStandingPoint != null) { SetGoal(randomStandingPoint); yield return StartCoroutine(MoveToGoalRoutine()); } yield return new WaitForSeconds(Random.Range(2f, 4f)); } }
    public void GetHelpFromIntern(Waypoint newGoal = null) { if (parent.helpedByInternSound != null) AudioSource.PlayClipAtPoint(parent.helpedByInternSound, transform.position); if(currentState == ClientState.Confused) { isBeingHelped = true; SetGoal(newGoal ?? ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); } else { StopAllActionCoroutines(); if(newGoal != null) { if (newGoal == ClientSpawner.Instance.exitWaypoint) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Normal; SetState(ClientState.Leaving); } else { SetState(ClientState.MovingToGoal); } ClientQueueManager.Instance.RemoveClientFromQueue(parent); SetGoal(newGoal); } } }
    #endregion
}