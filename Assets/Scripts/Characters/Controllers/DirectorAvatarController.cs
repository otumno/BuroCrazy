using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AgentMover), typeof(CharacterVisuals), typeof(ThoughtBubbleController))]
public class DirectorAvatarController : StaffController, IServiceProvider
{
    #region Fields and Properties
    public static DirectorAvatarController Instance { get; private set; }

    public enum DirectorState { Idle, MovingToPoint, AtDesk, CarryingDocuments, GoingForDocuments, WorkingAtStation, ServingClient }
    
    [Header("Ссылки")]
    private StackHolder stackHolder;

    [Header("Настройки Кабинета")]
    public Transform directorChairPoint;
    
    [Header("Визуальные настройки Директора")]

    private DirectorState currentState = DirectorState.Idle;
    private ServicePoint currentWorkstation;
    private Coroutine workCoroutine;
    private bool isManuallyWorking = false;
    
    private ClientPathfinding clientBeingServed = null;

    public bool IsInUninterruptibleAction { get; private set; } = false;
    public bool IsAtDesk { get; private set; } = false;
    #endregion

    #region Unity Methods
    protected override void Awake()
{
    base.Awake(); // ВАЖНО: Вызываем Awake() родительского класса StaffController
    Instance = this;
    stackHolder = GetComponent<StackHolder>();
}

    void Start()
    {
		
		if (skills == null)
    {
        skills = ScriptableObject.CreateInstance<CharacterSkills>();
        // Можно задать Директору максимальные или средние навыки
        skills.paperworkMastery = 0.8f;
        skills.sedentaryResilience = 0.5f;
        skills.pedantry = 0.9f;
        skills.softSkills = 0.7f;
        skills.corruption = 0.1f; // Директор у нас почти не коррумпирован :)
        Debug.Log("Для Директора были созданы базовые навыки (skills).");
    }
		
        if (visuals != null && spriteCollection != null && stateEmotionMap != null)
        {
            visuals.Setup(gender, spriteCollection, stateEmotionMap);
        }
        else
        {
            Debug.LogError("Визуальные настройки для Директора не заданы в инспекторе!", this);
        }
    }

    void Update()
    {
        if (directorChairPoint != null && Vector2.Distance(transform.position, directorChairPoint.position) < 0.5f)
        {
            if (!IsAtDesk) SetState(DirectorState.AtDesk);
            IsAtDesk = true;
        }
        else
        {
            if (IsAtDesk && currentState == DirectorState.AtDesk) SetState(DirectorState.Idle);
            IsAtDesk = false;
        }
    }
    #endregion
    
	public override bool IsOnBreak()
{
    // Директор находится под прямым управлением игрока и никогда не уходит на "перерыв" самостоятельно.
    // Поэтому для него это состояние всегда ложно.
    return false;
}
	
    #region Public Methods
    public void MoveToWaypoint(Waypoint targetWaypoint)
    {
        if (currentState == DirectorState.WorkingAtStation || currentState == DirectorState.ServingClient)
        {
            StopManualWork(false);
        }
        
        StopAllCoroutines();
        StartCoroutine(MoveToTargetRoutine(targetWaypoint.transform.position, DirectorState.Idle));
    }

    public void StartWorkingAt(ServicePoint workstation)
    {
        // <<< FIX 1: Исправляем смену задач >>>
        if (isManuallyWorking)
        {
            if (currentWorkstation == workstation) return; // Уже выполняем эту задачу
            
            Debug.Log($"<color=cyan>ДИРЕКТОР:</color> Получил команду сменить рабочее место. Прекращаю работу на '{currentWorkstation.name}'.");
            StopManualWork(false);
        }
        
        StopAllCoroutines();
        workCoroutine = StartCoroutine(WorkAtStationRoutine(workstation));
    }


