using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;

public class CoverDeskExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null || HiringManager.Instance == null) { FinishAction(false); yield break; }

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
            .FirstOrDefault(c => c != null && c.clerkRole == targetRole && c.IsOnBreak() &&
                c.assignedWorkstation != null && !c.assignedWorkstation.HasCoverReservation);
        if (clerkOnBreak == null || clerkOnBreak.assignedWorkstation == null) { FinishAction(false); yield break; }

        var targetPoint = clerkOnBreak.assignedWorkstation;
        if (targetPoint.clerkStandPoint == null || !targetPoint.TryReserveCover(intern))
        { FinishAction(false); yield break; }
        RegisterCleanup(() =>
        {
            if (targetPoint != null) targetPoint.ReleaseCover(intern);
            if (targetPoint != null && ClientSpawner.GetServiceProviderAtDesk(targetPoint) == intern)
                ClientSpawner.UnassignServiceProviderFromDesk(targetPoint, intern);
            if (intern != null) intern.AssignCoveredWorkstation(null);
        });
        intern.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.cover"), 2f, Color.cyan);
        yield return intern.MoveToTarget(targetPoint.clerkStandPoint.position, InternController.InternState.CoveringDesk);
        if (clerkOnBreak == null || !clerkOnBreak.IsOnBreak() ||
            Vector2.Distance(intern.transform.position, targetPoint.clerkStandPoint.position) > 1f)
        { FinishAction(false); yield break; }
        intern.AssignCoveredWorkstation(targetPoint);
        ClientSpawner.AssignServiceProviderToDesk(intern, targetPoint);
        
        yield return new WaitUntil(() => clerkOnBreak == null || !clerkOnBreak.IsOnBreak());

        // Cleanup removes only our registration: the returning clerk may already have reclaimed the desk.
        intern.SetState(InternController.InternState.Patrolling);
        Managers.AchievementManager.Instance?.UnlockAchievement("Achv_InternStory");
        FinishAction(true);
    }
}
