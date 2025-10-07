using UnityEngine;
using System.Collections;

public class GoToJanitorHomeExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker)) { FinishAction(); yield break; }
        
        // Расчет времени ожидания по вашей формуле
        float actualMaxWait = worker.maxIdleWait * (1f - worker.skills.pedantry);
        float waitTime = Random.Range(worker.minIdleWait, actualMaxWait);
        Debug.Log($"{worker.name} будет бездействовать {waitTime:F1} секунд (Педантичность: {worker.skills.pedantry:P0}).");

        Transform homePoint = ScenePointsRegistry.Instance?.janitorHomePoint;
        if (homePoint != null)
        {
            worker.SetState(ServiceWorkerController.WorkerState.Idle);
            yield return staff.StartCoroutine(worker.MoveToTarget(homePoint.position, ServiceWorkerController.WorkerState.Idle));
        }

        yield return new WaitForSeconds(waitTime);
        
        // Завершаем действие, чтобы AI мог начать новый цикл
        FinishAction();
    }
}