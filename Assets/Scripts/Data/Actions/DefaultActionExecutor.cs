using UnityEngine;
using System.Collections;

public class DefaultActionExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        staff.SetCurrentFrustration(0f);
        Debug.Log($"<color=cyan>[ДЕЙСТВИЕ ПО УМОЛЧАНИЮ]</color> Выгорание для {staff.characterName} сброшено до 0.");

        if (staff is GuardMovement guard)
        {
            guard.SetState(GuardMovement.GuardState.OnPost);
            Transform post = ScenePointsRegistry.Instance?.guardPostPoint;
            if (post != null)
            {
                yield return staff.StartCoroutine(guard.MoveToTarget(post.position, GuardMovement.GuardState.OnPost));
                yield return new WaitForSeconds(15f);
            }
        }
        else if (staff is ServiceWorkerController worker)
        {
            worker.SetState(ServiceWorkerController.WorkerState.Idle);
            RectZone homeZone = ScenePointsRegistry.Instance?.staffHomeZone;
            if (homeZone != null)
            {
                yield return staff.StartCoroutine(worker.MoveToTarget(homeZone.GetRandomPointInside(), ServiceWorkerController.WorkerState.Idle));
                yield return new WaitForSeconds(15f);
            }
        }
        else if (staff is ClerkController clerk)
        {
            clerk.SetState(ClerkController.ClerkState.OnBreak);
            Transform breakPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();
            if (breakPoint != null)
            {
                // ----- THE FIX IS HERE -----
                yield return staff.StartCoroutine(clerk.MoveToTarget(breakPoint.position, ClerkController.ClerkState.OnBreak.ToString()));
                yield return new WaitForSeconds(15f);
                ScenePointsRegistry.Instance.FreeKitchenPoint(breakPoint);
            }
        }
        else
        {
            yield return new WaitForSeconds(10f);
        }

        FinishAction();
    }
}