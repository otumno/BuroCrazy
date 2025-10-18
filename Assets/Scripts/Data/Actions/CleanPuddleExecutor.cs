using UnityEngine;
using System.Collections;
using System.Linq;

public class CleanPuddleExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker))
        {
            FinishAction(false);
            yield break;
        }

        MessPoint messPoint = MessManager.Instance.GetSortedMessList(worker.transform.position)
            .FirstOrDefault(m => m != null && m.type == MessPoint.MessType.Puddle);
        if (messPoint == null)
        {
            FinishAction(false);
            yield break;
        }

        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        yield return staff.StartCoroutine(worker.MoveToTarget(messPoint.transform.position, ServiceWorkerController.WorkerState.Cleaning));
        
        if (messPoint == null)
        {
            FinishAction(false);
            yield break;
        }
        
        worker.SetState(ServiceWorkerController.WorkerState.Cleaning);
        yield return new WaitForSeconds(worker.cleaningTimePuddle);
        
        Destroy(messPoint.gameObject);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);

        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        FinishAction(true);
    }
}