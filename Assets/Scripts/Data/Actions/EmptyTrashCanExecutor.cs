using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;

public class EmptyTrashCanExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker)) { FinishAction(false); yield break; }
        var dumpster = ScenePointsRegistry.Instance?.dumpsterPoint;
        var targetCan = TrashCan.AllTrashCans.Where(c => c != null && c.IsFull)
            .OrderBy(c => Vector3.Distance(staff.transform.position, c.transform.position)).FirstOrDefault();
        if (targetCan == null || dumpster == null) { FinishAction(false); yield break; }
        int carried = 0;
        RegisterCleanup(() =>
        {
            if (targetCan != null && carried > 0) targetCan.ReturnCollectedTrash(carried);
            if (worker == null) return;
            if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(false);
            if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(true);
        });
        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        yield return worker.MoveToTarget(targetCan.transform.position, ServiceWorkerController.WorkerState.Cleaning);
        if (targetCan == null || Vector2.Distance(staff.transform.position, targetCan.transform.position) > 2.5f)
        { FinishAction(false); yield break; }
        yield return new WaitForSeconds(1.5f);
        carried = targetCan.CurrentFillCount;
        targetCan.Empty();
        if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(false);
        if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(true);
        staff.CurrentSubStatus = "Несёт мусор к контейнеру";
        yield return worker.MoveToTarget(dumpster.position, ServiceWorkerController.WorkerState.Cleaning);
        if (dumpster == null || Vector2.Distance(staff.transform.position, dumpster.position) > 2.5f)
        { FinishAction(false); yield break; }
        yield return new WaitForSeconds(2f);
        carried = 0;
        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}
