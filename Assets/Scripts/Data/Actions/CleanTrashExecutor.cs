using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;

public class CleanTrashExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Уборку на полпути не бросаем

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker) || MessManager.Instance == null)
        { FinishAction(false); yield break; }

        MessPoint messPoint = MessManager.Instance.GetSortedMessList(worker.transform.position, availableOnly: true)
            .FirstOrDefault(m => m != null && m.type == MessPoint.MessType.Trash);
        
        // ВАЖНО: Если мусора нет, ОБЯЗАТЕЛЬНО FinishAction(false) перед выходом!
        if (messPoint == null || !messPoint.TryReserve(worker)) { FinishAction(false); yield break; }
        RegisterCleanup(() => { if (messPoint != null) messPoint.ReleaseReservation(worker); });

        // Идем к мусору с таймаутом
        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        
        float moveTimeout = 15f;
        staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, messPoint.transform.position, staff.gameObject));
        
        while (staff.AgentMover.IsMoving() && moveTimeout > 0)
        {
            moveTimeout -= Time.deltaTime;
            if (messPoint == null) break; // Кто-то другой убрал мусор, пока мы шли
            yield return null;
        }

        staff.AgentMover.Stop();

        // Проверяем, не исчез ли мусор и дошли ли мы
        if (messPoint == null)
        {
            FinishAction(true); // Кто-то убрал за нас, считаем успехом
            yield break;
        }

        if (Vector2.Distance(staff.transform.position, messPoint.transform.position) > 2.5f)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Не достать...", 2f, Color.red);
            FinishAction(false); // Не смогли дойти
            yield break;
        }

        // --- ПРОЦЕСС УБОРКИ ---
        worker.SetState(ServiceWorkerController.WorkerState.Cleaning);
        
        bool canFindValuables = staff.activeActions.Any(a => a != null && a.actionType == ActionType.FindValuablesInTrash);
        float chance = 0.1f + (worker.skills.paperworkMastery * 0.4f);
        int moneyFound = 0;

        if (canFindValuables && Random.value < chance)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Ого, да это же...", 2f, Color.green);
            yield return new WaitForSeconds(2f);

            // Commit the reward with disposal; cancelling and retrying the same pile must not mint money.
            moneyFound = Random.Range(10, 51);
        }
        else
        {
            staff.thoughtBubble?.ShowPriorityMessage("*Вжик-вжик*", 2f, Color.cyan);
            yield return new WaitForSeconds(worker.cleaningTimeTrash);
        }

        var dumpster = ScenePointsRegistry.Instance?.dumpsterPoint;
        if (dumpster == null || messPoint == null) { FinishAction(false); yield break; }
        bool disposed = false;
        messPoint.gameObject.SetActive(false);
        if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(true);
        RegisterCleanup(() =>
        {
            if (!disposed && messPoint != null) messPoint.gameObject.SetActive(true);
            if (worker != null && worker.TrashBagObject != null) worker.TrashBagObject.SetActive(false);
        });
        staff.CurrentSubStatus = "Несёт мусор к контейнеру";
        yield return worker.MoveToTarget(dumpster.position, ServiceWorkerController.WorkerState.Cleaning);
        if (dumpster == null || Vector2.Distance(staff.transform.position, dumpster.position) > 2.5f)
        { FinishAction(false); yield break; }
        yield return new WaitForSeconds(1f);
        disposed = true;
        if (messPoint != null) Destroy(messPoint.gameObject);
        if (moneyFound > 0) PlayerWallet.Instance?.AddMoney(moneyFound, "Находка в мусоре", IncomeType.Shadow);

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        Managers.AchievementManager.Instance?.UnlockAchievement("Achv_CleanerStory");
        FinishAction(true);
    }
}
