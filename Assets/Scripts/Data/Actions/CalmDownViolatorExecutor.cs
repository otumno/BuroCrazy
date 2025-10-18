using UnityEngine;
using System.Collections;

public class CalmDownViolatorExecutor : ActionExecutor
{
	public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is GuardMovement guard))
        {
            FinishAction(false);
            yield break;
        }

        ClientPathfinding violator = GuardManager.Instance.GetViolatorToHandle();
        if (violator == null)
        {
            FinishAction(false);
            yield break;
        }

        GuardManager.Instance.MarkTaskAsTaken(violator);
        guard.SetState(GuardMovement.GuardState.Chasing);
        Debug.Log($"<color=red>{guard.name} начинает преследование нарушителя {violator.name}!</color>");

        guard.AgentMover.ApplySpeedMultiplier(guard.chaseSpeedMultiplier);
        while (violator != null && Vector2.Distance(guard.transform.position, violator.transform.position) > 2f)
        {
            guard.AgentMover.StartDirectChase(violator.transform.position);
            yield return null;
        }

        guard.AgentMover.StopDirectChase();
        guard.AgentMover.ApplySpeedMultiplier(1f);

        if (violator == null)
        {
            FinishAction(false);
            yield break;
        }

        guard.SetState(GuardMovement.GuardState.Talking);
        Debug.Log($"{guard.name} разговаривает с {violator.name}.");
        yield return new WaitForSeconds(guard.talkTime);

        if (Random.value > 0.5f)
        {
            violator.CalmDownAndLeave();
        }
        else
        {
            violator.CalmDownAndReturnToQueue();
        }
        
        guard.unwrittenReportPoints++;
        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}