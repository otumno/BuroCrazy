using UnityEngine;
using System.Collections;

public class GoToPostExecutor : ActionExecutor
{
    public override bool IsInterruptible => true; // Это действие можно прервать

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is GuardMovement guard)) { FinishAction(); yield break; }

        Transform post = ScenePointsRegistry.Instance?.guardPostPoint;
        if (post == null) {
            Debug.LogWarning($"{guard.name} не может найти свой пост (guardPostPoint). Просто ждет на месте.");
            while (true) { yield return new WaitForSeconds(5f); }
        }

        guard.SetState(GuardMovement.GuardState.OnPost);
        yield return staff.StartCoroutine(guard.MoveToTarget(post.position, GuardMovement.GuardState.OnPost));

        while (true)
        {
            // Бесконечно ждем, пока нас не прервет более важное дело
            yield return new WaitForSeconds(5f);
        }
    }
}