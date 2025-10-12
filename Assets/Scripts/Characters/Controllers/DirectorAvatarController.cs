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
        base.Awake();
        Instance = this;
        stackHolder = GetComponent<StackHolder>();
    }

    void Start()
    {
		if (skills == null)
        {
            skills = ScriptableObject.CreateInstance<CharacterSkills>();
            skills.paperworkMastery = 0.8f;
            skills.sedentaryResilience = 0.5f;
            skills.pedantry = 0.9f;
            skills.softSkills = 0.7f;
            skills.corruption = 0.1f;
        }
		
        if (visuals != null && spriteCollection != null && stateEmotionMap != null)
        {
            visuals.Setup(gender, spriteCollection, stateEmotionMap);
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
    
	public override bool IsOnBreak() => false;
	
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
        if (isManuallyWorking)
        {
            if (currentWorkstation == workstation) return;
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
    public DirectorState GetCurrentState() => currentState;
    
    public void GoAndOperateBarrier() { if (IsInUninterruptibleAction) { thoughtBubble.ShowPriorityMessage("Я занят!", 2f, Color.yellow); return; } StopAllCoroutines(); StartCoroutine(OperateBarrierRoutine()); }
    public void CollectDocuments(DocumentStack stack) { if (currentState != DirectorState.Idle && currentState != DirectorState.AtDesk || stack.IsEmpty) { return; } StopAllCoroutines(); StartCoroutine(CollectAndDeliverRoutine(stack)); }
    public void GoToDesk() { if (directorChairPoint != null) { var wp = FindNearestWaypointTo(directorChairPoint.position); if (wp != null) MoveToWaypoint(wp); } }
    public void TeleportTo(Vector3 position) => transform.position = position;
    public void ForceSetAtDeskState(bool atDesk) { IsAtDesk = atDesk; if(atDesk) SetState(DirectorState.AtDesk); }
    #endregion

    #region Private Coroutines & Methods
    private IEnumerator WorkAtStationRoutine(ServicePoint workstation)
    {
        currentWorkstation = workstation;
        ClientSpawner.AssignServiceProviderToDesk(this, workstation.deskId);
        isManuallyWorking = true;

        yield return StartCoroutine(MoveToTargetRoutine(workstation.clerkStandPoint.position, DirectorState.MovingToPoint));
        
        SetState(DirectorState.WorkingAtStation);
        
        while (isManuallyWorking)
        {
            var zone = ClientSpawner.GetZoneByDeskId(workstation.deskId);
            var clientToServe = zone?.GetOccupyingClients().FirstOrDefault();

            if (clientToServe != null && clientBeingServed == null)
            {
                yield return StartCoroutine(DirectorServiceRoutine(clientToServe));
            }
            
            yield return new WaitForSeconds(0.5f);
        }
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

    public void SetState(DirectorState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        visuals?.SetEmotionForState(newState);
    }
    
    private IEnumerator OperateBarrierRoutine()
    {
        IsInUninterruptibleAction = true;
        var barrier = SecurityBarrier.Instance;
        if (barrier == null || barrier.guardInteractionPoint == null)
        {
            IsInUninterruptibleAction = false;
            yield break;
        }
        yield return StartCoroutine(MoveToTargetRoutine(barrier.guardInteractionPoint.position, DirectorState.MovingToPoint));
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
        int docCount = stack.TakeEntireStack();
        if (docCount > 0)
        {
            stackHolder?.ShowStack(docCount, stack.maxStackSize);
            SetState(DirectorState.CarryingDocuments);
            Transform archivePoint = ArchiveManager.Instance.RequestDropOffPoint();
            if (archivePoint != null)
            {
                yield return StartCoroutine(MoveToTargetRoutine(archivePoint.position, DirectorState.CarryingDocuments));
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

    private Waypoint FindNearestWaypointTo(Vector2 position) => FindObjectsByType<Waypoint>(FindObjectsSortMode.None).OrderBy(wp => Vector2.Distance(position, wp.transform.position)).FirstOrDefault();
    #endregion
    
    #region IServiceProvider Implementation & Director Service Logic
    
    public bool IsAvailableToServe => currentState == DirectorState.WorkingAtStation && isManuallyWorking && clientBeingServed == null;
    public Transform GetClientStandPoint() => currentWorkstation != null ? currentWorkstation.clientStandPoint.transform : transform;
    public ServicePoint GetWorkstation() => currentWorkstation;

    public void AssignClient(ClientPathfinding client)
    {
        // This method is now empty, logic is handled by WorkAtStationRoutine
    }
    
    private IEnumerator DirectorServiceRoutine(ClientPathfinding client)
    {
        if (clientBeingServed != null && clientBeingServed != client)
        {
            yield break;
        }
        clientBeingServed = client;
        SetState(DirectorState.ServingClient);
        Debug.Log($"<color=#00FFFF>ДИРЕКТОР:</color> Начал процедуру обслуживания для {client.name} на столе #{currentWorkstation.deskId}.");

        thoughtBubble.ShowPriorityMessage("Разбираюсь...", 3f, Color.cyan);
        yield return new WaitForSeconds(1.5f);
        int deskId = currentWorkstation.deskId;
        bool jobDone = false; 

        if (deskId == 0)
        {
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
            
            client.stateMachine.SetGoal(destination);
            client.stateMachine.SetState(ClientState.MovingToGoal);
            jobDone = true;
        }
        else if (deskId == -1)
        {
            if (client.billToPay == 0 && client.mainGoal == ClientGoal.PayTax)
            {
                client.billToPay = Random.Range(20, 121);
            }
            
            if (PlayerWallet.Instance != null && client.billToPay > 0)
            {
                PlayerWallet.Instance.AddMoney(client.billToPay, transform.position);
                if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
                client.billToPay = 0;
                jobDone = true;
            }
            
            client.isLeavingSuccessfully = true;
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
            client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
            client.stateMachine.SetState(ClientState.Leaving);
        }
        else if (deskId == 1 || deskId == 2)
        {
            DocumentType requiredDoc = (deskId == 1) ? DocumentType.Form1 : DocumentType.Form2;
            if (client.docHolder.GetCurrentDocumentType() != requiredDoc)
            {
                client.stateMachine.GoGetFormAndReturn();
                clientBeingServed = null;
                SetState(DirectorState.WorkingAtStation);
                yield break;
            }
            else 
            {
                client.docHolder.SetDocument(DocumentType.None);
                yield return new WaitForSeconds(1.5f);
                DocumentType newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
                client.billToPay += 100;
                client.docHolder.SetDocument(newDocType);
                
                client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
                client.stateMachine.SetState(ClientState.MovingToGoal);
                jobDone = true;
            }
        }
        
        if (jobDone && currentWorkstation?.documentStack != null)
        {
            currentWorkstation.documentStack.AddDocumentToStack();
        }

        clientBeingServed = null;
        SetState(DirectorState.WorkingAtStation);
    }
    #endregion
}