using UnityEngine;
using System.Collections;

public class PrioritizeDirectorDocExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController registrar)) { FinishAction(false); yield break; }

        registrar.thoughtBubble?.ShowPriorityMessage("Кто на подпись к директору?", 2f, Color.green);
        bool success = ClientQueueManager.Instance.CallClientWithSpecificGoal(ClientGoal.DirectorApproval, registrar);
        if (success)
        {
            ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        }
        
        yield return new WaitForSeconds(1f);
        FinishAction(success);
    }
}