    public void StopManualWork(bool moveAway = true)
    {
        if (!isManuallyWorking) return;
        
        if(workCoroutine != null) StopCoroutine(workCoroutine);
        workCoroutine = null;
        isManuallyWorking = false;
        clientBeingServed = null;

        if(currentWorkstation != null)
        {
            ClientSpawner.UnassignServiceProviderFromDesk(currentWorkstation.deskId);
            if (moveAway)
            {
                StartCoroutine(MoveToTargetRoutine((Vector2)currentWorkstation.clerkStandPoint.position + Vector2.down, DirectorState.Idle));
            }
            currentWorkstation = null;
        }
        SetState(DirectorState.Idle);
    }
    
    public DirectorState GetCurrentState() { return currentState; }
    
    public void GoAndOperateBarrier()
    {
        if (IsInUninterruptibleAction)
        {
            thoughtBubble.ShowPriorityMessage("Я занят!", 2f, Color.yellow);
            return;
        }
        StopAllCoroutines();
        StartCoroutine(OperateBarrierRoutine());
    }

    public void CollectDocuments(DocumentStack stack)
    {
        if (currentState != DirectorState.Idle && currentState != DirectorState.AtDesk)
        {
            thoughtBubble.ShowPriorityMessage("Я занят!", 2f, Color.yellow);
            return;
        }
        if (stack.IsEmpty)
        {
            thoughtBubble.ShowPriorityMessage("Стопка пуста.", 2f, Color.gray);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(CollectAndDeliverRoutine(stack));
    }

    public void GoToDesk() 
    {
        if (directorChairPoint != null)
        {
            var targetWaypoint = FindNearestWaypointTo(directorChairPoint.position);
            if (targetWaypoint != null)
            {
                MoveToWaypoint(targetWaypoint);
            }
        }
    }
    
    public void TeleportTo(Vector3 position) 
    {
        transform.position = position;
    }

    public void ForceSetAtDeskState(bool atDesk) 
    {
        IsAtDesk = atDesk;
        if(atDesk) SetState(DirectorState.AtDesk);
    }
    
    #endregion

    #region Private Coroutines & Methods
    private IEnumerator WorkAtStationRoutine(ServicePoint workstation)
    {
        // --- ЛОГ ---
        Debug.Log($"[Director AI] 1. Запущена корутина WorkAtStationRoutine для '{workstation.name}' (deskId: {workstation.deskId})");

        currentWorkstation = workstation;
        
        // --- ЛОГ ---
        Debug.Log($"[Director AI] 2. Вызываю ClientSpawner.AssignServiceProviderToDesk для deskId: {workstation.deskId}");
        ClientSpawner.AssignServiceProviderToDesk(this, workstation.deskId);
        isManuallyWorking = true;

        yield return StartCoroutine(MoveToTargetRoutine(workstation.clerkStandPoint.position, DirectorState.MovingToPoint));
        
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble.ShowPriorityMessage("Приступаю к работе.", 3f, Color.green);
        
        // --- ЛОГ ---
        Debug.Log($"[Director AI] 3. Прибыл на место и нахожусь в состоянии WorkingAtStation. IsAvailableToServe: {IsAvailableToServe}");

        while (true)
        {
            yield return null;
        }
    }

public IEnumerator MoveToTarget(Vector2 targetPosition, DirectorState stateOnArrival) 
{ 
    SetState(DirectorState.MovingToPoint);
    Queue<Waypoint> path = PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject); 
    if (path != null && path.Count > 0) 
    { 
        agentMover.SetPath(path);
        yield return new WaitUntil(() => !agentMover.IsMoving()); 
    } 
    SetState(stateOnArrival);
}


    public void SetState(DirectorState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        visuals?.SetEmotionForState(newState);
    }
    
