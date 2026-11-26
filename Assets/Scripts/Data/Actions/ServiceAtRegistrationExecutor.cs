using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Gameplay;
using Clinch;
using Enums;

public class ServiceAtRegistrationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Цикл контролирует себя сам
    private ClientPathfinding servingClient;
    private ServicePoint servingDesk;
    private ArchiveRequest activeArchiveRequest;
    private float currentEfficiency = 1.0f;
    private OfficeObjectDurability currentDurabilityComponent;
    private bool _lastServiceSuccess;

    protected override IEnumerator ActionRoutine()
    {
        yield return ReturnToWorkstation();
        if (!IsAtWorkstation) { FinishAction(false); yield break; }
        var registrar = staff as ClerkController;
        if (registrar == null || registrar.assignedWorkstation == null) { FinishAction(false); yield break; }

        currentDurabilityComponent = registrar.assignedWorkstation.GetComponent<OfficeObjectDurability>();
        if (currentDurabilityComponent != null && !currentDurabilityComponent.IsUsable())
        {
            registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.broken"), 3f, Color.red);
            FinishAction(false);
            yield break;
        }
        currentEfficiency = Mathf.Max(.05f, currentDurabilityComponent != null ? currentDurabilityComponent.GetEfficiencyMultiplier() : 1.0f);

        ServicePoint desk = registrar.assignedWorkstation;
        servingDesk = desk;

        // === ПОЛНЫЙ ЦИКЛ ОБСЛУЖИВАНИЯ (PULL-МОДЕЛЬ) ===
        while (desk != null && registrar.assignedWorkstation == desk)
        {
            _lastServiceSuccess = false;
            registrar.SetState(ClerkController.ClerkState.Working);

            // 1. Проверяем, есть ли уже наглый клиент у стола
            ClientPathfinding client = desk.CurrentClient;

            // 2. Если у стола пусто - тянем клиента из глобальной очереди
            if (client == null && ClientQueueManager.Instance != null && ClientQueueManager.Instance.queue.Count > 0)
            {
                var nextInQueue = ClientQueueManager.Instance.queue
                    .Where(c => ClientQueueManager.Instance.CanCallForRegistration(c.Key, c.Value))
                    .OrderBy(kvp => kvp.Value)
                    .FirstOrDefault();

                client = nextInQueue.Key;

                if (client != null)
                {
                    int ticketNum = nextInQueue.Value;
                    if (!client.stateMachine.TryGetCalledToSpecificDesk(desk.clientStandPoint, ticketNum, registrar)) break;
                    ClientQueueManager.Instance.currentlyCalledNumbers.Add(ticketNum);
                    ClientQueueManager.Instance.clientsAwaitingResponse[ticketNum] = Time.time;

                    registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.call_ticket", ticketNum), 3f, Color.green);
                    if (ClientQueueManager.Instance.nextClientSound != null)
                        Managers.AudioManager.Instance?.PlayAudioClip2D(ClientQueueManager.Instance.nextClientSound);

                }
            }

            // Если после всех проверок клиента нет - очередь пуста, выходим сортировать бумаги
            if (client == null) break;
            servingClient = client;

            // --- ЛОГИКА НАГЛЕЦОВ (QUEUE JUMPERS) ---
            if (client != null && client.isQueueJumper)
            {
                // Читаем статы работника (если skills не назначен, берем базовые значения)
                // Предполагается, что у StaffController есть ссылка на CharacterSkills (например, skills)
                float currentSoftSkills = (staff.skills != null) ? staff.skills.softSkills : 0.5f;
                float currentCorruption = (staff.skills != null) ? staff.skills.corruption : 0.0f;

                // Проверка SoftSkills: хватит ли духу прогнать наглеца?
                bool willKickOut = Random.value < currentSoftSkills;

                if (willKickOut)
                {
                    staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.join_queue"), 3f, Color.red);
                    client.ShowThoughtBubble(UI.GameText.Get("client.registration.bureaucrats"), 2f);
                    
                    // Снимаем флаг наглеца (теперь он послушный, но злой)
                    client.isQueueJumper = false;
                    
                    // Отправляем обратно к аппарату за талоном
                    if (Objects.TicketTerminal.Instance != null && Objects.TicketTerminal.Instance.GetStandWaypoint() != null)
                    {
                        client.stateMachine.SetGoal(Objects.TicketTerminal.Instance.GetStandWaypoint());
                        client.stateMachine.SetState(ClientState.MovingToTerminal);
                    }
                    else
                    {
                        client.stateMachine.SetState(ClientState.Confused);
                    }
                    
                    desk.ClearClient(client);
                    yield return new WaitForSeconds(2f);
                    continue; // Пропускаем обслуживание, ждем нормального клиента
                }
                else
                {
                    // Согласился обслужить. Проверка на Взятку (Corruption)
                    bool willTakeBribe = Random.value < currentCorruption;
                    
                    if (willTakeBribe)
                    {
                        staff.thoughtBubble?.ShowPriorityMessage("$$$", 2f, Color.green);
                        if (Managers.PlayerWallet.Instance != null) Managers.PlayerWallet.Instance.AddMoney(50, UI.GameText.Get("ledger.bribe"), IncomeType.Shadow);
                        if (Managers.AudioManager.Instance != null) Managers.AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Money_Income, staff.transform.position);
                    }
                    else
                    {
                        staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.accept_jumper"), 2f, Color.gray);
                    }
                    
                    // Снимаем флаг, чтобы нормально обслужить
                    client.isQueueJumper = false;
                }
            }
            // --- КОНЕЦ ЛОГИКИ НАГЛЕЦОВ ---

            // 3. ЖДЕМ КЛИЕНТА (Таймаут уменьшен до 15 секунд!)
            float waitTimer = 0f;
            bool clientArrived = false;
            
            // Безопасное получение позиции точки для клиента
            Vector2 wpPos = desk.clientStandPoint != null ? (Vector2)desk.clientStandPoint.transform.position : (Vector2)desk.transform.position;

            while (waitTimer < 15f)
            {
                if (client == null || client.stateMachine == null || desk.CurrentClient != client) break;

                if (desk.IsClientPhysicallyReady && client.CanContinueService)
                {
                    clientArrived = true;
                    // --- ИСПРАВЛЕНИЕ: Принудительно останавливаем клиента у стола ---
                    client.stateMachine.StopAllActionCoroutines();
                    client.GetComponent<AgentMover>()?.Stop();
                    client.stateMachine.SetState(ClientState.AtRegistration);
                    // ---------------------------------------------------------------
                    break;
                }

                var state = client.stateMachine.GetCurrentState();
                if (state == ClientState.Leaving || state == ClientState.LeavingUpset || state == ClientState.Confused || state == ClientState.Enraged)
                {
                    break; // Клиент сорвался - не ждем
                }

                // Визуальная реакция ИИ - он ждет и раздражается!
                if (waitTimer == 5f) registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.where_client"), 2f, Color.yellow);
                if (waitTimer == 10f) registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.impatient"), 2f, new Color(1f, 0.5f, 0f));

                waitTimer += 0.5f;
                staff.CurrentSubStatus = UI.GameText.Get("staff.registration.waiting", client.name, CharacterActivityOverlay.DeskName(desk), waitTimer);
                yield return new WaitForSeconds(0.5f);
            }

            // 4. ТАЙМАУТ - СБРАСЫВАЕМ И БЕРЕМ СЛЕДУЮЩЕГО
            if (!clientArrived)
            {
                registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.no_show"), 2f, Color.red);
                desk.ClearClient(client);
                if (client != null && ClientQueueManager.Instance != null)
                {
                    client.stateMachine?.CancelServiceCall(registrar);
                }
                yield return new WaitForSeconds(1f);
                continue; // Крутим цикл заново
            }

            // 5. КЛИЕНТ ПОДОШЕЛ - НАЧИНАЕМ ОБСЛУЖИВАНИЕ
            staff.CurrentSubStatus = UI.GameText.Get("staff.service.serving", CharacterActivityOverlay.DeskName(desk), client.name);
            
            // --- ЖЕЛЕЗОБЕТОННЫЙ КЛИНЧ (НЕЗАВИСИМЫЕ КУБИКИ) ---
            var clinchConfig = Gameplay.AIBalanceConfig.Instance;
            Clinch.ClinchTarget clinchToTrigger = null;
            
            if (clinchConfig != null)
            {
                // Бросок для клиента
                bool rollClient = Random.value < (clinchConfig.clientClinchBaseChance * (1f + client.suetunFactor));
                // Бросок для персонала
                bool rollStaff = Random.value < (clinchConfig.staffClinchBaseChance * (1f - staff.skills.sedentaryResilience));
                
                Clinch.ClinchTarget clinch = null;
                
                if (rollClient)
                {
                    clinch = client?.GetComponent<Clinch.ClinchTarget>();
                    if (clinch == null) clinch = client?.gameObject.AddComponent<Clinch.ClinchTarget>();
                }
                else if (rollStaff)
                {
                    clinch = staff.GetComponent<Clinch.ClinchTarget>();
                    if (clinch == null) clinch = staff.gameObject.AddComponent<Clinch.ClinchTarget>();
                }

                if (clinch != null && !clinch.IsActive)
                {
                    clinchToTrigger = clinch;
                }
                
                if (clinchToTrigger != null)
                {
                    Debug.Log($"<color=orange>[Клинч-Тест]</color> Запускаем TriggerClinch(). Ставим обслуживание на паузу.");
                    clinchToTrigger.TriggerClinch();
                    registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.call_director"), 3f, Color.yellow);
                    
                    // ЖДЕМ РАЗРЕШЕНИЯ КЛИНЧА
                    yield return new WaitWhile(() => clinchToTrigger != null && clinchToTrigger.IsActive && CanServeAtWorkstation(client));
                    yield return WaitForClientService(client, 2.5f); // Даём реакции проявиться
                    
                    Debug.Log($"<color=orange>[Клинч-Тест]</color> Клинч завершен. Проверяем статус клиента.");
                    
                    // ПРОВЕРКА: Если клиент расстроился и ушел (провал клинча)
                    if (!CanServeAtWorkstation(client))
                    {
                        Debug.Log($"<color=orange>[Клинч-Тест]</color> Клиент ушел злым. Прерываем обслуживание.");
                        _lastServiceSuccess = false;
                        desk.ClearClient(client);
                        yield return new WaitForSeconds(1f);
                        continue; // Переходим к следующему клиенту в очереди
                    }
                }
            }
            // --- КОНЕЦ КЛИНЧА ---
            
            if (!CanServeAtWorkstation(client)) { FinishAction(false); yield break; }
            
            if (client.mainGoal == ClientGoal.GetArchiveRecord)
                yield return HandleArchiveRequest(registrar, client);
            else
                yield return HandleStandardRegistration(registrar, client);

            // 6. ОТЧЕТ НА СТОЛ (Обязательная генерация макулатуры)
            if (_lastServiceSuccess && desk.documentStack != null) desk.documentStack.AddDocumentToStack();

            // 7. ОЧИСТКА И ПЕРЕХОД К СЛЕДУЮЩЕМУ
            if (!_lastServiceSuccess && desk.CurrentClient == client)
                client.stateMachine?.CancelServiceCall(registrar);
            desk.ClearClient(client);
            if (_lastServiceSuccess && currentDurabilityComponent != null) currentDurabilityComponent.Degrade(2f);

            // Return to AI after each client: otherwise a continuous queue prevents shift end.
            FinishAction(_lastServiceSuccess);
            yield break;
        }

        FinishAction(true);
    }

    protected override void OnResourcesReleased()
    {
        if (servingDesk != null && servingClient != null && servingDesk.CurrentClient == servingClient)
        {
            servingDesk.ClearClient(servingClient);
            servingClient.stateMachine?.CancelServiceCall(staff as IServiceProvider);
        }
        if (activeArchiveRequest != null && !activeArchiveRequest.IsDelegated && !activeArchiveRequest.IsDelivered)
            ArchiveRequestManager.Instance?.CancelRequest(activeArchiveRequest);
        if (staff != null) staff.CurrentSubStatus = "";
    }

    private IEnumerator HandleStandardRegistration(ClerkController registrar, ClientPathfinding client)
    {
        client.ShowThoughtBubble(UI.GameText.Get("client.registration.hello"), 1.5f);
        yield return WaitForClientService(client, 1.5f);
        if (!CanServeAtWorkstation(client)) yield break;

        string[] greetings = { UI.GameText.Get("staff.registration.greeting_listening"), UI.GameText.Get("staff.registration.greeting_purpose"), UI.GameText.Get("staff.registration.greeting_need"), UI.GameText.Get("staff.registration.greeting_documents") };
        registrar.thoughtBubble?.ShowPriorityMessage(greetings[Random.Range(0, greetings.Length)], 2f, Color.white);
        yield return WaitForClientService(client, 2f);
        if (!CanServeAtWorkstation(client)) yield break;

        client.ShowThoughtBubble(GetClientGoalText(client.mainGoal), 2.5f);
        yield return WaitForClientService(client, 2.5f);
        if (!CanServeAtWorkstation(client)) yield break;

        float waitTime = Random.Range(1f, 2.5f) / currentEfficiency;
        registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.checking"), waitTime, Color.cyan);
        ShowProcessing(client.transform.position, waitTime);
        yield return WaitForClientService(client, waitTime);
        if (!CanServeAtWorkstation(client)) yield break;

        Waypoint correctDestination = DetermineCorrectGoalForClient(client);
        Waypoint actualDestination = correctDestination;

        float baseSuccessChance = 0.7f;
        float clientModifier = (client.babushkaFactor * 0.1f) - (client.suetunFactor * 0.2f);
        float finalChance = Mathf.Clamp(baseSuccessChance + clientModifier + registrar.redirectionBonus, 0.3f, 0.95f);

        bool isSuccess = true;
        if (Random.value > finalChance)
        {
            isSuccess = false;
            client.ApplyStressJump(0.05f);
            registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.unsure"), 2f, Color.yellow);
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
        registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.direction", destinationName), 3f, Color.green);
        yield return WaitForClientService(client, 1.5f);
        if (!CanServeAtWorkstation(client)) yield break;
        client.ShowThoughtBubble(UI.GameText.Get("client.registration.thanks"), 2f);

        if (client.stateMachine.MyQueueNumber != -1) ClientQueueManager.Instance?.RemoveClientFromQueue(client);
        
        client.stateMachine.SetGoal(actualDestination);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        
        // FIXED: Show feedback based on whether a mistake was made
        _lastServiceSuccess = isSuccess; // FIXED: Store result instead of showing duplicate icon
    }

    private IEnumerator HandleArchiveRequest(ClerkController registrar, ClientPathfinding client)
    {
        client.ShowThoughtBubble(UI.GameText.Get("client.archive.request"), 2.5f);
        yield return WaitForClientService(client, 2.5f);
        if (!CanServeAtWorkstation(client)) yield break;

        // FIXED: Проверяем, есть ли вообще архивариус на работе
        bool hasArchivist = ClientSpawner.GetServiceProviderAtDesk(3) is DirectorAvatarController director && director.isActiveAndEnabled ||
            HiringManager.Instance != null && HiringManager.Instance.AllStaff.Any(s => s != null && s.currentRole == StaffController.Role.Archivist && s.gameObject.activeInHierarchy && s.IsOnDuty());
        if (!hasArchivist || ArchiveRequestManager.Instance == null)
        {
            registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.unavailable"), 3f, Color.red);
            yield return WaitForClientService(client, 2f);
            if (!CanServeAtWorkstation(client)) yield break;
            client.ApplyStressJump(client.stressJump_Refusal * 1.5f);
            if (client.stateMachine.MyQueueNumber != -1) ClientQueueManager.Instance?.RemoveClientFromQueue(client);
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            _lastServiceSuccess = false;
            yield break;
        }

        // --- НОВАЯ ЛОГИКА ДЕЛЕГИРОВАНИЯ ---
        bool hasCouriers = HiringManager.Instance != null && HiringManager.Instance.AllStaff.Any(s => s != null &&
            (s.currentRole == StaffController.Role.Intern || s.currentRole == StaffController.Role.OfficeManager) &&
            s.gameObject.activeInHierarchy && s.IsOnDuty());
        
        // Шанс делегировать (пока 50%, если есть свободные руки. Позже можно привязать к softSkills)
        bool willDelegate = hasCouriers && ClientQueueManager.Instance?.mainWaitingZone != null && (Random.value < 0.5f);

        if (willDelegate)
        {
            registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.call_courier"), 3f, Color.cyan);
            yield return WaitForClientService(client, 1.5f);
            if (!CanServeAtWorkstation(client)) yield break;
            
            var queue = ClientQueueManager.Instance;
            var waitingPoint = queue != null && queue.mainWaitingZone != null ? queue.ChooseNewGoal(client) : null;
            if (waitingPoint != null && ArchiveRequestManager.Instance != null)
            {
                activeArchiveRequest = ArchiveRequestManager.Instance.CreateRequest(registrar, client, true);
                if (activeArchiveRequest == null) yield break;
                client.ShowThoughtBubble(UI.GameText.Get("client.archive.wait_in_hall"), 2.5f);
                queue.ServiceFinishedForNumber(client.stateMachine.MyQueueNumber);
                client.stateMachine.SetGoal(waitingPoint);
                client.stateMachine.SetState(ClientState.MovingToGoal);
                registrar.assignedWorkstation.ClearClient(client);
                _lastServiceSuccess = true;
                registrar.SetState(ClerkController.ClerkState.ReturningToWork);
                yield break;
            }
        }
        // --- КОНЕЦ ЛОГИКИ ДЕЛЕГИРОВАНИЯ ---

        registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.requesting"), 3f, Color.yellow);
        yield return WaitForClientService(client, 1.5f);
        if (!CanServeAtWorkstation(client)) yield break;

        if (ArchiveRequestManager.Instance == null) {
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            yield break;
        }

        var archivistDesk = ScenePointsRegistry.Instance?.GetServicePointByID(3);
        if (archivistDesk == null || archivistDesk.clerkStandPoint == null)
        {
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString());
            yield break;
        }

        activeArchiveRequest = ArchiveRequestManager.Instance.CreateRequest(registrar, client, false);
        if (activeArchiveRequest == null) yield break;
        registrar.SetState(ClerkController.ClerkState.WaitingForArchive);
        client.stateMachine.SetState(ClientState.WaitingForDocument);
        yield return registrar.MoveToTarget(archivistDesk.clerkStandPoint.position, ClerkController.ClerkState.WaitingForArchive.ToString());
        if (!CanServeAtWorkstation(client) || archivistDesk == null || archivistDesk.clerkStandPoint == null ||
            Vector2.Distance(registrar.transform.position, archivistDesk.clerkStandPoint.position) > 1f)
        {
            Debug.LogWarning("[ArchiveTrace] Registrar could not reach archive desk; request cancelled.", registrar);
            yield break;
        }


        float waitTimer = 0f;
        float maxWaitTime = Gameplay.AIBalanceConfig.Instance != null ? Gameplay.AIBalanceConfig.Instance.archiveWaitTimeout : 60f;
        bool requestFulfilled = false;
        ArchiveRequest request = activeArchiveRequest;

        int lastBubbleQuarter = 0;

        while(waitTimer < maxWaitTime)
        {
            if (!CanServeAtWorkstation(client) || ArchiveRequestManager.Instance == null || request == null || request.IsCancelled) break;

            if (request != null && request.IsFulfilled) { requestFulfilled = true; break; }


            // --- Flavor thoughts ---
            float progress = waitTimer / maxWaitTime;
            int currentQuarter = Mathf.FloorToInt(progress * 4); // 0, 1, 2, 3
            if (currentQuarter > lastBubbleQuarter)
            {
                lastBubbleQuarter = currentQuarter;
                string[] waitingThoughts = { UI.GameText.Get("staff.archive.wait_where"), UI.GameText.Get("staff.archive.wait_eager"), UI.GameText.Get("staff.archive.wait_long"), UI.GameText.Get("staff.archive.wait_asleep") };
                registrar.thoughtBubble?.ShowPriorityMessage(waitingThoughts[Random.Range(0, waitingThoughts.Length)], 2f, Color.yellow);
            }
            // -----------------------

            waitTimer += Time.deltaTime;
            yield return null;
        }

        if (requestFulfilled)
        {
            registrar.GetComponent<StackHolder>()?.ShowSingleDocumentSprite(); 
            registrar.SetState(ClerkController.ClerkState.ReturningToWork); 
            yield return registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString());
            registrar.GetComponent<StackHolder>()?.HideStack(); 

            var cashierDestination = ClientSpawner.GetCashierZone()?.waitingWaypoint;
            if (CanServeAtWorkstation(client) && IsAtWorkstation && cashierDestination != null &&
                ArchiveRequestManager.Instance != null && client.stateMachine.GetCurrentState() == ClientState.WaitingForDocument)
            {
                client.billToPay += 150;
                ArchiveRequestManager.Instance.CompleteDelivery(request);
                ClientQueueManager.Instance?.RemoveClientFromQueue(client);
                client.stateMachine.SetGoal(cashierDestination);
                client.stateMachine.SetState(ClientState.MovingToGoal);
                _lastServiceSuccess = true; // FIXED: Store success
            }
        }
        else 
        {
            if (CanServeAtWorkstation(client))
            {
                registrar.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.timeout"), 3f, Color.red);
                yield return WaitForClientService(client, 1.0f);
                if (!CanServeAtWorkstation(client)) yield break;
                client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                client.stateMachine.SetState(ClientState.LeavingUpset);
                _lastServiceSuccess = false; // FIXED: Store failure
            }
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString());
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
            case ClientGoal.DirectorApproval:
            case ClientGoal.DirectorAudience: return ClientSpawner.Instance?.directorReceptionZone?.waitingWaypoint;
            default: return ClientSpawner.Instance?.exitWaypoint;
        }
    }

    private string GetClientGoalText(ClientGoal goal)
    {
        switch(goal)
        {
            case ClientGoal.GetCertificate1: return UI.GameText.Get("client.goal.certificate1");
            case ClientGoal.GetCertificate2: return UI.GameText.Get("client.goal.certificate2");
            case ClientGoal.PayTax: return UI.GameText.Get("client.goal.tax");
            case ClientGoal.GetArchiveRecord: return UI.GameText.Get("client.goal.archive");
            case ClientGoal.VisitToilet: return UI.GameText.Get("client.goal.toilet");
            case ClientGoal.DirectorApproval: return UI.GameText.Get("client.goal.director_signature");
            case ClientGoal.DirectorAudience: return UI.GameText.Get("client.goal.director_meeting");
            case ClientGoal.AskAndLeave: return UI.GameText.Get("client.goal.ask");
            default: return UI.GameText.Get("client.goal.ask");
        }
    }
}
