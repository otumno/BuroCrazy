// Файл: Assets/Scripts/Characters/Controllers/InternController.cs - ПОЛНАЯ ИСПРАВЛЕННАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class InternController : StaffController, IServiceProvider
{
    public enum InternState { Patrolling, HelpingConfused, ServingFromQueue, CoveringDesk, GoingToBreak, OnBreak, GoingToToilet, AtToilet, ReturningToPatrol, Inactive, Working, TalkingToConfused, TakingStackToArchive }
    
    [Header("Настройки стажера")]
    private InternState currentState = InternState.Inactive;
    private ServicePoint coveredServicePoint; // Стол, который стажер сейчас подменяет

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ ИСПОЛНИТЕЛЕЙ ---
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
        // Преобразуем строку обратно в enum InternState
        if (System.Enum.TryParse<InternState>(stateOnArrival, out InternState newState))
        {
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
            yield return new WaitUntil(() => !agentMover.IsMoving());
            SetState(newState);
        }
        else // Если состояние не распознано, просто двигаемся
        {
             agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
             yield return new WaitUntil(() => !agentMover.IsMoving());
        }
    }

    // Добавляем перегрузку метода для работы с enum напрямую внутри этого класса
    public IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival)
    {
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    public override string GetCurrentStateName()
    {
        return currentState.ToString();
    }

    // --- РЕАЛИЗАЦИЯ БАЗОВЫХ МЕТОДОВ ---
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
    if (coveredServicePoint == null) yield break;
    int deskId = coveredServicePoint.deskId;

    if (deskId == 0) // Если подменяем регистратора
    {
        thoughtBubble?.ShowPriorityMessage("Попробую помочь...", 2f, Color.yellow);
        yield return new WaitForSeconds(3f); // Стажер работает медленнее

        Waypoint destination = DetermineCorrectGoalForClient(client);
        string destName = string.IsNullOrEmpty(destination.friendlyName) ? destination.name : destination.friendlyName;

        float errorChance = 0.4f * (1f - skills.pedantry);
        if(Random.value < errorChance)
        {
             thoughtBubble?.ShowPriorityMessage("Ой, кажется, вам\nтуда...", 3f, Color.red);
        }
        else
        {
             thoughtBubble?.ShowPriorityMessage($"Вам к '{destName}'", 3f, Color.white);
        }

        if (client.stateMachine.MyQueueNumber != -1) ClientQueueManager.Instance.RemoveClientFromQueue(client);
        client.stateMachine.SetGoal(destination);
        client.stateMachine.SetState(ClientState.MovingToGoal);
    }
    else if (deskId == -1) // Если подменяем кассира
    {
        thoughtBubble?.ShowPriorityMessage("Принимаю оплату...", 2f, Color.yellow);
        yield return new WaitForSeconds(3f);

        if (client.billToPay > 0)
        {
            PlayerWallet.Instance?.AddMoney(client.billToPay, transform.position);
            if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
            client.billToPay = 0;
            coveredServicePoint.documentStack?.AddDocumentToStack();
            thoughtBubble?.ShowPriorityMessage("Оплачено!", 2f, Color.green);
        }
        client.isLeavingSuccessfully = true;
        client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
        client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
        client.stateMachine.SetState(ClientState.Leaving);
    }
    // --- НАЧАЛО НОВОЙ ЛОГИКИ ДЛЯ КЛЕРКА ---
    else if (deskId == 1 || deskId == 2) // Если подменяем клерка
    {
        thoughtBubble?.ShowPriorityMessage("Так... посмотрим...", 2f, Color.yellow);
        yield return new WaitForSeconds(1.5f); // Задержка на "оценку ситуации"

        DocumentType requiredDoc = (deskId == 1) ? DocumentType.Form1 : DocumentType.Form2;
        
        // Проверяем, есть ли у клиента нужный документ
        if (client.docHolder.GetCurrentDocumentType() != requiredDoc)
        {
            thoughtBubble?.ShowPriorityMessage("У вас бланк не тот!\nВозьмите другой.", 3f, Color.red);
            yield return new WaitForSeconds(2f);
            client.stateMachine.GoGetFormAndReturn();
        }
        else
        {
            // Документ правильный, начинаем "обработку"
            thoughtBubble?.ShowPriorityMessage("Это займет чуть\nбольше времени...", 4f, Color.gray);
            
            // Стажер работает дольше клерка. Время также зависит от навыка "Бюрократия".
            float processingTime = Random.Range(5f, 8f) * (1f + (1f - skills.paperworkMastery)); 
            yield return new WaitForSeconds(processingTime);

            // Забираем старый документ, выдаем новый
            client.docHolder.SetDocument(DocumentType.None);
            if (client.stampSound != null) AudioSource.PlayClipAtPoint(client.stampSound, transform.position);
            yield return new WaitForSeconds(1f);
            
            DocumentType newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
            client.docHolder.SetDocument(newDocType);
            
            // Выставляем счет
            client.billToPay += (deskId == 1) ? 100 : 250;
            
            // Отправляем в кассу
            thoughtBubble?.ShowPriorityMessage("Готово! Теперь в кассу.", 3f, Color.green);
            client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
            client.stateMachine.SetState(ClientState.MovingToGoal);
            
            // Засчитываем выполненную работу (добавляем документ в стопку на столе)
            coveredServicePoint.documentStack?.AddDocumentToStack();
        }
    }
    // --- КОНЕЦ НОВОЙ ЛОГИКИ ---
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