using UnityEngine;
using System.Collections;
using Managers;
using Utilities;

public class DelegateArchiveFetchExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Курьерскую задачу нельзя бросать на полпути

    protected override IEnumerator ActionRoutine()
    {
        var request = ArchiveRequestManager.Instance?.GetNextDelegatedRequest();
        if (request == null) { FinishAction(false); yield break; }

        var intern = staff as IServiceProvider; // InternController реализует IServiceProvider
        if (intern == null) { FinishAction(false); yield break; }

        // 1. Идем к Регистратору за указаниями
        staff.thoughtBubble?.ShowPriorityMessage("Иду за поручением!", 2f, Color.cyan);
        yield return staff.StartCoroutine(staff.MoveToTarget(request.RequestingRegistrar.transform.position, "Walking"));
        
        staff.thoughtBubble?.ShowPriorityMessage("Принял, бегу в архив!", 2f, Color.green);
        yield return new WaitForSeconds(1f);

        // 2. Идем в Архив
        var archivistDesk = ScenePointsRegistry.Instance?.GetServicePointByID(3);
        if (archivistDesk == null) { FinishAction(false); yield break; }
        
        yield return staff.StartCoroutine(staff.MoveToTarget(archivistDesk.clerkStandPoint.position, "WaitingForArchive"));

        // 3. Ждем документ у архива
        float waitTimer = 0f;
        float maxWaitTime = Gameplay.AIBalanceConfig.Instance != null ? Gameplay.AIBalanceConfig.Instance.archiveWaitTimeout : 60f;
        bool isFulfilled = false;
        int lastBubbleQuarter = 0;

        while (waitTimer < maxWaitTime)
        {
            if (request.WaitingClient == null) break;
            var cState = request.WaitingClient.stateMachine.GetCurrentState();
            if (cState == ClientState.Leaving || cState == ClientState.LeavingUpset) break;

            if (request.IsFulfilled) { isFulfilled = true; break; }

            float progress = waitTimer / maxWaitTime;
            int currentQuarter = Mathf.FloorToInt(progress * 4);
            if (currentQuarter > lastBubbleQuarter)
            {
                lastBubbleQuarter = currentQuarter;
                string[] waitingThoughts = { "Где же он...", "Жду-не дождусь!", "Как долго...", "Архив уснул там?" };
                staff.thoughtBubble?.ShowPriorityMessage(waitingThoughts[Random.Range(0, waitingThoughts.Length)], 2f, Color.yellow);
            }

            waitTimer += Time.deltaTime;
            yield return null;
        }

        if (!isFulfilled)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Архив не отвечает...", 2f, Color.red);
            FinishAction(false);
            yield break;
        }

        // 4. Документ готов, берем папку
        staff.GetComponent<StackHolder>()?.ShowSingleDocumentSprite();
        staff.thoughtBubble?.ShowPriorityMessage("Несу справку!", 2f, Color.green);

        // 5. Идем обратно в зал (встаем чуть сбоку от регистратора, чтобы не мешать ему обслуживать других)
        Vector3 meetPos = request.RequestingRegistrar.transform.position + new Vector3(1.5f, -1.0f, 0f);
        yield return staff.StartCoroutine(staff.MoveToTarget(meetPos, "Walking"));

        // 6. Делаем VIP-вызов клиента из зала
        bool called = ClientQueueManager.Instance.CallSpecificClient(request.WaitingClient, intern);
        if (!called)
        {
            staff.GetComponent<StackHolder>()?.HideStack();
            FinishAction(false);
            yield break;
        }

        // 7. Ждем, пока клиент подойдет к стажеру
        float clientWait = 0f;
        bool clientArrived = false;
        while (clientWait < 15f)
        {
            if (request.WaitingClient == null) break;
            if (Vector2.Distance(request.WaitingClient.transform.position, staff.transform.position) < 1.5f)
            {
                clientArrived = true;
                break;
            }
            clientWait += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        // 8. Передача документа и прощание
        if (clientArrived)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Ваша справка. Пройдите в кассу.", 3f, Color.green);
            request.WaitingClient.billToPay += 150;
            request.WaitingClient.stateMachine.SetGoal(ClientSpawner.GetCashierZone()?.waitingWaypoint);
            request.WaitingClient.stateMachine.SetState(ClientState.MovingToGoal);
            staff.ShowActionEffect(true);
        }
        else
        {
            staff.thoughtBubble?.ShowPriorityMessage("Клиент не пришел...", 2f, Color.red);
            staff.ShowActionEffect(false);
        }

        staff.GetComponent<StackHolder>()?.HideStack();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}
