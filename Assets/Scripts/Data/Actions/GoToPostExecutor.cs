using UnityEngine;
using System.Collections;

public class GoToPostExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is GuardMovement guard)) { FinishAction(false); yield break; }

        float actualMaxWait = guard.maxIdleWait * (1f - guard.skills.pedantry);
        float waitTime = Random.Range(guard.minIdleWait, actualMaxWait);
        
        Transform post = ScenePointsRegistry.Instance?.guardPostPoint;
        if (post != null)
        {
            guard.SetState(GuardMovement.GuardState.OnPost);
            yield return staff.StartCoroutine(guard.MoveToTarget(post.position, GuardMovement.GuardState.OnPost));
        }
        
        yield return new WaitForSeconds(waitTime);
        FinishAction(true);
    }
}