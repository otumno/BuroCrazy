using UnityEngine;
using System.Collections;
using System.Linq;

public class HandleSituationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (staff is ClerkController clerk && clerk.assignedWorkstation != null)
        {
            float distanceToPost = Vector2.Distance(clerk.transform.position, clerk.assignedWorkstation.clerkStandPoint.position);
            if (distanceToPost < 0.5f)
            {
                ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
                if (confusedClient != null)
                {
                    float distanceToClient = Vector2.Distance(clerk.transform.position, confusedClient.transform.position);
                    HandleSituationAction handleAction = actionData as HandleSituationAction;
                    if (handleAction != null && distanceToClient < handleAction.remoteHelpRadius)
                    {
                        staff.thoughtBubble?.ShowPriorityMessage("Молодой человек, вам куда?", 3f, Color.cyan);
                        yield return new WaitForSeconds(2f);

                        Waypoint correctGoal = DetermineCorrectGoalForClient(confusedClient);
                        confusedClient.stateMachine.GetHelpFromIntern(correctGoal);
                        
                        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
                        FinishAction(true);
                        yield break;
                    }
                }
            }
        }

        ClientPathfinding clientToHelp = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
        if (clientToHelp == null) { FinishAction(false); yield break; }

        staff.thoughtBubble?.ShowPriorityMessage("Вижу, нужна помощь...", 2f, Color.cyan);

        staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, clientToHelp.transform.position, staff.gameObject));
        yield return new WaitUntil(() => !staff.AgentMover.IsMoving());

        if (clientToHelp == null) { FinishAction(false); yield break; }

        staff.thoughtBubble?.ShowPriorityMessage("Вам куда?", 3f, Color.white);
        yield return new WaitForSeconds(2f);

        Waypoint goal = DetermineCorrectGoalForClient(clientToHelp);
        clientToHelp.stateMachine.GetHelpFromIntern(goal);

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }

    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        if (client.billToPay > 0) return ClientSpawner.GetCashierZone().waitingWaypoint;
        switch (client.mainGoal)
        {
            case ClientGoal.PayTax: return ClientSpawner.GetCashierZone().waitingWaypoint;
            case ClientGoal.GetCertificate1: return ClientSpawner.GetDesk1Zone().waitingWaypoint;
            case ClientGoal.GetCertificate2: return ClientSpawner.GetDesk2Zone().waitingWaypoint;
            case ClientGoal.VisitToilet: return ClientSpawner.GetToiletZone().waitingWaypoint;
            default: return ClientQueueManager.Instance.ChooseNewGoal(client);
        }
    }
}