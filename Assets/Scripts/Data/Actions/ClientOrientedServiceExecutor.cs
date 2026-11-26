using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;

public class ClientOrientedServiceExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedWorkstation == null)
        {
            FinishAction(false);
            yield break;
        }

        var zone = clerk.assignedWorkstation.GetComponentInParent<LimitedCapacityZone>();
        if (zone == null) { FinishAction(false); yield break; }

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null)
        {
            FinishAction(false);
            yield break;
        }

        clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.small_talk"), 3f, Color.green);
        yield return new WaitForSeconds(3f);

        float patienceBonus = 30f;
        client.totalPatienceTime += patienceBonus;
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}