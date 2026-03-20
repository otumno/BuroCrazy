using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Gameplay;

public class ServiceAtRegistrationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Цикл контролирует себя сам
    private float currentEfficiency = 1.0f;
    private OfficeObjectDurability currentDurabilityComponent;

    protected override IEnumerator ActionRoutine()
    {
        var registrar = staff as ClerkController;
        if (registrar == null || registrar.assignedWorkstation == null) { FinishAction(false); yield break; }

        currentDurabilityComponent = registrar.assignedWorkstation.GetComponent<OfficeObjectDurability>();
        if (currentDurabilityComponent != null && !currentDurabilityComponent.IsUsable())
        {
            registrar.thoughtBubble?.ShowPriorityMessage("Стойка сломана!", 3f, Color.red);
            FinishAction(false);
            yield break;
        }
        currentEfficiency = currentDurabilityComponent != null ? currentDurabilityComponent.GetEfficiencyMultiplier() : 1.0f;

        ServicePoint desk = registrar.assignedWorkstation;

        // === ПОЛНЫЙ ЦИКЛ ОБСЛУЖИВАНИЯ (PULL-МОДЕЛЬ) ===
        while (true)
        {
            registrar.SetState(ClerkController.ClerkState.Working);

            // 1. Проверяем, есть ли уже наглый клиент у стола
            ClientPathfinding client = desk.CurrentClient;

            // 2. Если у стола пусто - тянем клиента из глобальной очереди
            if (client == null && ClientQueueManager.Instance != null && ClientQueueManager.Instance.queue.Count > 0)
            {
                var nextInQueue = ClientQueueManager.Instance.queue
                    .Where(c => c.Key != null && !ClientQueueManager.Instance.currentlyCalledNumbers.Contains(c.Value))
                    .OrderBy(kvp => kvp.Value)
                    .FirstOrDefault();

                client = nextInQueue.Key;

                if (client != null)
                {
                    int ticketNum = nextInQueue.Value;
                    ClientQueueManager.Instance.currentlyCalledNumbers.Add(ticketNum);
                    desk.AssignClient(client);

                    registrar.thoughtBubble?.ShowPriorityMessage($"Талон №{ticketNum}, подойдите!", 3f, Color.green);
                    if (ClientQueueManager.Instance.nextClientSound != null)
                        AudioSource.PlayClipAtPoint(ClientQueueManager.Instance.nextClientSound, transform.position);

                    client.stateMachine.GetCalledToSpecificDesk(desk.clientStandPoint, ticketNum, registrar);
                }
            }

            // Если после всех проверок клиента нет - очередь пуста, выходим сортировать бумаги
            if (client == null) break;

            // 3. БРОНЕБОЙНАЯ ТЯГА: Если очередь пуста (или дебаг-клиенты), берем ближайшего неприкаянного клиента из сцены
            if (client == null)
            {
                var allClients = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
                float minDistance = float.MaxValue;
                Vector3 myPos = registrar.transform.position;

                foreach (var c in allClients)
                {
                    if (c != null && !c.isLeavingSuccessfully && c.stateMachine != null)
                    {
                        var state = c.stateMachine.GetCurrentState();
                        // Ищем тех, кто просто стоит, потерялся или идет куда-то без назначения
                        if (state != ClientState.Leaving && state != ClientState.LeavingUpset && state != ClientState.Enraged && state != ClientState.AtDesk1 && state != ClientState.AtDesk2 && state != ClientState.AtCashier)
                        {
                            if (c.stateMachine.MyServiceProvider == null) // Только тех, кто еще не закреплен
                            {
                                float dist = Vector2.Distance(myPos, c.transform.position);
                                if (dist < minDistance)
                                {
                                    minDistance = dist;
                                    client = c;
                                }
                            }
                        }
                    }
                }

                if (client != null)
                {
                    desk.AssignClient(client);
                    registrar.thoughtBubble?.ShowPriorityMessage("Эй, вы, подходите!", 3f, Color.green);
                    // Защита от null clientStandPoint
                    Waypoint wp = desk.clientStandPoint != null ? desk.clientStandPoint : desk.GetComponentInChildren<Waypoint>();
                    client.stateMachine.GetCalledToSpecificDesk(wp, 999, registrar);
                }
            }

            // === КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: ЗАЩИТА ОТ МИКРОЦИКЛА ===
            if (client == null)
            {
                // Если Мозг ошибся и послал работать, но рук не нашли кого обслужить - ждем 1 секунду перед выходом
                yield return new WaitForSeconds(1f);
                break;
            }

            // 3. ЖДЕМ КЛИЕНТА (Таймаут уменьшен до 15 секунд!)
            float waitTimer = 0f;
            bool clientArrived = false;
            
            // Безопасное получение позиции точки для клиента
            Vector2 wpPos = desk.clientStandPoint != null ? (Vector2)desk.clientStandPoint.transform.position : (Vector2)desk.transform.position;

            while (waitTimer < 15f)
            {
                if (client == null || client.stateMachine == null || desk.CurrentClient != client) break;

                if (desk.IsClientPhysicallyReady || Vector2.Distance(client.transform.position, wpPos) < 1.5f)
                {
                    clientArrived = true;
                    break;
                }

                var state = client.stateMachine.GetCurrentState();
                if (state == ClientState.Leaving || state == ClientState.LeavingUpset || state == ClientState.Confused || state == ClientState.Enraged)
                {
                    break; // Клиент сорвался - не ждем
                }

                // Визуальная реакция ИИ - он ждет и раздражается!
                if (waitTimer == 5f) registrar.thoughtBubble?.ShowPriorityMessage("Ну и где клиент?", 2f, Color.yellow);
                if (waitTimer == 10f) registrar.thoughtBubble?.ShowPriorityMessage("Я долго ждать буду?!", 2f, new Color(1f, 0.5f, 0f));

                waitTimer += 0.5f;
                staff.CurrentSubStatus = $"[Ожидание] Идет к стойке ({waitTimer:F1}s)";
                yield return new WaitForSeconds(0.5f);
            }

            // 4. ТАЙМАУТ - СБРАСЫВАЕМ И БЕРЕМ СЛЕДУЮЩЕГО
            if (!clientArrived)
            {
                registrar.thoughtBubble?.ShowPriorityMessage("Не пришел. Следующий!", 2f, Color.red);
                desk.ClearClient();
                if (client != null && ClientQueueManager.Instance != null)
                {
                    ClientQueueManager.Instance.RemoveClientFromQueue(client);
                    client.stateMachine?.SetState(ClientState.Confused);
                }
                yield return new WaitForSeconds(1f);
                continue; // Крутим цикл заново
            }

            // 5. КЛИЕНТ ПОДОШЕЛ - НАЧИНАЕМ ОБСЛУЖИВАНИЕ
            staff.CurrentSubStatus = $"[Обслуживание] {client.name}";
            
            if (client.mainGoal == ClientGoal.GetArchiveRecord)
                yield return staff.StartCoroutine(HandleArchiveRequest(registrar, client));
            else
                yield return staff.StartCoroutine(HandleStandardRegistration(registrar, client));

            // 6. ОТЧЕТ НА СТОЛ (Обязательная генерация макулатуры)
            if (desk.documentStack != null) desk.documentStack.AddDocumentToStack();

            // 7. ОЧИСТКА И ПЕРЕХОД К СЛЕДУЮЩЕМУ
            desk.ClearClient();
            if (currentDurabilityComponent != null) currentDurabilityComponent.Degrade(2f);

            yield return new WaitForSeconds(1f);
        }

        FinishAction(true);
    }

    private IEnumerator HandleStandardRegistration(ClerkController registrar, ClientPathfinding client)
    {
        client.ShowThoughtBubble("Здравствуйте.", 1.5f);
        yield return new WaitForSeconds(1.5f);
        if (client == null || client.stateMachine == null) yield break;

        string[] greetings = { "Слушаю вас.", "Какая у вас цель?", "Что вам нужно?", "Документы давайте." };
        registrar.thoughtBubble?.ShowPriorityMessage(greetings[Random.Range(0, greetings.Length)], 2f, Color.white);
        yield return new WaitForSeconds(2f);

        client.ShowThoughtBubble(GetClientGoalText(client.mainGoal), 2.5f);
        yield return new WaitForSeconds(2.5f);

        float waitTime = Random.Range(1f, 2.5f) / currentEfficiency;
        registrar.thoughtBubble?.ShowPriorityMessage("Минутку, смотрю...", waitTime, Color.cyan);
        yield return new WaitForSeconds(waitTime);
        if (client == null || client.stateMachine == null) yield break;

        if (client.mainGoal == ClientGoal.GetArchiveRecord)
        {
            registrar.thoughtBubble?.ShowPriorityMessage("Извините, архив\nсегодня не работает.", 3f, Color.red);
            yield return new WaitForSeconds(2f);
            client.ApplyStressJump(client.stressJump_Refusal * 1.5f);
            if (client.stateMachine.MyQueueNumber != -1) ClientQueueManager.Instance?.RemoveClientFromQueue(client);
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            yield break;
        }

        Waypoint correctDestination = DetermineCorrectGoalForClient(client);
        Waypoint actualDestination = correctDestination;

        float baseSuccessChance = 0.7f;
        float clientModifier = (client.babushkaFactor * 0.1f) - (client.suetunFactor * 0.2f);
        float finalChance = Mathf.Clamp(baseSuccessChance + clientModifier + registrar.redirectionBonus, 0.3f, 0.95f);

        if (Random.value > finalChance)
        {
            client.ApplyStressJump(0.05f);
            registrar.thoughtBubble?.ShowPriorityMessage("Эээ... наверное туда...", 2f, Color.yellow);
            List<Waypoint> possibleDestinations = new List<Waypoint>();
            if (ClientSpawner.Instance != null) {
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category1DeskZones)?.waitingWaypoint);
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category2DeskZones)?.waitingWaypoint);
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.cashierZones)?.waitingWaypoint);
            }
            possibleDestinations.RemoveAll(item => item == null || item == correctDestination);
            if (possibleDestinations.Count > 0) actualDestination = possibleDestinations[Random.Range(0, possibleDestinations.Count)];
        }

        if (actualDestination == null) yield break;

        string destinationName = string.IsNullOrEmpty(actualDestination.friendlyName) ? actualDestination.name : actualDestination.friendlyName;
        registrar.thoughtBubble?.ShowPriorityMessage($"Вам нужно в:\n'{destinationName}'", 3f, Color.green);
        yield return new WaitForSeconds(1.5f);
        client.ShowThoughtBubble("Понял, спасибо.", 2f);

        if (client.stateMachine.MyQueueNumber != -1) ClientQueueManager.Instance?.RemoveClientFromQueue(client);
        
        client.stateMachine.SetGoal(actualDestination);
        client.stateMachine.SetState(ClientState.MovingToGoal);
    }

    private IEnumerator HandleArchiveRequest(ClerkController registrar, ClientPathfinding client)
    {
        client.ShowThoughtBubble("Мне нужна выписка из архива.", 2.5f);
        yield return new WaitForSeconds(2.5f);

        registrar.thoughtBubble?.ShowPriorityMessage("Делаю запрос в архив.\nОжидайте.", 3f, Color.yellow);
        yield return new WaitForSeconds(1.5f);

        if (ArchiveRequestManager.Instance == null) {
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            yield break;
        }

        ArchiveRequestManager.Instance.CreateRequest(registrar, client);
        registrar.SetState(ClerkController.ClerkState.WaitingForArchive);
        client.stateMachine.SetState(ClientState.WaitingForDocument);

        var archivistDesk = ScenePointsRegistry.Instance?.GetServicePointByID(3);
        if (archivistDesk == null)
        {
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
            yield break;
        }

        yield return staff.StartCoroutine(registrar.MoveToTarget(archivistDesk.clerkStandPoint.position, ClerkController.ClerkState.WaitingForArchive.ToString()));

        float waitTimer = 0f;
        bool requestFulfilled = false;
        ArchiveRequest request = ArchiveRequestManager.Instance.GetOurRequest(registrar); 

        while(waitTimer < 60f)
        {
            if (client == null || client.stateMachine == null) break;
            var clientState = client.stateMachine.GetCurrentState();
            if (clientState == ClientState.Leaving || clientState == ClientState.LeavingUpset) break;

            if (request != null && request.IsFulfilled) { requestFulfilled = true; break; }
            else if (request == null && ArchiveRequestManager.Instance.HasPendingRequests()) {
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
    }

    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        if (client == null || ClientSpawner.Instance == null) return ClientSpawner.Instance?.exitWaypoint; 
        if (client.billToPay > 0) return ClientSpawner.GetCashierZone()?.waitingWaypoint ?? ClientSpawner.Instance?.exitWaypoint;

        switch (client.mainGoal)
        {
            case ClientGoal.PayTax: return ClientSpawner.GetCashierZone()?.waitingWaypoint;
            case ClientGoal.GetCertificate1: return ClientSpawner.GetDesk1Zone()?.waitingWaypoint;
            case ClientGoal.GetCertificate2: return ClientSpawner.GetDesk2Zone()?.waitingWaypoint;
            default: return ClientSpawner.Instance?.exitWaypoint;
        }
    }

    private string GetClientGoalText(ClientGoal goal)
    {
        switch(goal)
        {
            case ClientGoal.GetCertificate1: return "Мне нужна справка.";
            case ClientGoal.GetCertificate2: return "Оформляю форму 2.";
            case ClientGoal.PayTax: return "Я налоги заплатить.";
            case ClientGoal.GetArchiveRecord: return "Мне в архив нужно.";
            case ClientGoal.VisitToilet: return "Где тут туалет?";
            case ClientGoal.DirectorApproval: return "Мне к директору!";
            default: return "Я просто спросить.";
        }
    }
}
