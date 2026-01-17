// Assets/Scripts/Data/Actions/ServiceAtRegistrationExecutor.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Gameplay; // Подключаем пространство имен с OfficeObjectDurability

public class ServiceAtRegistrationExecutor : ActionExecutor
{
    // Регистрация - важное действие, которое не должно прерываться легко
    public override bool IsInterruptible => false;

    // Сюда мы сохраним эффективность для передачи в подотряды
    private float currentEfficiency = 1.0f;
    private OfficeObjectDurability currentDurabilityComponent;

    protected override IEnumerator ActionRoutine()
    {
        // 1. Получаем ссылку на регистратора и проверяем валидность
        var registrar = staff as ClerkController;
        if (registrar == null)
        {
            Debug.LogError($"[ServiceAtRegistrationExecutor] Ошибка: Staff не является ClerkController.");
            FinishAction(false); 
            yield break; 
        }
        if (registrar.assignedWorkstation == null)
        {
             Debug.LogError($"[ServiceAtRegistrationExecutor] Ошибка: Регистратору {registrar.name} не назначено рабочее место.");
             FinishAction(false);
             yield break;
        }

        // --- ИНТЕГРАЦИЯ ИЗНОСА (НАЧАЛО) ---
        currentDurabilityComponent = registrar.assignedWorkstation.GetComponent<OfficeObjectDurability>();
        if (currentDurabilityComponent != null && !currentDurabilityComponent.IsUsable())
        {
            registrar.thoughtBubble?.ShowPriorityMessage("Стойка развалилась!", 3f, Color.red);
            // Если мы не работаем, то и статус не меняем
            FinishAction(false);
            yield break;
        }
        currentEfficiency = (currentDurabilityComponent != null) ? currentDurabilityComponent.GetEfficiencyMultiplier() : 1.0f;
        // --- ИНТЕГРАЦИЯ ИЗНОСА (КОНЕЦ) ---

        // 2. Находим зону обслуживания и клиента в ней
        var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault();

        // Если зона не найдена или в ней нет клиента
        if (client == null)
        {
            FinishAction(false); 
            yield break;
        }

        Debug.Log($"[ServiceAtRegistrationExecutor] {registrar.name} начинает обслуживание клиента {client.name}");

        // 3. Устанавливаем статус "Работает"
        registrar.SetState(ClerkController.ClerkState.Working);

        // 4. Проверяем, есть ли у регистратора активное действие "Сделать запрос в архив"
        bool canMakeArchiveRequest = registrar.activeActions != null && registrar.activeActions.Any(a => a != null && a.actionType == ActionType.MakeArchiveRequest);

        // 5. Выбираем ветку логики
        if (client.mainGoal == ClientGoal.GetArchiveRecord && canMakeArchiveRequest)
        {
            // Архивный запрос
            yield return staff.StartCoroutine(HandleArchiveRequest(registrar, client));
        }
        else 
        {
            // Стандартная регистрация
            yield return staff.StartCoroutine(HandleStandardRegistration(registrar, client));

            // Проверяем, существует ли еще этот Executor
            if (this != null)
            {
                // Наносим урон стойке при успешном стандартном обслуживании
                if (currentDurabilityComponent != null) 
                {
                    currentDurabilityComponent.Degrade(Random.Range(1f, 3f));
                }

                registrar.ServiceComplete(); 
                ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
                FinishAction(true); 
            }
        }
    } // Конец ActionRoutine

    // Корутина для стандартной регистрации (направления клиента)
    private IEnumerator HandleStandardRegistration(ClerkController registrar, ClientPathfinding client)
    {
        // --- ПРИМЕНЕНИЕ ЭФФЕКТИВНОСТИ ---
        // Задержка зависит от состояния стойки
        float waitTime = Random.Range(1f, 2.5f) / currentEfficiency;
        yield return new WaitForSeconds(waitTime);

        // Проверяем состояние клиента еще раз перед действием
         if (client == null || client.stateMachine == null) {
              Debug.LogWarning("[HandleStandardRegistration] Клиент исчез во время ожидания.");
              FinishAction(false); 
              yield break;
         }

        // Проверяем цель "Архив" ДО основного определения цели
        if (client.mainGoal == ClientGoal.GetArchiveRecord)
        {
             // Если цель - архив, а мы здесь (архив отключен), отправляем домой
             registrar.thoughtBubble?.ShowPriorityMessage("Извините, архив\nсегодня не работает.", 3f, Color.red);
             yield return new WaitForSeconds(1.5f); 

             if (client == null || client.stateMachine == null) yield break; 
			 
			 client.ApplyStressJump(client.stressJump_Refusal * 1.5f);

             if (client.stateMachine.MyQueueNumber != -1)
             {
                 ClientQueueManager.Instance?.RemoveClientFromQueue(client);
             }
             client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
             client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint); 
             client.stateMachine.SetState(ClientState.LeavingUpset);
             
             // Наносим урон даже при отказе (поговорили же)
             if (currentDurabilityComponent != null) currentDurabilityComponent.Degrade(1f);

             FinishAction(true); 
             yield break; 
        }

