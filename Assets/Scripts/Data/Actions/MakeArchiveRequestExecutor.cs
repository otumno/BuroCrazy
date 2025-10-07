using UnityEngine;
using System.Collections;
using System.Linq;

public class MakeArchiveRequestExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
{
    var registrar = staff as ClerkController;
    var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedServicePoint.deskId);
    var client = zone.GetOccupyingClients().FirstOrDefault(c => c.mainGoal == ClientGoal.GetArchiveRecord);

    if (registrar == null || client == null) { FinishAction(); yield break; }

    // ... (Код создания запроса и похода к архивариусу остается тем же) ...
    var request = new ArchiveRequest { RequestingRegistrar = registrar, WaitingClient = client };
    ArchiveRequestManager.Instance.CreateRequest(registrar, client);

    registrar.SetState(ClerkController.ClerkState.WaitingForArchive);
    client.stateMachine.SetState(ClientState.WaitingForDocument);

    var archivistDesk = ScenePointsRegistry.Instance.GetServicePointByID(3); 
    if (archivistDesk == null) { FinishAction(); yield break; }

    yield return staff.StartCoroutine(registrar.MoveToTarget(archivistDesk.clerkStandPoint.position, ClerkController.ClerkState.WaitingForArchive));

    // --- НАЧАЛО ЗАЩИТЫ ОТ ДУРАКА (ТАЙМ-АУТ) ---

    float waitTimer = 0f;
    float maxWaitTime = 60f; // Ждем максимум 60 секунд
    bool requestFulfilled = false;

    while(waitTimer < maxWaitTime)
    {
        if (request.IsFulfilled)
        {
            requestFulfilled = true;
            break;
        }
        waitTimer += Time.deltaTime;
        yield return null;
    }

    // --- КОНЕЦ ЗАЩИТЫ ОТ ДУРАКА (ТАЙМ-АУТ) ---

    if (requestFulfilled)
    {
        // УСПЕХ: Архивариус принес документ. Возвращаемся на свое место.
        registrar.GetComponent<StackHolder>().ShowSingleDocumentSprite(); 
        registrar.SetState(ClerkController.ClerkState.ReturningToWork);
        yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedServicePoint.clerkStandPoint.position, ClerkController.ClerkState.Working));

        // Отдаем документ клиенту
        registrar.GetComponent<StackHolder>().HideStack();
        // ... (логика передачи документа клиенту) ...

        client.billToPay += 150; 
        client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        client.stateMachine.SetState(ClientState.MovingToGoal);
    }
    else
    {
        // ПРОВАЛ: Время ожидания истекло
        Debug.LogWarning($"Регистратор {registrar.name} не дождался Архивариуса и отменил запрос.");
        registrar.thoughtBubble?.ShowPriorityMessage("Архив не отвечает...\nИзвините.", 3f, Color.red);

        // Возвращаемся на рабочее место
        registrar.SetState(ClerkController.ClerkState.ReturningToWork);
        yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedServicePoint.clerkStandPoint.position, ClerkController.ClerkState.Working));

        // Отправляем расстроенного клиента на выход
        client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
        client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
        client.stateMachine.SetState(ClientState.LeavingUpset);
    }

    // Завершаем
    registrar.ServiceComplete();
    FinishAction();
}
}