    private IEnumerator MoveToTargetRoutine(Vector2 targetPosition, DirectorState stateAfterArrival) 
    { 
        SetState(DirectorState.MovingToPoint); 
        Queue<Waypoint> path = PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject); 
        if (path != null && path.Count > 0) 
        { 
            agentMover.SetPath(path); 
            yield return new WaitUntil(() => !agentMover.IsMoving()); 
        } 
        SetState(stateAfterArrival); 
    }

    private IEnumerator OperateBarrierRoutine()
    {
        IsInUninterruptibleAction = true;

        var barrier = SecurityBarrier.Instance;
        if (barrier == null || barrier.guardInteractionPoint == null)
        {
            Debug.LogError("Барьер или точка взаимодействия с ним не найдены!");
            IsInUninterruptibleAction = false;
            yield break;
        }
        
        Debug.Log($"<color=cyan>ДИРЕКТОР:</color> Иду к пульту управления барьером.");
        yield return StartCoroutine(MoveToTargetRoutine(barrier.guardInteractionPoint.position, DirectorState.MovingToPoint));

        Debug.Log($"<color=cyan>ДИРЕКТОР:</color> Переключаю барьер.");
        thoughtBubble.ShowPriorityMessage("Щёлк...", 1.5f, Color.gray);
        yield return new WaitForSeconds(1.5f);
        
        barrier.ToggleBarrier();

        SetState(DirectorState.Idle);
        IsInUninterruptibleAction = false;
    }
    
    private IEnumerator CollectAndDeliverRoutine(DocumentStack stack)
    {
        IsInUninterruptibleAction = true; 
        
        SetState(DirectorState.GoingForDocuments);
        yield return StartCoroutine(MoveToTargetRoutine(stack.transform.position, DirectorState.GoingForDocuments));

        Debug.Log("Директор: Забираю документы со стопки.");
        int docCount = stack.TakeEntireStack();
        if (docCount > 0)
        {
            stackHolder?.ShowStack(docCount, stack.maxStackSize);
            SetState(DirectorState.CarryingDocuments);
            Transform archivePoint = ArchiveManager.Instance.RequestDropOffPoint();
            if (archivePoint != null)
            {
                Debug.Log("Директор: Несу документы в архив.");
                yield return StartCoroutine(MoveToTargetRoutine(archivePoint.position, DirectorState.CarryingDocuments));

                Debug.Log("Директор: Складываю документы в архив.");
                for (int i = 0; i < docCount; i++)
                {
                    ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack();
                }
                stackHolder?.HideStack();
                ArchiveManager.Instance.FreeOverflowPoint(archivePoint);
            }
        }

        SetState(DirectorState.Idle);
        IsInUninterruptibleAction = false;
    }

    private Waypoint FindNearestWaypointTo(Vector2 position)
    {
        var allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        return allWaypoints
            .OrderBy(wp => Vector2.Distance(position, wp.transform.position))
            .FirstOrDefault();
    }
    #endregion

    #region IServiceProvider Implementation
    
    public bool IsAvailableToServe => currentState == DirectorState.WorkingAtStation && isManuallyWorking && clientBeingServed == null;
    
    public Transform GetClientStandPoint() { return currentWorkstation != null ? currentWorkstation.clientStandPoint.transform : transform; }
    public ServicePoint GetWorkstation() { return currentWorkstation; }

    public void AssignClient(ClientPathfinding client)
    {
        StartCoroutine(DirectorServiceRoutine(client));
    }
    
    private IEnumerator DirectorServiceRoutine(ClientPathfinding client)
    {
        if (clientBeingServed != null && clientBeingServed != client)
        {
            Debug.LogError($"<color=red>ОШИБКА ЛОГИКИ:</color> Директору пытаются назначить клиента {client.name}, пока он уже занят клиентом {clientBeingServed.name}!");
            yield break;
        }
        clientBeingServed = client;
        SetState(DirectorState.ServingClient);
        
        Debug.Log($"<color=#00FFFF>ДИРЕКТОР:</color> Начал процедуру обслуживания для клиента {client.name} на столе #{currentWorkstation.deskId}. Статус IsAvailableToServe: {IsAvailableToServe}");

        thoughtBubble.ShowPriorityMessage("Разбираюсь...", 3f, Color.cyan);
        yield return new WaitForSeconds(1.5f);

        int deskId = currentWorkstation.deskId;
        bool jobDone = false; 

        if (deskId == 0)
        {
            Debug.Log($"<color=#00FFFF>ДИРЕКТОР-РЕГИСТРАТОР:</color> Обслуживаю {client.name}. Его цель: {client.mainGoal}.");
            Waypoint destination;
            if (client.billToPay > 0) { destination = ClientSpawner.GetCashierZone().waitingWaypoint; }
            else
            {
                switch (client.mainGoal)
                {
                    case ClientGoal.PayTax: destination = ClientSpawner.GetCashierZone().waitingWaypoint; break;
                    case ClientGoal.GetCertificate1: destination = ClientSpawner.GetDesk1Zone().waitingWaypoint; break;
                    case ClientGoal.GetCertificate2: destination = ClientSpawner.GetDesk2Zone().waitingWaypoint; break;
                    default: destination = ClientSpawner.Instance.exitWaypoint; break;
                }
            }
            string destinationName = string.IsNullOrEmpty(destination.friendlyName) ? destination.name : destination.friendlyName;
            thoughtBubble.ShowPriorityMessage($"Пройдите к\n'{destinationName}'", 3f, Color.white);
            if (client.stateMachine.MyQueueNumber != -1) { ClientQueueManager.Instance.RemoveClientFromQueue(client); }
            
            Debug.Log($"<color=#00FFFF>ДИРЕКТОР-РЕГИСТРАТОР:</color> Направляю клиента к '{destinationName}'.");
            client.stateMachine.SetGoal(destination);
            client.stateMachine.SetState(ClientState.MovingToGoal);
            jobDone = true;
        }
        else if (deskId == -1)
        {
            Debug.Log($"<color=#00FFFF>ДИРЕКТОР-КАССИР:</color> Проверяю счет клиента {client.name}. Текущий счет: ${client.billToPay}, цель: {client.mainGoal}.");
            
            if (client.billToPay == 0 && client.mainGoal == ClientGoal.PayTax)
            {
                client.billToPay = Random.Range(20, 121);
                Debug.Log($"<color=#00FFFF>ДИРЕКТОР-КАССИР:</color> Счет был 0. Выставил новый счет на ${client.billToPay}.");
            }
            
            if (PlayerWallet.Instance != null && client.billToPay > 0)
            {
                Debug.Log($"<color=#00FFFF>ДИРЕКТОР-КАССИР:</color> Принимаю оплату ${client.billToPay}.");
                thoughtBubble.ShowPriorityMessage($"К оплате: ${client.billToPay}", 3f, new Color(0.1f, 0.4f, 0.1f));
                
                if (client.moneyPrefab != null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 spawnPosition = (Vector2)client.transform.position + (Random.insideUnitCircle * 0.2f);
                        GameObject moneyInstance = Instantiate(client.moneyPrefab, spawnPosition, Quaternion.identity);
                        MoneyMover mover = moneyInstance.GetComponent<MoneyMover>();
                        Transform moneyTarget = currentWorkstation.documentPointOnDesk;
                        if (mover != null && moneyTarget != null) { mover.StartMove(moneyTarget); }
                        yield return new WaitForSeconds(0.15f);
                    }
                }

                PlayerWallet.Instance.AddMoney(client.billToPay, transform.position);
                if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
                client.billToPay = 0;
                jobDone = true;
            }
            
            Debug.Log($"<color=#00FFFF>ДИРЕКТОР-КАССИР:</color> Обслуживание завершено. Отправляю клиента на выход.");
            client.isLeavingSuccessfully = true;
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
            client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
            client.stateMachine.SetState(ClientState.Leaving);
        }
        else if (deskId == 1 || deskId == 2)
        {
            DocumentType requiredDoc = (deskId == 1) ? DocumentType.Form1 : DocumentType.Form2;
            DocumentType clientDoc = client.docHolder.GetCurrentDocumentType();
            
            Debug.Log($"<color=#00FFFF>ДИРЕКТОР-КЛЕРК:</color> Проверяю документы. Требуется: {requiredDoc}, у клиента: {clientDoc}.");

            if (clientDoc != requiredDoc)
            {
                string requiredDocName = (requiredDoc == DocumentType.Form1) ? "Бланк-1" : "Бланк-2";
                thoughtBubble.ShowPriorityMessage($"Неверный бланк!\nНужен '{requiredDocName}'.", 3f, Color.yellow);
                
                // --- ИЗМЕНЕНИЕ НАЧАЛО: Заменяем несколько строк одной ---
                client.stateMachine.GoGetFormAndReturn();
                // --- ИЗМЕНЕНИЕ КОНЕЦ ---

                Debug.Log($"<color=#00FFFF>ДИРЕКТОР-КЛЕРК:</color> Отправил {client.name} за бланком. Завершаю текущий сеанс.");
                
                clientBeingServed = null;
                SetState(DirectorState.WorkingAtStation);
                yield break;
            }
            else 
            {
                Debug.Log($"<color=#00FFFF>ДИРЕКТОР-КЛЕРК:</color> Документ верный. Начинаю обработку.");
                GameObject prefabToFly = client.docHolder.GetPrefabForType(clientDoc);
                client.docHolder.SetDocument(DocumentType.None);
                bool transferToDirectorComplete = false;
                if (prefabToFly != null && currentWorkstation != null)
                {
                    GameObject flyingDoc = Instantiate(prefabToFly, client.docHolder.handPoint.position, Quaternion.identity);
                    DocumentMover mover = flyingDoc.GetComponent<DocumentMover>();
                    if (mover != null)
                    {
                        mover.StartMove(currentWorkstation.documentPointOnDesk, () => { if(flyingDoc != null) Destroy(flyingDoc); transferToDirectorComplete = true; });
                        yield return new WaitUntil(() => transferToDirectorComplete);
                    }
                }
                
                thoughtBubble.ShowPriorityMessage("Принято.", 2f, Color.white);
                yield return new WaitForSeconds(1.5f);

                DocumentType newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
                client.billToPay += 100;

                GameObject newDocPrefab = client.docHolder.GetPrefabForType(newDocType);
                bool transferToClientComplete = false;
                if (newDocPrefab != null && currentWorkstation != null)
                {
                    GameObject newDocOnDesk = Instantiate(newDocPrefab, currentWorkstation.documentPointOnDesk.position, Quaternion.identity);
                    if (client.stampSound != null) { AudioSource.PlayClipAtPoint(client.stampSound, currentWorkstation.documentPointOnDesk.position); }
                    yield return new WaitForSeconds(1.0f);
                    
                    DocumentMover mover = newDocOnDesk.GetComponent<DocumentMover>();
                    if (mover != null)
                    {
                        mover.StartMove(client.docHolder.handPoint, () => { client.docHolder.ReceiveTransferredDocument(newDocType, newDocOnDesk); transferToClientComplete = true; });
                        yield return new WaitUntil(() => transferToClientComplete);
                    }
                }
                
                Debug.Log($"<color=#00FFFF>ДИРЕКТОР-КЛЕРК:</color> Выдал {newDocType}, выставил счет ${client.billToPay}. Отправляю в кассу.");
                client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
                client.stateMachine.SetState(ClientState.MovingToGoal);
                jobDone = true;
            }
        }
        
        if (jobDone && currentWorkstation != null && currentWorkstation.documentStack != null)
        {
            currentWorkstation.documentStack.AddDocumentToStack();
            Debug.Log($"<color=#00FFFF>ДИРЕКТОР:</color> Создал копию-отчет на столе '{currentWorkstation.name}'.");
        }

        clientBeingServed = null;
        SetState(DirectorState.WorkingAtStation);
        Debug.Log($"<color=#00FFFF>ДИРЕКТОР:</color> Полностью завершил обслуживание {client.name}. Статус IsAvailableToServe: {IsAvailableToServe}");
    }
    #endregion
}