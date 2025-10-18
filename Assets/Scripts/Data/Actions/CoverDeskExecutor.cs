using UnityEngine;
using System.Collections;
using System.Linq;

public class CoverDeskExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null) { FinishAction(false); yield break; }

        ClerkController.ClerkRole targetRole;

        if (actionData.actionType == ActionType.CoverRegistrar)
            targetRole = ClerkController.ClerkRole.Registrar;
        else if (actionData.actionType == ActionType.CoverClerk)
            targetRole = ClerkController.ClerkRole.Regular;
        else if (actionData.actionType == ActionType.CoverCashier)
            targetRole = ClerkController.ClerkRole.Cashier;
        else
        {
            FinishAction(false);
            yield break;
        }

        var clerkOnBreak = HiringManager.Instance.AllStaff.OfType<ClerkController>()
            .FirstOrDefault(c => c.role == targetRole && c.IsOnBreak());
        if (clerkOnBreak == null || clerkOnBreak.assignedWorkstation == null) { FinishAction(false); yield break; }

        var targetPoint = clerkOnBreak.assignedWorkstation;
        intern.SetState(InternController.InternState.CoveringDesk);
        intern.thoughtBubble?.ShowPriorityMessage("Подменю!", 2f, Color.cyan);
        yield return staff.StartCoroutine(intern.MoveToTarget(targetPoint.clerkStandPoint.position, InternController.InternState.CoveringDesk));
        
        intern.AssignCoveredWorkstation(targetPoint);
        ClientSpawner.AssignServiceProviderToDesk(intern, targetPoint.deskId);
        
        yield return new WaitUntil(() => clerkOnBreak == null || !clerkOnBreak.IsOnBreak());
        
        ClientSpawner.UnassignServiceProviderFromDesk(targetPoint.deskId);
        intern.AssignCoveredWorkstation(null);
        intern.SetState(InternController.InternState.Patrolling);
        FinishAction(true);
    }
}