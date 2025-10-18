using UnityEngine;
using System.Collections;
using System.Linq;

public class EmptyTrashCanExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        var worker = staff as ServiceWorkerController;
        if (worker == null) { FinishAction(false); yield break; }

        var targetCan = TrashCan.AllTrashCans
            .Where(can => can.IsFull)
            .OrderBy(can => Vector3.Distance(staff.transform.position, can.transform.position))
            .FirstOrDefault();
        if (targetCan == null) { FinishAction(false); yield break; }

        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        worker.thoughtBubble?.ShowPriorityMessage("Бак полон!", 2f, Color.red);
        yield return staff.StartCoroutine(worker.MoveToTarget(targetCan.transform.position, ServiceWorkerController.WorkerState.Cleaning));

        if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(false);
        if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(true);

        yield return new WaitForSeconds(1.5f);
        targetCan.Empty();
        
        Transform dumpster = ScenePointsRegistry.Instance?.dumpsterPoint;
        if (dumpster != null)
        {
            yield return staff.StartCoroutine(worker.MoveToTarget(dumpster.position, ServiceWorkerController.WorkerState.Cleaning));
            if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(false);
            if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(true);
            yield return new WaitForSeconds(2f);
        }
        else
        {
            if (worker.TrashBagObject != null) worker.TrashBagObject.SetActive(false);
            if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(true);
        }

        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}