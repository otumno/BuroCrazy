using UnityEngine;
using System.Collections;

public class SortPapersExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk))
        {
            FinishAction(false);
            yield break;
        }

        clerk.thoughtBubble?.ShowPriorityMessage("Надо бы прибраться...", 3f, Color.gray);
        
        yield return new WaitForSeconds(Random.Range(5f, 10f));
        
        float frustrationRelief = 0.05f;
        float newFrustration = staff.GetCurrentFrustration() - frustrationRelief;
        staff.SetCurrentFrustration(newFrustration);

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}