// Assets/Scripts/Characters/Controllers/InternController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Managers.Teletype; // <--- ДОБАВЛЕНО
using Utilities;
using Characters;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class InternController : StaffController, IServiceProvider
{
    public enum InternState
    {
        Patrolling,
        HelpingConfused,
        ServingFromQueue,
        CoveringDesk,
        GoingToBreak,
        OnBreak,
        GoingToToilet,
        AtToilet,
        ReturningToPatrol,
        Inactive,
        Working,
        TalkingToConfused,
        TakingStackToArchive
    }
    
    [Header("Настройки стажера")]
    private InternState currentState = InternState.Inactive;
    private ServicePoint coveredServicePoint; 

    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;
    
    // --- ПЕРЕОПРЕДЕЛЕНИЕ СТАРТА СМЕНЫ ---
    public override void StartShift()
    {
        // Не вызываем base.StartShift(), так как стажеру не нужно искать стол
        
        if (thoughtBubble) thoughtBubble.ShowPriorityMessage("На стажировку!", 2f, Color.white);
        gameObject.SetActive(true);
        hasArrivedToday = false;
        
        // Теперь TeletypeManager будет найден благодаря добавленному using
        TeletypeManager.Instance?.LogStaffWork(characterName, role.ToString(), isStartShift: true);
        
        // Стажер всегда "прибыл" сразу, так как спавнится в зоне стажеров/холле
        hasArrivedToday = true;

        // Запускаем уникальную логику стажера
        StartCoroutine(InternLogicLoop());
    }

    private IEnumerator InternLogicLoop()
    {
        yield return new WaitForSeconds(1f);
        
        while (IsOnDuty())
        {
            // Если занят (выполняет Executor), ждем
            if (currentExecutor != null) 
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            // Если не патрулирует и не выполняет действие -> Ищем, что делать
            if (currentState == InternState.Inactive || currentState == InternState.Patrolling)
            {
                // Приоритет 1: Подмена (Cover Desk)
                var coverAction = activeActions.FirstOrDefault(a => a.actionType == ActionType.CoverClerk || a.actionType == ActionType.CoverRegistrar);
                if (coverAction != null && coverAction.AreConditionsMet(this))
                {
                    ExecuteAction(coverAction);
                    continue;
                }

                // Приоритет 2: Помощь (Help Confused)
                var helpAction = activeActions.FirstOrDefault(a => a.actionType == ActionType.HelpConfusedClient);
                if (helpAction != null && helpAction.AreConditionsMet(this))
                {
                    ExecuteAction(helpAction);
                    continue;
                }

                // Приоритет 3: Патруль (если ничего другого нет и мы не патрулируем)
                if (currentExecutor == null)
                {
                    var patrolAction = activeActions.FirstOrDefault(a => a.actionType == ActionType.InternPatrol) 
                                       ?? systemActionDatabase?.allActions.FirstOrDefault(a => a.actionType == ActionType.InternPatrol);
                    
                    if (patrolAction != null)
                    {
                        ExecuteAction(patrolAction);
                    }
                    else
                    {
                        // Фолбэк, если экшена нет - просто гуляем
                        SetState(InternState.Patrolling);
                        var points = ScenePointsRegistry.Instance?.internPatrolPoints;
                        if (points != null && points.Count > 0)
                        {
                             var p = points[Random.Range(0, points.Count)];
                             yield return StartCoroutine(MoveToTarget(p.transform.position, InternState.Patrolling));
                        }
                    }
                }
            }
            
            yield return new WaitForSeconds(2f);
        }
    }
    
    public void SetState(InternState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        if(visuals != null)
        {
            visuals.SetEmotionForState(newState);
        }
    }
    
    public void AssignCoveredWorkstation(ServicePoint point)
    {
        coveredServicePoint = point;
    }
    
    public InternState GetCurrentState()
    {
        return currentState;
    }
    
    // Переопределяем метод из базового класса
    public override IEnumerator MoveToTarget(Vector2 targetPosition, string stateOnArrival)
    {
        if (System.Enum.TryParse<InternState>(stateOnArrival, out InternState newState))
        {
            if(agentMover != null) 
                agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject));
            
            yield return new WaitUntil(() => agentMover == null || !agentMover.IsMoving());
            SetState(newState);
        }
        else
        {
             yield return base.MoveToTarget(targetPosition, stateOnArrival);
        }
    }

    public IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival)
    {
        if(agentMover != null)
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject));
        
        yield return new WaitUntil(() => agentMover == null || !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    public override string GetCurrentStateName()
    {
        return currentState.ToString();
    }

    public override bool IsOnBreak()
    {
        return currentState == InternState.OnBreak ||
               currentState == InternState.GoingToBreak ||
               currentState == InternState.AtToilet ||
               currentState == InternState.GoingToToilet;
    }

    public override string GetStatusInfo()
    {
        return currentState.ToString();
    }
    
    public void InitializeFromData(RoleData data)
    {
        if (agentMover != null)
        {
            agentMover.moveSpeed = data.moveSpeed;
            agentMover.priority = data.priority;
        }
        
        this.spriteCollection = data.spriteCollection;
        this.stateEmotionMap = data.stateEmotionMap;
        
        if(visuals != null)
        {
            visuals.EquipAccessory(data.accessoryPrefab);
        }
    }
    
    #region IServiceProvider Implementation

    public bool IsAvailableToServe => GetCurrentState() == InternState.CoveringDesk;

    public Transform GetClientStandPoint()
    {
        return coveredServicePoint != null ? coveredServicePoint.clientStandPoint.transform : transform;
    }

    public ServicePoint GetWorkstation()
    {
        return coveredServicePoint;
    }

    public void AssignClient(ClientPathfinding client)
    {
        StartCoroutine(InternServiceRoutine(client));
    }

    private IEnumerator InternServiceRoutine(ClientPathfinding client)
    {
        if (client == null || coveredServicePoint == null) yield break;

        int deskId = coveredServicePoint.deskId;

        // ИЗНОС
        var durability = coveredServicePoint.GetComponent<Gameplay.OfficeObjectDurability>();
        if (durability != null && !durability.IsUsable())
        {
            thoughtBubble?.ShowPriorityMessage("Стол сломан!\nЯ не могу...", 3f, Color.red);
            yield break;
        }
        float efficiency = (durability != null) ? durability.GetEfficiencyMultiplier() : 1.0f;

        if (deskId == 0) // Регистратура
        {
            thoughtBubble?.ShowPriorityMessage("Попробую помочь...", 2f, Color.yellow);
            yield return new WaitForSeconds(3f / efficiency); 

            Waypoint destination = DetermineCorrectGoalForClient(client);
            string destName = (destination != null) ? destination.name : "Выход";

            float errorChance = 0.4f * (1f - skills.pedantry);
            if(Random.value < errorChance)
                thoughtBubble?.ShowPriorityMessage("Ой, кажется, вам\nтуда...", 3f, Color.red);
            else
                thoughtBubble?.ShowPriorityMessage($"Вам к '{destName}'", 3f, Color.white);

            if (client.stateMachine != null)
            {
                if (client.stateMachine.MyQueueNumber != -1) 
                    ClientQueueManager.Instance?.RemoveClientFromQueue(client);
                if (destination != null)
                {
                    client.stateMachine.SetGoal(destination);
                    client.stateMachine.SetState(ClientState.MovingToGoal);
                }
            }
        }
        else if (deskId == -1) // Касса
        {
            thoughtBubble?.ShowPriorityMessage("Принимаю оплату...", 2f, Color.yellow);
            yield return new WaitForSeconds(3f / efficiency);

            if (client.billToPay > 0)
            {
                PlayerWallet.Instance?.AddMoney(client.billToPay, "Оплата (Стажер)");
                if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
                client.billToPay = 0;
                coveredServicePoint.documentStack?.AddDocumentToStack();
                thoughtBubble?.ShowPriorityMessage("Оплачено!", 2f, Color.green);
            }
            client.isLeavingSuccessfully = true;
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
            client.stateMachine?.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine?.SetState(ClientState.Leaving);
        }
        else if (deskId == 1 || deskId == 2) // Клерк
        {
            thoughtBubble?.ShowPriorityMessage("Так... посмотрим...", 2f, Color.yellow);
            yield return new WaitForSeconds(1.5f / efficiency); 

            if (client.docHolder == null)
            {
                thoughtBubble?.ShowPriorityMessage("Ошибка: нет документа!", 3f, Color.red);
                yield break;
            }

            DocumentType requiredDoc = (deskId == 1) ? DocumentType.Form1 : DocumentType.Form2;
            
            if (client.docHolder.GetCurrentDocumentType() != requiredDoc)
            {
                thoughtBubble?.ShowPriorityMessage("У вас бланк не тот!", 3f, Color.red);
                yield return new WaitForSeconds(2f);
                client.stateMachine?.GoGetFormAndReturn();
            }
            else
            {
                float processingTime = Random.Range(5f, 8f) / efficiency; 
                yield return new WaitForSeconds(processingTime);

                client.docHolder.SetDocument(DocumentType.None);
                if (client.stampSound != null) AudioSource.PlayClipAtPoint(client.stampSound, transform.position);
                yield return new WaitForSeconds(1f);
                
                DocumentType newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
                client.docHolder.SetDocument(newDocType);
                client.billToPay += (deskId == 1) ? 100 : 250;
                
                thoughtBubble?.ShowPriorityMessage("Готово!", 3f, Color.green);
                client.stateMachine?.SetGoal(ClientSpawner.GetCashierZone()?.waitingWaypoint);
                client.stateMachine?.SetState(ClientState.MovingToGoal);
                
                coveredServicePoint.documentStack?.AddDocumentToStack();
            }
        }

        if (durability != null) durability.Degrade(Random.Range(3f, 6f));
    }

    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        if (client.billToPay > 0) return ClientSpawner.GetCashierZone()?.waitingWaypoint;
        switch (client.mainGoal)
        {
            case ClientGoal.PayTax: return ClientSpawner.GetCashierZone()?.waitingWaypoint;
            case ClientGoal.GetCertificate1: return ClientSpawner.GetDesk1Zone()?.waitingWaypoint;
            case ClientGoal.GetCertificate2: return ClientSpawner.GetDesk2Zone()?.waitingWaypoint;
            case ClientGoal.VisitToilet: return ClientSpawner.GetToiletZone()?.waitingWaypoint;
            default: return ClientQueueManager.Instance.ChooseNewGoal(client);
        }
    }
    #endregion
}