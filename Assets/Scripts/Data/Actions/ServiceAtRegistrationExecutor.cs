// Файл: Assets/Scripts/Data/Actions/ServiceAtRegistrationExecutor.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ServiceAtRegistrationExecutor : ActionExecutor
{
    // Регистрация - важное действие, которое не должно прерываться легко,
    // особенно когда идет взаимодействие с клиентом или ожидание архива.
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        // 1. Получаем ссылку на регистратора и проверяем валидность
        var registrar = staff as ClerkController;
        if (registrar == null)
        {
            Debug.LogError($"[ServiceAtRegistrationExecutor] Ошибка: Staff не является ClerkController. Staff: {staff?.name}");
            FinishAction(false); // Завершаем с ошибкой
            yield break; // Выходим из корутины
        }
        if (registrar.assignedWorkstation == null)
        {
             Debug.LogError($"[ServiceAtRegistrationExecutor] Ошибка: Регистратору {registrar.name} не назначено рабочее место.");
             FinishAction(false);
             yield break;
        }


        // 2. Находим зону обслуживания и клиента в ней
        var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault(); // Берем первого клиента, занявшего место

        // Если зона не найдена или в ней нет клиента
        if (client == null)
        {
            // Это может случиться, если клиент ушел/был удален между проверкой условий и выполнением
            // Debug.LogWarning($"[ServiceAtRegistrationExecutor] Нет клиента для обслуживания у {registrar.name} в момент начала действия. Зона: {zone?.name}");
            FinishAction(false); // Завершаем, так как нет клиента
            yield break; // Выходим
        }

        Debug.Log($"[ServiceAtRegistrationExecutor] {registrar.name} начинает обслуживание клиента {client.name} (Цель: {client.mainGoal})");

        // 3. Устанавливаем статус "Работает"
        registrar.SetState(ClerkController.ClerkState.Working);

        // 4. Проверяем, есть ли у регистратора активное действие "Сделать запрос в архив"
        bool canMakeArchiveRequest = registrar.activeActions != null && registrar.activeActions.Any(a => a != null && a.actionType == ActionType.MakeArchiveRequest);

        // 5. Выбираем ветку логики: Архивный запрос ИЛИ Стандартная регистрация
        // Если цель клиента - получить запись из архива И регистратор МОЖЕТ сделать запрос
        if (client.mainGoal == ClientGoal.GetArchiveRecord && canMakeArchiveRequest)
        {
            // Запускаем специальную корутину для обработки архивного запроса
            yield return staff.StartCoroutine(HandleArchiveRequest(registrar, client));
            // Завершение действия (FinishAction) происходит ВНУТРИ HandleArchiveRequest
        }
        else // Во всех остальных случаях (другая цель ИЛИ регистратор не может делать запрос в архив)
        {
            // Запускаем стандартную корутину регистрации/направления
            yield return staff.StartCoroutine(HandleStandardRegistration(registrar, client));

            // Завершаем здесь, ТОЛЬКО если действие не завершилось внутри HandleStandardRegistration
            // (например, при отправке архивного клиента домой)
            // Проверяем, существует ли еще этот Executor (не был ли он уже завершен)
            if (this != null)
            {
                // Если Executor еще существует, значит, стандартная регистрация прошла успешно (клиент направлен)
                registrar.ServiceComplete(); // Засчитываем обслуживание
                ExperienceManager.Instance?.GrantXP(staff, actionData.actionType); // Даем опыт
                FinishAction(true); // Завершаем действие успешно
                 Debug.Log($"[ServiceAtRegistrationExecutor] Стандартное обслуживание {client.name} завершено {registrar.name}.");
            }
            else
            {
                 Debug.Log($"[ServiceAtRegistrationExecutor] Действие для {client.name} было завершено внутри HandleStandardRegistration.");
            }
        }
    } // Конец ActionRoutine

    // Корутина для стандартной регистрации (направления клиента)
    private IEnumerator HandleStandardRegistration(ClerkController registrar, ClientPathfinding client)
    {
        // Небольшая задержка для имитации работы
        yield return new WaitForSeconds(Random.Range(1f, 2.5f));

        // Проверяем состояние клиента еще раз перед действием
         if (client == null || client.stateMachine == null) {
              Debug.LogWarning("[HandleStandardRegistration] Клиент исчез во время ожидания.");
              FinishAction(false); // Завершаем, так как клиента больше нет
              yield break;
         }

        // Проверяем цель "Архив" ДО основного определения цели
        if (client.mainGoal == ClientGoal.GetArchiveRecord)
        {
             // Если цель - архив, а мы попали сюда (т.к. canMakeArchiveRequest == false),
             // значит, эта функция у регистратора отключена. Отправляем клиента домой.
             registrar.thoughtBubble?.ShowPriorityMessage("Извините, архив\nсегодня не работает.", 3f, Color.red);
             yield return new WaitForSeconds(1.5f); // Даем время прочитать

             if (client == null || client.stateMachine == null) yield break; // Перепроверка

             // Снимаем с очереди, если он там был
             if (client.stateMachine.MyQueueNumber != -1)
             {
                 ClientQueueManager.Instance?.RemoveClientFromQueue(client);
             }
             client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
             client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint); // Используем безопасный доступ
             client.stateMachine.SetState(ClientState.LeavingUpset);
              Debug.Log($"[HandleStandardRegistration] Клиент {client.name} отправлен домой (архив недоступен).");


             FinishAction(true); // Завершаем Executor (действие выполнено - клиент отправлен)
             yield break; // Выходим из корутины
        }

        // Определяем правильный пункт назначения для клиента
        Waypoint correctDestination = DetermineCorrectGoalForClient(client);
        Waypoint actualDestination = correctDestination; // По умолчанию отправляем правильно

        // Рассчитываем шанс ошибки при направлении
        float baseSuccessChance = 0.7f;
        float clientModifier = (client.babushkaFactor * 0.1f) - (client.suetunFactor * 0.2f);
        float registrarBonus = registrar.redirectionBonus;
        float finalChance = Mathf.Clamp(baseSuccessChance + clientModifier + registrarBonus, 0.3f, 0.95f);

        // Проверяем, произошла ли ошибка
        if (Random.value > finalChance)
        {
            Debug.LogWarning($"[Registration] ПРОВАЛ НАПРАВЛЕНИЯ! Регистратор {registrar.name} ошибся с клиентом {client.name}. Шанс был {finalChance:P0}");
            // Собираем список возможных НЕПРАВИЛЬНЫХ пунктов назначения
            List<Waypoint> possibleDestinations = new List<Waypoint>();
            if (ClientSpawner.Instance != null) {
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category1DeskZones)?.waitingWaypoint);
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category2DeskZones)?.waitingWaypoint);
                possibleDestinations.Add(ClientSpawner.GetQuietestZone(ClientSpawner.Instance.cashierZones)?.waitingWaypoint);
                if (ClientSpawner.Instance.toiletZone != null) possibleDestinations.Add(ClientSpawner.Instance.toiletZone.waitingWaypoint);
            }
             if (ClientQueueManager.Instance != null) {
                possibleDestinations.Add(ClientQueueManager.Instance.ChooseNewGoal(client)); // Вернуться в очередь ожидания
             }

            // Убираем null значения и правильный пункт назначения
            possibleDestinations.RemoveAll(item => item == null || item == correctDestination);

            if (possibleDestinations.Count > 0)
            {
                actualDestination = possibleDestinations[Random.Range(0, possibleDestinations.Count)];
                 Debug.Log($" -> Отправлен неверно к {actualDestination.name}");
            } else {
                 Debug.Log(" -> Не найдено неверных точек, отправлен правильно.");
            }
        } else {
             Debug.Log($"[Registration] Успешное направление {client.name} регистратором {registrar.name}. Шанс был {finalChance:P0}");
        }


         // Проверяем клиента еще раз перед отправкой
         if (client == null || client.stateMachine == null || actualDestination == null) {
              Debug.LogWarning("[HandleStandardRegistration] Клиент или точка назначения исчезли перед отправкой.");
              FinishAction(false); // Завершаем, так как что-то пошло не так
              yield break;
         }


        // Формируем сообщение для клиента
        string destinationName = string.IsNullOrEmpty(actualDestination.friendlyName) ?
                                actualDestination.name : actualDestination.friendlyName;
        string directionMessage = $"Пройдите, пожалуйста, к\n'{destinationName}'";
        registrar.thoughtBubble?.ShowPriorityMessage(directionMessage, 3f, Color.white);

        // Если клиент был в основной очереди, удаляем его
        if (client.stateMachine.MyQueueNumber != -1)
        {
            ClientQueueManager.Instance?.RemoveClientFromQueue(client);
        }

        // Отправляем клиента
        client.stateMachine.SetGoal(actualDestination);
        client.stateMachine.SetState(ClientState.MovingToGoal);
         Debug.Log($" -> Клиент {client.name} отправлен к {actualDestination.name}.");

        // НЕ ВЫЗЫВАЕМ FinishAction здесь, он будет вызван в основном ActionRoutine
    } // Конец HandleStandardRegistration

    // Корутина для обработки запроса в архив
    private IEnumerator HandleArchiveRequest(ClerkController registrar, ClientPathfinding client)
    {
        // Проверяем наличие менеджера запросов
        if (ArchiveRequestManager.Instance == null) {
            Debug.LogError("[HandleArchiveRequest] ArchiveRequestManager не найден! Невозможно обработать запрос.");
            // Отправляем клиента домой
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            FinishAction(true); // Завершаем действие (клиент отправлен)
            yield break;
        }
         // Проверяем клиента
         if (client == null || client.stateMachine == null) {
             Debug.LogWarning("[HandleArchiveRequest] Клиент исчез до создания запроса.");
             FinishAction(false);
             yield break;
         }

        // Создаем запрос
        ArchiveRequestManager.Instance.CreateRequest(registrar, client);
        Debug.Log($"[HandleArchiveRequest] Запрос создан для {client.name} регистратором {registrar.name}.");
        // Устанавливаем состояния ожидания
        registrar.SetState(ClerkController.ClerkState.WaitingForArchive);
        client.stateMachine.SetState(ClientState.WaitingForDocument);

        // Находим стол архивариуса (ID=3)
        var archivistDesk = ScenePointsRegistry.Instance?.GetServicePointByID(3);
        if (archivistDesk == null)
        {
             Debug.LogError("[HandleArchiveRequest] Стол архивариуса (ID=3) не найден! Некуда идти за запросом.");
            // Отправляем клиента домой
             if (client != null && client.stateMachine != null) { // Перепроверка
                 client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                 client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                 client.stateMachine.SetState(ClientState.LeavingUpset);
             }
            // Возвращаем регистратора к работе
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
            FinishAction(true); // Завершаем действие
            yield break;
        }

        // Отправляем регистратора к столу архивариуса
        Debug.Log($"[HandleArchiveRequest] {registrar.name} идет к столу архивариуса ({archivistDesk.name}).");
        yield return staff.StartCoroutine(registrar.MoveToTarget(archivistDesk.clerkStandPoint.position, ClerkController.ClerkState.WaitingForArchive.ToString()));
         Debug.Log($"[HandleArchiveRequest] {registrar.name} прибыл к столу архивариуса и ожидает.");

        // Ожидаем выполнения запроса (максимум 60 секунд)
        float waitTimer = 0f;
        float maxWaitTime = 60f;
        bool requestFulfilled = false;
        // --- ИЗМЕНЕНИЕ: Получаем СЛЕДУЮЩИЙ запрос из очереди, предполагая, что он наш ---
        // Это менее надежно, если запросы могут обрабатываться не по порядку, но проще в реализации без GetRequestForClient
        ArchiveRequest request = ArchiveRequestManager.Instance.GetOurRequest(registrar); // Используем гипотетический метод для поиска *нашего* запроса
        // --- КОНЕЦ ИЗМЕНЕНИЯ ---

        while(waitTimer < maxWaitTime)
        {
             // Проверяем клиента
             if (client == null || client.stateMachine == null) {
                 Debug.LogWarning($"[HandleArchiveRequest] Клиент {client?.name ?? "N/A"} исчез во время ожидания архива.");
                 // ArchiveRequestManager.Instance.CancelRequest(request); // Метод CancelRequest отсутствует
                 // Просто выходим из ожидания
                 break;
             }
             // Проверяем состояние клиента (мог уйти сам)
             ClientState clientState = client.stateMachine.GetCurrentState();
             if (clientState == ClientState.Leaving || clientState == ClientState.LeavingUpset) {
                 Debug.LogWarning($"[HandleArchiveRequest] Клиент {client.name} ушел ({clientState}) во время ожидания архива.");
                 // ArchiveRequestManager.Instance.CancelRequest(request); // Метод CancelRequest отсутствует
                 break; // Выходим из ожидания
             }

             // --- ИЗМЕНЕНИЕ: Проверяем статус НАШЕГО запроса ---
            if (request != null && request.IsFulfilled)
            {
                requestFulfilled = true;
                Debug.Log($"[HandleArchiveRequest] Запрос для {client.name} выполнен.");
                break; // Выходим из цикла, если запрос выполнен
            }
             // Если наш запрос еще не взят или не выполнен, проверяем, есть ли вообще запросы в очереди
             // Это нужно, чтобы GetNextRequest не "съел" чужой запрос, если наш еще не обработан
             else if (request == null && ArchiveRequestManager.Instance.HasPendingRequests())
             {
                 // Пытаемся получить наш запрос снова, если он еще не был взят архивариусом
                 request = ArchiveRequestManager.Instance.GetOurRequest(registrar);
             }
             // --- КОНЕЦ ИЗМЕНЕНИЯ ---

            waitTimer += Time.deltaTime; // Увеличиваем таймер
            yield return null; // Ждем следующего кадра
        }

        // Обработка результата ожидания
        if (requestFulfilled) // Запрос выполнен успешно
        {
             Debug.Log($"[HandleArchiveRequest] {registrar.name} получил документ для {client.name}.");
            registrar.GetComponent<StackHolder>()?.ShowSingleDocumentSprite(); // Показываем документ
            registrar.SetState(ClerkController.ClerkState.ReturningToWork); // Статус "Возвращается к работе"
            // Возвращаемся на свое рабочее место
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
            registrar.GetComponent<StackHolder>()?.HideStack(); // Прячем документ

            // Проверяем, что клиент все еще здесь и ждет
            if (client != null && client.stateMachine != null && client.stateMachine.GetCurrentState() == ClientState.WaitingForDocument)
            {
                client.billToPay += 150; // Выставляем счет
                Debug.Log($"[HandleArchiveRequest] Клиент {client.name} получил документ и отправлен в кассу (Счет: {client.billToPay}).");
                // Отправляем клиента в кассу
                client.stateMachine.SetGoal(ClientSpawner.GetCashierZone()?.waitingWaypoint); // Безопасный доступ
                client.stateMachine.SetState(ClientState.MovingToGoal);
            } else {
                // Если клиент ушел, пока регистратор возвращался
                 Debug.LogWarning($"[HandleArchiveRequest] Клиент {client?.name ?? "N/A"} ушел до получения документа из архива от {registrar.name}.");
                 // Документ просто исчезает
            }
        }
        else // Запрос НЕ был выполнен (таймаут или клиент ушел)
        {
             Debug.LogWarning($"[HandleArchiveRequest] Запрос для {client?.name ?? "N/A"} не выполнен (Таймаут: {waitTimer >= maxWaitTime}).");
             // Если клиент еще не ушел сам
             if (client != null && client.stateMachine != null && client.stateMachine.GetCurrentState() != ClientState.Leaving && client.stateMachine.GetCurrentState() != ClientState.LeavingUpset)
             {
                 registrar.thoughtBubble?.ShowPriorityMessage("Архив не отвечает...\nИзвините.", 3f, Color.red);
                 yield return new WaitForSeconds(1.0f); // Короткая пауза
                 // Отправляем клиента домой
                 client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                 client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                 client.stateMachine.SetState(ClientState.LeavingUpset);
                  Debug.Log($" -> Клиент {client.name} отправлен домой из-за таймаута архива.");
             }
             // Возвращаем регистратора к работе в любом случае
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
        }

        // Завершаем действие регистратора в любом случае после возвращения
        registrar.ServiceComplete(); // Засчитываем обслуживание (даже если клиент ушел, попытка была)
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType); // Даем опыт
        FinishAction(true); // Завершаем выполнение Executor'а
        Debug.Log($"[HandleArchiveRequest] Завершение действия для регистратора {registrar.name}.");

    } // Конец HandleArchiveRequest


    // Вспомогательный метод для определения правильной цели клиента
    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
         // Проверка на null
        if (client == null || ClientSpawner.Instance == null) {
            Debug.LogError("[DetermineCorrectGoal] Client или ClientSpawner == null!");
            return ClientSpawner.Instance?.exitWaypoint; // Попытка отправить на выход в случае ошибки
        }


        // Если есть неоплаченный счет, всегда отправляем в кассу
        if (client.billToPay > 0)
        {
             LimitedCapacityZone cashierZone = ClientSpawner.GetCashierZone();
             if (cashierZone == null || cashierZone.waitingWaypoint == null) {
                 Debug.LogError($"Касса или ее точка ожидания не найдена для клиента {client.name} со счетом!");
                 return ClientSpawner.Instance.exitWaypoint; // Отправляем домой, если кассы нет
             }
             return cashierZone.waitingWaypoint;
        }

        // Определяем цель в зависимости от mainGoal клиента
        LimitedCapacityZone targetZone = null;
        Waypoint targetWaypoint = null;

        switch (client.mainGoal)
        {
            case ClientGoal.PayTax:
                 targetZone = ClientSpawner.GetCashierZone();
                 targetWaypoint = targetZone?.waitingWaypoint;
                 if (targetWaypoint == null) Debug.LogError($"Цель PayTax: Касса или ее точка ожидания не найдена для {client.name}!");
                break;
            case ClientGoal.GetCertificate1:
                 targetZone = ClientSpawner.GetDesk1Zone();
                 targetWaypoint = targetZone?.waitingWaypoint;
                 if (targetWaypoint == null) Debug.LogError($"Цель GetCertificate1: Зона Desk1 или ее точка ожидания не найдена для {client.name}!");
                break;
            case ClientGoal.GetCertificate2:
                 targetZone = ClientSpawner.GetDesk2Zone();
                 targetWaypoint = targetZone?.waitingWaypoint;
                  if (targetWaypoint == null) Debug.LogError($"Цель GetCertificate2: Зона Desk2 или ее точка ожидания не найдена для {client.name}!");
               break;

            // Случай GetArchiveRecord обрабатывается ранее
            case ClientGoal.AskAndLeave: // Если цель просто спросить
            case ClientGoal.VisitToilet: // Если цель была туалет (но он уже у регистратора)
            default: // Все остальные неопределенные цели
                client.isLeavingSuccessfully = true;
                client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                targetWaypoint = ClientSpawner.Instance.exitWaypoint; // Отправляем на выход
                if (targetWaypoint == null) Debug.LogError($"Точка выхода (ExitWaypoint) не найдена для {client.name}!");
                break;
        }

        // Возвращаем найденную точку или точку выхода в случае ошибки
        return targetWaypoint ?? ClientSpawner.Instance.exitWaypoint;
    } // Конец DetermineCorrectGoalForClient

} // Конец класса ServiceAtRegistrationExecutor