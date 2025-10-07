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
	
	private LimitedCapacityZone zoneToReturnTo;
    
    private ClientState currentState = ClientState.Spawning;
    private Waypoint currentGoal, previousGoal;
    private int myQueueNumber = -1;
	public int MyQueueNumber => myQueueNumber; // Публичное свойство для чтения приватного поля
    
    // --- ИЗМЕНЕНИЕ: Заменяем ClerkController на универсальный IServiceProvider ---
    private IServiceProvider myServiceProvider;
	public IServiceProvider MyServiceProvider => myServiceProvider;	
    
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

    private IEnumerator RegistrationServiceRoutine() 
    {
        IServiceProvider servingProvider = myServiceProvider;
        if (servingProvider == null)
        {
             SetState(ClientState.Confused);
             yield break;
        }
        
        float waitTimer = 0f;
        while (servingProvider == null || !servingProvider.IsAvailableToServe)
        {
            yield return new WaitForSeconds(0.5f);
            waitTimer += 0.5f;
            if (waitTimer > clerkWaitTimeout) { SetState(ClientState.Confused); yield break; }
            servingProvider = myServiceProvider;
        }

        // --- ОБНОВЛЕНИЕ: Проверяем, является ли наш работник Клерком, чтобы получить доступ к его навыкам ---
        if (servingProvider is ClerkController servingClerk)
{
    // >>> НАЧАЛО НОВОЙ ЛОГИКИ ШАНСОВ <<<

    Waypoint correctDestination = DetermineCorrectGoalAfterRegistration();
    Waypoint actualDestination = correctDestination; // По умолчанию считаем, что направили верно

    // 1. Базовый шанс на успех и модификаторы
    float baseSuccessChance = 0.7f; // 70% базовый шанс
    float clientModifier = (parent.babushkaFactor * 0.1f) - (parent.suetunFactor * 0.2f); // Влияние характера клиента
    float registrarBonus = servingClerk.redirectionBonus; // Бонус от действия "Патруль на стуле"

    // 2. Считаем итоговый шанс, ограничивая его рамками 30%-95%
    float finalChance = baseSuccessChance + clientModifier + registrarBonus;
    finalChance = Mathf.Clamp(finalChance, 0.3f, 0.95f);

    // 3. Бросаем кубик
    if (Random.value > finalChance)
    {
        // ПРОВАЛ! Выбираем случайную неверную цель
        Debug.LogWarning($"[Registration] ПРОВАЛ НАПРАВЛЕНИЯ! Регистратор {servingClerk.name} ошибся с клиентом {parent.name}. Шанс был {finalChance:P0}");
        List<Waypoint> possibleDestinations = new List<Waypoint> 
        { 
            ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category1DeskZones)?.waitingWaypoint,
            ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category2DeskZones)?.waitingWaypoint,
            ClientSpawner.GetQuietestZone(ClientSpawner.Instance.cashierZones)?.waitingWaypoint,
            ClientSpawner.Instance.toiletZone.waitingWaypoint,
            ClientQueueManager.Instance.ChooseNewGoal(parent) // Обратно в общую очередь
        };

        // Убираем из списка null и правильную цель, чтобы не отправить туда же
        possibleDestinations.RemoveAll(item => item == null || item == correctDestination);

        if (possibleDestinations.Count > 0)
        {
            actualDestination = possibleDestinations[Random.Range(0, possibleDestinations.Count)];
        }
    }

    // >>> КОНЕЦ НОВОЙ ЛОГИКИ ШАНСОВ <<<

    string destinationName = string.IsNullOrEmpty(actualDestination.friendlyName) ?
        actualDestination.name : actualDestination.friendlyName;
    string directionMessage = $"Пройдите, пожалуйста, к\n'{destinationName}'";
    servingClerk.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage(directionMessage, 3f, Color.white);

    Debug.Log($"[Registration] Client {parent.name} directed to {actualDestination.name}.");

    // Создаем отчет об обслуживании
    servingClerk.ServiceComplete();
    Debug.Log($"[Registration] {servingClerk.name} создал отчет об обслуживании клиента {parent.name}.");

    if (myQueueNumber != -1) { ClientQueueManager.Instance.RemoveClientFromQueue(parent); }
    SetGoal(actualDestination);
    SetState(ClientState.MovingToGoal);
}
        else if (servingProvider is DirectorAvatarController director)
        {
            // --- НОВАЯ ЛОГИКА: Что делает Директор в роли Регистратора ---
            director.AssignClient(parent); // Передаем клиента Директору для обслуживания
            // Директор сам разберется с клиентом, а мы просто ждем
            yield return new WaitUntil(() => parent.stateMachine.GetCurrentState() != ClientState.AtRegistration);
        }
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
            
            // --- ИЗМЕНЕНИЕ: Ищем IServiceProvider вместо ClerkController ---
            IServiceProvider serviceProvider = ClientSpawner.GetServiceProviderAtDesk(deskId);
            while (serviceProvider == null || !serviceProvider.IsAvailableToServe || Vector2.Distance((serviceProvider as MonoBehaviour).transform.position, serviceProvider.GetWorkstation().clerkStandPoint.position) > requiredServiceDistance) 
            { 
                yield return new WaitForSeconds(0.5f);
                serviceProvider = ClientSpawner.GetServiceProviderAtDesk(deskId); 
                waitTimer += 0.5f; 
                if (waitTimer > clerkWaitTimeout) { yield return StartCoroutine(ExitZoneAndSetNewGoal(null, ClientState.Confused)); yield break; } 
            }
			
            // Передаем клиента на обслуживание
            serviceProvider.AssignClient(parent);

            // Если это клерк, выполняем старую сложную логику
            if (serviceProvider is ClerkController clerk)
            {
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
                clerk.GetComponent<ThoughtBubbleController>()?.ShowPriorityMessage("Please get a form\nfrom the table!", 3f, Color.yellow);
                yield return new WaitForSeconds(2f);

                // --- ИЗМЕНЕНИЕ НАЧАЛО ---
                parent.hasBeenSentForRevision = true; // Ставим флаг
                previousGoal = targetZone.waitingWaypoint; // Устанавливаем точку возврата - вход в очередь зоны
                // --- ИЗМЕНЕНИЕ КОНЕЦ ---

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
				
				DocumentQualityManager.Instance?.RegisterProcessedDocument(parent.documentQuality);

// Сообщаем клерку, что обслуживание завершено...
if (clerk != null && clerk.role == ClerkController.ClerkRole.Regular) 
{ 
    clerk.ServiceComplete();
} 
    yield return StartCoroutine(ExitZoneAndSetNewGoal(null, ClientState.PassedRegistration));
}
        } 
        else if (targetZone == ClientSpawner.GetCashierZone()) 
        {
			Debug.Log($"<color=yellow>КЛИЕНТ {parent.name}:</color> Нахожусь в зоне кассы. Запускаю процедуру оплаты (PayBillRoutine).");

            yield return StartCoroutine(PayBillRoutine());
        } 
        else if (targetZone == ClientSpawner.GetToiletZone()) 
        { 
            yield return StartCoroutine(ToiletRoutineInZone());
        } 
    }

    // ... (остальные методы до GetCalledToSpecificDesk без изменений)
    private IEnumerator ToiletRoutineInZone() { yield return new WaitForSeconds(Random.Range(5f, 10f)); if (parent.toiletSound != null) AudioSource.PlayClipAtPoint(parent.toiletSound, transform.position); if (parent.puddlePrefabs != null && parent.puddlePrefabs.Count > 0 && Random.value < puddleChanceInToilet) { if (MessManager.Instance.CanCreateMess()) { GameObject randomPuddlePrefab = parent.puddlePrefabs[Random.Range(0, parent.puddlePrefabs.Count)]; Instantiate(randomPuddlePrefab, transform.position, Quaternion.identity); } } Waypoint finalGoal; ClientState nextState; if (parent.mainGoal == ClientGoal.VisitToilet) { parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; finalGoal = ClientSpawner.Instance.exitWaypoint; nextState = ClientState.Leaving; } else { finalGoal = ClientQueueManager.Instance.GetToiletReturnGoal(parent); nextState = (finalGoal == ClientSpawner.Instance.exitWaypoint) ? ClientState.Leaving : ClientState.MovingToGoal; } yield return StartCoroutine(ExitZoneAndSetNewGoal(finalGoal, nextState)); }
    public void StopAllActionCoroutines() { if (mainActionCoroutine != null) StopCoroutine(mainActionCoroutine); mainActionCoroutine = null; if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine); zonePatienceCoroutine = null; if (millingCoroutine != null) StopCoroutine(millingCoroutine); millingCoroutine = null; }
    public void DecideToVisitToilet() { if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea) { StopAllActionCoroutines(); ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); previousGoal = currentGoal; SetGoal(ClientSpawner.GetToiletZone().waitingWaypoint); SetState(ClientState.MovingToGoal); } }
    public void GoToSeat(Transform seat) { StopAllActionCoroutines(); seatTarget = seat; SetState(ClientState.MovingToSeat); }
    public IEnumerator MainLogicLoop() { yield return new WaitForSeconds(0.1f); while (true) { if (parent.notification != null) parent.notification.UpdateNotification(); if (mainActionCoroutine == null) { mainActionCoroutine = StartCoroutine(HandleCurrentState()); } yield return null; } }
    private IEnumerator TrashGenerationRoutine()
{
    while (true)
    {
        if (currentState != ClientState.Leaving && currentState != ClientState.LeavingUpset && parent.trashPrefabs != null && parent.trashPrefabs.Count > 0)
        {
            if (MessManager.Instance.CanCreateMess())
            {
                // Шанс создать мусор зависит от характера
                float chance = baseTrashChancePerSecond + (parent.suetunFactor * 0.02f) - (parent.babushkaFactor * 0.005f);
                if (Random.value < chance)
                {
                    GameObject randomTrashPrefab = parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)];
                    Vector2 spawnPosition = (Vector2)transform.position;

                    // --- НАЧАЛО НОВОЙ ЛОГИКИ ---

                    TrashCan closestCan = null;
                    float minDistance = float.MaxValue;

                    // Ищем ближайший НЕПОЛНЫЙ мусорный бак
                    foreach (var can in TrashCan.AllTrashCans)
                    {
                        if (can.IsFull) continue;

                        float distance = Vector2.Distance(transform.position, can.transform.position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestCan = can;
                        }
                    }

                    // Если нашли бак в радиусе досягаемости
                    if (closestCan != null && minDistance <= closestCan.attractionRadius)
                    {
                        // Мусорим рядом с баком, а не под себя
                        spawnPosition = (Vector2)closestCan.transform.position + (Random.insideUnitCircle * 0.5f);
                        closestCan.AddTrash(); // Сообщаем баку, что в него "попали"
                        Debug.Log($"{parent.name} выбросил мусор рядом с баком {closestCan.name}.");
                    }
                    else
                    {
                        // Если баков нет или они далеко, мусорим под себя
                        spawnPosition += Random.insideUnitCircle * 0.2f;
                    }

                    // --- КОНЕЦ НОВОЙ ЛОГИКИ ---

                    Instantiate(randomTrashPrefab, spawnPosition, Quaternion.identity);
                }
            }
        }
        yield return new WaitForSeconds(1f);
    }
}
    private IEnumerator MoveToGoalRoutine() { if (currentGoal != null) { parent.movement.StartStuckCheck(); agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, currentGoal.transform.position, this.gameObject)); yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck(); } else { Debug.LogWarning($"{name} wanted to move, but currentGoal was not set."); SetState(ClientState.Confused); } }
	
