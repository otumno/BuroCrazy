using UnityEngine;
using System.Collections;
using System.Linq;

public class CleanDirtExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker))
        {
            FinishAction();
            yield break;
        }

        MessPoint messPoint = MessManager.Instance.GetSortedMessList(worker.transform.position)
            .FirstOrDefault(m => m != null && m.type == MessPoint.MessType.Dirt);

        if (messPoint == null)
        {
            FinishAction();
            yield break;
        }

        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        yield return staff.StartCoroutine(worker.MoveToTarget(messPoint.transform.position, ServiceWorkerController.WorkerState.Cleaning));

        if (messPoint == null)
        {
            FinishAction();
            yield break;
        }
        
        worker.SetState(ServiceWorkerController.WorkerState.Cleaning);

        // --- ВАЖНОЕ ОТЛИЧИЕ: Время уборки зависит от уровня грязи ---
        float cleaningTime = worker.cleaningTimePerDirtLevel * messPoint.dirtLevel;
        Debug.Log($"{worker.name} убирает грязь (уровень {messPoint.dirtLevel}), это займет {cleaningTime:F1} сек.");
        yield return new WaitForSeconds(cleaningTime);
        
        Destroy(messPoint.gameObject);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);

        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        FinishAction();
    }
}