using UnityEngine;
using System.Collections;

public class CatchThiefExecutor : ActionExecutor
{
	public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is GuardMovement guard))
        {
            FinishAction(false);
            yield break;
        }

        ClientPathfinding thief = GuardManager.Instance.GetThiefToCatch();
        if (thief == null)
        {
            FinishAction(false);
            yield break;
        }

        GuardManager.Instance.MarkTaskAsTaken(thief);
        guard.SetState(GuardMovement.GuardState.ChasingThief);
        Debug.Log($"<color=red>{guard.name} начинает погоню за вором {thief.name}!</color>");

        guard.AgentMover.ApplySpeedMultiplier(guard.chaseSpeedMultiplier);
        while (thief != null && Vector2.Distance(guard.transform.position, thief.transform.position) > 1.5f)
        {
            guard.AgentMover.StartDirectChase(thief.transform.position);
            yield return null;
        }

        guard.AgentMover.StopDirectChase();
        guard.AgentMover.ApplySpeedMultiplier(1f);

        if (thief == null)
        {
            FinishAction(false);
            yield break;
        }

        Debug.Log($"{guard.name} поймал вора {thief.name}.");
        thief.ForceLeave(ClientPathfinding.LeaveReason.Theft);
        
        guard.unwrittenReportPoints++;
        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}