private IEnumerator GetFormFromTableRoutine()
{
    yield return new WaitForSeconds(Random.Range(2f, 4f));

    // Определяем, какой бланк нужен
    DocumentType docToGet = DocumentType.None;
    if (parent.mainGoal == ClientGoal.GetCertificate1) { docToGet = DocumentType.Form1; }
    else if (parent.mainGoal == ClientGoal.GetCertificate2) { docToGet = DocumentType.Form2; }

    if (docToGet != DocumentType.None)
    {
        parent.docHolder.SetDocument(docToGet);
    }

    // --- ИЗМЕНЕНИЕ: Логика возвращения ---
    if (zoneToReturnTo != null && zoneToReturnTo.waitingWaypoint != null)
    {
        // Если мы помним, куда вернуться, идем ко входу в очередь этой зоны
        SetGoal(zoneToReturnTo.waitingWaypoint);
        zoneToReturnTo = null; // Очищаем память
    }
    else
    {
        // Аварийный случай: если не знаем, куда идти, идем в общую очередь
        Debug.LogWarning($"Клиент {parent.name} взял бланк, но не знает, в какую зону вернуться. Идет в общую очередь.");
        SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent));
    }

    SetState(ClientState.MovingToGoal); // Двигаемся к выбранной цели (входу в очередь)
}	
	private IEnumerator EnterZoneRoutine()
{
    if (targetZone == null) { SetState(ClientState.Confused); yield break; }
    agentMover.Stop();

    // --- ИЗМЕНЕНИЕ НАЧАЛО ---
    // Если у клиента есть флаг, что его отправили за бланком, он "перепрыгивает" очередь.
    if (parent.hasBeenSentForRevision)
    {
        targetZone.JumpQueue(parent.gameObject);
        parent.hasBeenSentForRevision = false; // Сбрасываем флаг, чтобы он не прыгал в очередь в следующий раз
    }
    else
    {
        targetZone.JoinQueue(parent.gameObject);
    }
    // --- ИЗМЕНЕНИЕ КОНЕЦ ---

    zonePatienceCoroutine = StartCoroutine(PatienceMonitorForZone(targetZone));
    yield return new WaitUntil(() => targetZone.IsFirstInQueue(parent.gameObject));
    Waypoint freeSpot = null;
    while (freeSpot == null)
    {
        // Если состояние изменилось, пока мы ждали (например, нас позвал стажер), выходим из цикла
        if (currentState != ClientState.AtLimitedZoneEntrance) yield break;

        // --- НАЧАЛО ИСПРАВЛЕНИЙ ---
        
        // 1. Узнаем ID стола, к которому мы стоим в очереди
        int deskId = GetDeskIdFromZone(targetZone);
        IServiceProvider provider = null;

        if (deskId != int.MinValue)
        {
            // 2. Спрашиваем у ClientSpawner, есть ли кто-то на этом месте
            provider = ClientSpawner.GetServiceProviderAtDesk(deskId);
        }
        
        // 3. Пытаемся занять место ТОЛЬКО ЕСЛИ сотрудник на месте и он доступен
        if (provider != null && provider.IsAvailableToServe)
        {
            freeSpot = targetZone.RequestAndOccupyWaypoint(parent.gameObject);
        }
        
        // --- КОНЕЦ ИСПРАВЛЕНИЙ ---

        // Если место занять не удалось (либо нет сотрудника, либо нет места), просто ждем
        if (freeSpot == null)
        {
            yield return new WaitForSeconds(0.5f);
        }
    }
    targetZone.LeaveQueue(parent.gameObject);
    if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine);
    zonePatienceCoroutine = null;
    SetGoal(freeSpot);
    SetState(ClientState.InsideLimitedZone);
    if (parent.mainGoal == ClientGoal.DirectorApproval && targetZone == ClientSpawner.Instance.directorReceptionZone)
    {
        StartOfDayPanel directorsDesk = StartOfDayPanel.Instance;
        if (directorsDesk != null)
        {
            directorsDesk.RegisterDirectorDocument(parent);
        }
    }
}
    private IEnumerator ExitZoneAndSetNewGoal(Waypoint finalDestination, ClientState stateAfterExit) { if (targetZone != null) { yield return new WaitForSeconds(1.0f); targetZone.ReleaseWaypoint(GetCurrentGoal()); if (targetZone.exitWaypoint != null) { SetGoal(targetZone.exitWaypoint); yield return StartCoroutine(MoveToGoalRoutine()); } } SetGoal(finalDestination); SetState(stateAfterExit); }
    private IEnumerator PassedRegistrationRoutine() { if (myQueueNumber != -1) { ClientQueueManager.Instance.RemoveClientFromQueue(parent); } if (parent.billToPay > 0) { SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint); SetState(ClientState.MovingToGoal); yield break; } DocumentType docType = parent.docHolder.GetCurrentDocumentType(); ClientGoal goal = parent.mainGoal; Waypoint nextGoal = null; ClientState nextState = ClientState.MovingToGoal; switch (goal) { case ClientGoal.PayTax: nextGoal = ClientSpawner.Instance.exitWaypoint; parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; break; case ClientGoal.GetCertificate1: nextGoal = (docType == DocumentType.Form1) ? ClientSpawner.GetDesk1Zone().waitingWaypoint : ClientSpawner.Instance.exitWaypoint; break; case ClientGoal.GetCertificate2: nextGoal = (docType == DocumentType.Form2) ? ClientSpawner.GetDesk2Zone().waitingWaypoint : ClientSpawner.Instance.exitWaypoint; break; default: nextGoal = ClientSpawner.Instance.exitWaypoint; parent.isLeavingSuccessfully = true; parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; break; } if (nextGoal == ClientSpawner.Instance.exitWaypoint) { nextState = ClientState.Leaving; if (parent.reasonForLeaving == ClientPathfinding.LeaveReason.Normal) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; } } SetGoal(nextGoal); SetState(nextState); yield return null; }
    private IEnumerator HandleImpoliteArrival() { agentMover.Stop(); ClientQueueManager.Instance.JoinQueue(parent); SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); yield return null; }
    private IEnumerator OfficeServiceRoutine(LimitedCapacityZone zone) { if (currentState == ClientState.AtDesk1 || currentState == ClientState.AtDesk2) { targetZone = zone; SetState(ClientState.AtLimitedZoneEntrance); yield return StartCoroutine(EnterZoneRoutine()); if (currentState != ClientState.InsideLimitedZone) yield break; } else { SetState(ClientState.Confused); yield break; } }
    private IEnumerator PatienceMonitorForZone(LimitedCapacityZone zone) { yield return new WaitForSeconds(zonePatienceTime); if (currentState == ClientState.AtLimitedZoneEntrance) { zone.LeaveQueue(parent.gameObject); if (parent.puddlePrefabs != null && parent.puddlePrefabs.Count > 0 && zone == ClientSpawner.GetToiletZone() && Random.value < puddleChanceOnUpset) { if (MessManager.Instance.CanCreateMess()) { GameObject randomPuddlePrefab = parent.puddlePrefabs[Random.Range(0, parent.puddlePrefabs.Count)]; Instantiate(randomPuddlePrefab, transform.position, Quaternion.identity); } } if (Random.value < 0.5f) { SetState(ClientState.Enraged); } else { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; SetState(ClientState.LeavingUpset); } } }
    private IEnumerator AttemptToStealRoutine() { parent.GetVisuals()?.SetEmotion(Emotion.Sly); yield return new WaitForSeconds(Random.Range(1f, 2f)); GuardManager.Instance.ReportTheft(parent); parent.reasonForLeaving = ClientPathfinding.LeaveReason.Theft; yield return StartCoroutine(ExitZoneAndSetNewGoal(ClientSpawner.Instance.exitWaypoint, ClientState.Leaving)); }
    private IEnumerator ConfusedRoutine() { agentMover.Stop(); isBeingHelped = false; while(!isBeingHelped) { yield return new WaitForSeconds(3f); if (isBeingHelped) break; float recoveryChance = 0.3f * (1f - parent.babushkaFactor); float choice = Random.value; if (choice < recoveryChance) { SetGoal(previousGoal); SetState(ClientState.MovingToGoal); yield break; } else if (choice < recoveryChance + 0.2f) { SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); yield break; } } }
    private IEnumerator EnragedRoutine() { GuardManager.Instance.ReportViolator(parent); ClientQueueManager.Instance.AddAngryClient(parent); if (parent.trashPrefabs != null && parent.trashPrefabs.Count > 0) { int trashCount = Random.Range(4, 8); for(int i = 0; i < trashCount; i++) { if (MessManager.Instance.CanCreateMess()) { GameObject randomTrashPrefab = parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)]; Vector2 randomOffset = Random.insideUnitCircle * 0.5f; Instantiate(randomTrashPrefab, (Vector2)transform.position + randomOffset, Quaternion.identity); } } } var thoughtController = GetComponent<ThoughtBubbleController>(); if (thoughtController != null) { thoughtController.StopThinking(); StartCoroutine(RageThinkLoop(thoughtController)); } float rageTimer = Time.time + rageDuration; while (Time.time < rageTimer) { Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); if(allWaypoints.Length > 0) { Waypoint randomTarget = allWaypoints[Random.Range(0, allWaypoints.Length)]; agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, currentGoal.transform.position, this.gameObject)); yield return new WaitUntil(() => !agentMover.IsMoving() || Vector2.Distance(transform.position, randomTarget.transform.position) < 2f); } else { yield return new WaitForSeconds(1f); } } parent.reasonForLeaving = ClientPathfinding.LeaveReason.Angry; SetGoal(ClientSpawner.Instance.exitWaypoint); SetState(ClientState.Leaving); }
    private IEnumerator RageThinkLoop(ThoughtBubbleController controller) { while (GetCurrentState() == ClientState.Enraged) { controller.TriggerCriticalThought($"Client_{ClientState.Enraged}"); yield return new WaitForSeconds(Random.Range(2f, 4f)); } }
    private IEnumerator MillAroundAndWaitForSeat() { ClientQueueManager.Instance.StartPatienceTimer(parent); while (currentState == ClientState.AtWaitingArea) { Transform freeSeat = ClientQueueManager.Instance.FindSeatForClient(parent); if (freeSeat != null) { GoToSeat(freeSeat); yield break; } Waypoint randomStandingPoint = ClientQueueManager.Instance.ChooseNewGoal(parent); if(randomStandingPoint != null) { SetGoal(randomStandingPoint); yield return StartCoroutine(MoveToGoalRoutine()); } yield return new WaitForSeconds(Random.Range(2f, 4f)); } }
    public void GetHelpFromIntern(Waypoint newGoal = null) { if (parent.helpedByInternSound != null) AudioSource.PlayClipAtPoint(parent.helpedByInternSound, transform.position); if(currentState == ClientState.Confused) { isBeingHelped = true; SetGoal(newGoal ?? ClientQueueManager.Instance.ChooseNewGoal(parent)); SetState(ClientState.MovingToGoal); } else { StopAllActionCoroutines(); if(newGoal != null) { if (newGoal == ClientSpawner.Instance.exitWaypoint) { parent.reasonForLeaving = ClientPathfinding.LeaveReason.Normal; SetState(ClientState.Leaving); } else { SetState(ClientState.MovingToGoal); } ClientQueueManager.Instance.RemoveClientFromQueue(parent); SetGoal(newGoal); } } }
    public ClientState GetCurrentState() => currentState; public Waypoint GetCurrentGoal() => currentGoal; public void SetGoal(Waypoint g) => currentGoal = g;

    // --- ИЗМЕНЕНИЕ: Метод GetCalledToSpecificDesk теперь принимает IServiceProvider ---
    public void GetCalledToSpecificDesk(Waypoint destination, int queueNumber, IServiceProvider provider) 
    { 
        StopAllActionCoroutines();
        ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent); 
        myQueueNumber = queueNumber; 
        myServiceProvider = provider; // Сохраняем ссылку на работника (неважно, Клерк или Директор)
        SetGoal(destination); 
        SetState(ClientState.MovingToGoal); 
    }

    public void SetState(ClientState newState)
{
    if (newState == currentState) return;

    // --- НАДЕЖНАЯ ПРОВЕРКА ДЛЯ ОСВОБОЖДЕНИЯ МЕСТА ---
    // Определяем, был ли клиент в любом состоянии, где он занимает место в зоне.
    bool wasInsideZone = currentState == ClientState.InsideLimitedZone ||
                         currentState == ClientState.AtLimitedZoneEntrance ||
                         currentState == ClientState.AtRegistration ||
                         currentState == ClientState.AtDesk1 ||
                         currentState == ClientState.AtDesk2 ||
                         currentState == ClientState.AtCashier;

    // Определяем, является ли новое состояние выходом из зоны.
    bool willBeOutsideZone = newState != ClientState.InsideLimitedZone;

    // Если клиент был внутри и теперь его состояние меняется на "внешнее", он должен освободить место.
    if (wasInsideZone && willBeOutsideZone)
    {
        if (targetZone != null)
        {
            targetZone.LeaveQueue(parent.gameObject);
            targetZone.ReleaseWaypoint(GetCurrentGoal());
            Debug.Log($"<color=orange>[StateMachine Cleanup]</color> Client {parent.name} left zone '{targetZone.name}' from state '{currentState}'. Waypoint released.");
        }
    }
    // --- КОНЕЦ ПРОВЕРКИ ---

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

    private void HandleArrivalAfterMove()
{
    Waypoint dest = GetCurrentGoal();
    if (dest == null) { SetState(ClientState.Confused); return; }

    if (myServiceProvider != null && myServiceProvider.GetWorkstation() != null && dest == myServiceProvider.GetWorkstation().clientStandPoint)
    {
        ClientQueueManager.Instance.ClientArrivedAtDesk(myQueueNumber);

        // --- ИСПРАВЛЕНИЕ НАЧАЛО: Используем новый надежный метод поиска зоны ---
        int deskId = myServiceProvider.GetWorkstation().deskId;
        targetZone = ClientSpawner.GetZoneByDeskId(deskId); 
        
        if (targetZone == null)
        {
            Debug.LogError($"[ClientStateMachine] Не удалось найти LimitedCapacityZone для deskId {deskId}! Клиент {parent.name} переведен в Confused.", parent.gameObject);
            SetState(ClientState.Confused);
            return;
        }
        // --- ИСПРАВЛЕНИЕ КОНЕЦ ---

        SetState(ClientState.AtRegistration);
        return;
    }
    
    // Этот блок кода остается без изменений
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
    
    Debug.LogWarning($"{name} arrived at {dest.name}, but doesn't know what to do next. Assigned provider: {(myServiceProvider != null ? (myServiceProvider as MonoBehaviour).name : "null")}");
    SetState(ClientState.Confused);
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
                if (seatTarget != null) { parent.movement.StartStuckCheck(); agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, seatTarget.position, this.gameObject)); yield return new WaitUntil(() => !agentMover.IsMoving()); parent.movement.StopStuckCheck(); } 
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
			case ClientState.WaitingForDocument:
            // В этом состоянии клиент просто терпеливо ждет.
            // Можно добавить отдельный таймер с пониженным расходом терпения.
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
    
    private IEnumerator PayBillRoutine()
    {
        agentMover.Stop();
        
        float waitTimer = 0f;
        IServiceProvider cashierProvider = null;

        // 1. Просто ждем любого доступного кассира (клерка или Директора)
		Debug.Log($"<color=yellow>КЛИЕНТ {parent.name}:</color> Внутри PayBillRoutine. Ищу доступного кассира...");
        while (cashierProvider == null || !cashierProvider.IsAvailableToServe)
        {
            cashierProvider = ClientSpawner.GetServiceProviderAtDesk(-1); // deskId для кассы = -1
            Debug.Log($"<color=yellow>КЛИЕНТ {parent.name}:</color> Жду свободного кассира... (Найден: {(cashierProvider as MonoBehaviour)?.name ?? "НЕТ"}, Доступен: {cashierProvider?.IsAvailableToServe})");
			yield return new WaitForSeconds(0.5f);
            waitTimer += 0.5f;
            if (waitTimer > clerkWaitTimeout)
            {
                Debug.LogWarning($"Клиент {parent.name} не дождался кассира и стал 'Confused'");
                yield return StartCoroutine(ExitZoneAndSetNewGoal(null, ClientState.Confused));
                yield break;
            }
        }
        
        // 2. Передаем себя на обслуживание
		Debug.Log($"<color=yellow>КЛИЕНТ {parent.name}:</color> Нашел провайдера {(cashierProvider as MonoBehaviour)?.name}. Передаю себя на обслуживание (AssignClient).");
        cashierProvider.AssignClient(parent);

        // 3. Просто ждем, пока поставщик услуг (Директор или клерк) не изменит наше состояние.
        // Вся логика оплаты теперь полностью на стороне того, кто обслуживает.
		Debug.Log($"<color=yellow>КЛИЕНТ {parent.name}:</color> Передал себя на обслуживание. Теперь жду смены состояния...");
        yield return new WaitUntil(() => currentState != ClientState.AtCashier && currentState != ClientState.InsideLimitedZone);
    }

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
                statusText += $"Being served by {(myServiceProvider as MonoBehaviour)?.name ?? "unknown"}";
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
	
	public LimitedCapacityZone GetTargetZone() { return targetZone; }
	
	private int GetDeskIdFromZone(LimitedCapacityZone zone)
{
    if (zone == null) return int.MinValue;
    if (zone == ClientSpawner.GetDesk1Zone()) return 1;
    if (zone == ClientSpawner.GetDesk2Zone()) return 2;
    if (zone == ClientSpawner.GetCashierZone()) return -1;
    if (zone == ClientSpawner.GetRegistrationZone()) return 0;
    // Можно добавить и другие зоны по аналогии
    return int.MinValue;
}
	
	 
	
}