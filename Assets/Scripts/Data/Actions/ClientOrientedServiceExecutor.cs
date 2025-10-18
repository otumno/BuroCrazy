using UnityEngine;
using System.Collections;
using System.Linq;

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

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (zone == null) { FinishAction(false); yield break; }

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null)
        {
            FinishAction(false);
            yield break;
        }

        clerk.thoughtBubble?.ShowPriorityMessage("Как ваш день? Минуточку...", 3f, Color.green);
        yield return new WaitForSeconds(3f);

        float patienceBonus = 30f;
        client.totalPatienceTime += patienceBonus;
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}