        // Определяем правильный пункт назначения для клиента
        Waypoint correctDestination = DetermineCorrectGoalForClient(client);
        Waypoint actualDestination = correctDestination; 

        // Рассчитываем шанс ошибки при направлении
        float baseSuccessChance = 0.7f;
        float clientModifier = (client.babushkaFactor * 0.1f) - (client.suetunFactor * 0.2f);
        float registrarBonus = registrar.redirectionBonus;
        float finalChance = Mathf.Clamp(baseSuccessChance + clientModifier + registrarBonus, 0.3f, 0.95f);

        // Проверяем, произошла ли ошибка
        if (Random.value > finalChance)
        {
            Debug.LogWarning($"[Registration] ПРОВАЛ НАПРАВЛЕНИЯ! Регистратор {registrar.name} ошибся.");
            client.ApplyStressJump(0.05f); 
			registrar.thoughtBubble?.ShowPriorityMessage("Эээ... наверное туда...", 2f, Color.yellow);
			
            List<Waypoint> possibleDestinations = new List<Waypoint>();
            if (ClientSpawner.Instance != null) {
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category1DeskZones)?.waitingWaypoint);
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category2DeskZones)?.waitingWaypoint);
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.cashierZones)?.waitingWaypoint);
                if (ClientSpawner.Instance.toiletZone != null) possibleDestinations.Add(ClientSpawner.Instance.toiletZone.waitingWaypoint);
            }
             if (ClientQueueManager.Instance != null) {
                possibleDestinations.Add(ClientQueueManager.Instance.ChooseNewGoal(client)); 
             }

            possibleDestinations.RemoveAll(item => item == null || item == correctDestination);

            if (possibleDestinations.Count > 0)
            {
                actualDestination = possibleDestinations[Random.Range(0, possibleDestinations.Count)];
            }
        }

         // Проверяем клиента еще раз
         if (client == null || client.stateMachine == null || actualDestination == null) {
              FinishAction(false); 
              yield break;
         }

        string destinationName = string.IsNullOrEmpty(actualDestination.friendlyName) ?
                                actualDestination.name : actualDestination.friendlyName;
        string directionMessage = $"Пройдите, пожалуйста, к\n'{destinationName}'";
        registrar.thoughtBubble?.ShowPriorityMessage(directionMessage, 3f, Color.white);

        if (client.stateMachine.MyQueueNumber != -1)
        {
            ClientQueueManager.Instance?.RemoveClientFromQueue(client);
        }

        client.stateMachine.SetGoal(actualDestination);
        client.stateMachine.SetState(ClientState.MovingToGoal);
    } 

    // Корутина для обработки запроса в архив
    private IEnumerator HandleArchiveRequest(ClerkController registrar, ClientPathfinding client)
    {
        if (ArchiveRequestManager.Instance == null) {
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            FinishAction(true); 
            yield break;
        }
         if (client == null || client.stateMachine == null) {
             FinishAction(false);
             yield break;
         }

        ArchiveRequestManager.Instance.CreateRequest(registrar, client);
        registrar.SetState(ClerkController.ClerkState.WaitingForArchive);
        client.stateMachine.SetState(ClientState.WaitingForDocument);

        var archivistDesk = ScenePointsRegistry.Instance?.GetServicePointByID(3);
        if (archivistDesk == null)
        {
             if (client != null && client.stateMachine != null) { 
                 client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                 client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                 client.stateMachine.SetState(ClientState.LeavingUpset);
             }
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
            FinishAction(true); 
            yield break;
        }

        yield return staff.StartCoroutine(registrar.MoveToTarget(archivistDesk.clerkStandPoint.position, ClerkController.ClerkState.WaitingForArchive.ToString()));

        // Ожидаем выполнения запроса
        float waitTimer = 0f;
        float maxWaitTime = 60f;
        bool requestFulfilled = false;
        ArchiveRequest request = ArchiveRequestManager.Instance.GetOurRequest(registrar); 

        while(waitTimer < maxWaitTime)
        {
             if (client == null || client.stateMachine == null) { break; }
             ClientState clientState = client.stateMachine.GetCurrentState();
             if (clientState == ClientState.Leaving || clientState == ClientState.LeavingUpset) { break; }

            if (request != null && request.IsFulfilled)
            {
                requestFulfilled = true;
                break; 
            }
             else if (request == null && ArchiveRequestManager.Instance.HasPendingRequests())
             {
                 request = ArchiveRequestManager.Instance.GetOurRequest(registrar);
             }

            waitTimer += Time.deltaTime; 
            yield return null; 
        }

        if (requestFulfilled) 
        {
            registrar.GetComponent<StackHolder>()?.ShowSingleDocumentSprite(); 
            registrar.SetState(ClerkController.ClerkState.ReturningToWork); 
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
            registrar.GetComponent<StackHolder>()?.HideStack(); 

            if (client != null && client.stateMachine != null && client.stateMachine.GetCurrentState() == ClientState.WaitingForDocument)
            {
                client.billToPay += 150; 
                client.stateMachine.SetGoal(ClientSpawner.GetCashierZone()?.waitingWaypoint); 
                client.stateMachine.SetState(ClientState.MovingToGoal);
            }
        }
        else 
        {
             if (client != null && client.stateMachine != null && client.stateMachine.GetCurrentState() != ClientState.Leaving && client.stateMachine.GetCurrentState() != ClientState.LeavingUpset)
             {
                 registrar.thoughtBubble?.ShowPriorityMessage("Архив не отвечает...\nИзвините.", 3f, Color.red);
                 yield return new WaitForSeconds(1.0f); 
                 client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                 client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                 client.stateMachine.SetState(ClientState.LeavingUpset);
             }
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
        }

        // --- НАНЕСЕНИЕ УРОНА СТОЙКЕ (даже при архиве, он ее использует) ---
        if (currentDurabilityComponent != null) 
        {
            currentDurabilityComponent.Degrade(Random.Range(1f, 3f));
        }
        // ------------------------------------------------------------------

        registrar.ServiceComplete(); 
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType); 
        FinishAction(true); 

    } // Конец HandleArchiveRequest


    // Вспомогательный метод для определения правильной цели клиента
    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        if (client == null || ClientSpawner.Instance == null) {
            return ClientSpawner.Instance?.exitWaypoint; 
        }

        if (client.billToPay > 0)
        {
             LimitedCapacityZone cashierZone = ClientSpawner.GetCashierZone();
             if (cashierZone == null || cashierZone.waitingWaypoint == null) {
                 return ClientSpawner.Instance.exitWaypoint; 
             }
             return cashierZone.waitingWaypoint;
        }

        LimitedCapacityZone targetZone = null;
        Waypoint targetWaypoint = null;

        switch (client.mainGoal)
        {
            case ClientGoal.PayTax:
                 targetZone = ClientSpawner.GetCashierZone();
                 targetWaypoint = targetZone?.waitingWaypoint;
                break;
            case ClientGoal.GetCertificate1:
                 targetZone = ClientSpawner.GetDesk1Zone();
                 targetWaypoint = targetZone?.waitingWaypoint;
                break;
            case ClientGoal.GetCertificate2:
                 targetZone = ClientSpawner.GetDesk2Zone();
                 targetWaypoint = targetZone?.waitingWaypoint;
               break;
            case ClientGoal.AskAndLeave: 
            case ClientGoal.VisitToilet: 
            default: 
                client.isLeavingSuccessfully = true;
                client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                targetWaypoint = ClientSpawner.Instance.exitWaypoint; 
                break;
        }

        return targetWaypoint ?? ClientSpawner.Instance.exitWaypoint;
    } 
}