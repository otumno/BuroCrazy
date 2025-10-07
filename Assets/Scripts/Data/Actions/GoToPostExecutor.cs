using UnityEngine;
using System.Collections;

public class GoToPostExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is GuardMovement guard)) { FinishAction(); yield break; }

        // Расчет времени ожидания по вашей формуле
        float actualMaxWait = guard.maxIdleWait * (1f - guard.skills.pedantry);
        float waitTime = Random.Range(guard.minIdleWait, actualMaxWait);
        Debug.Log($"{guard.name} будет бездействовать {waitTime:F1} секунд (Педантичность: {guard.skills.pedantry:P0}).");

        Transform post = ScenePointsRegistry.Instance?.guardPostPoint;
        if (post != null)
        {
            guard.SetState(GuardMovement.GuardState.OnPost);
            yield return staff.StartCoroutine(guard.MoveToTarget(post.position, GuardMovement.GuardState.OnPost));
        }
        
        yield return new WaitForSeconds(waitTime);

        // Завершаем действие, чтобы AI мог начать новый цикл
        FinishAction();
    }
}