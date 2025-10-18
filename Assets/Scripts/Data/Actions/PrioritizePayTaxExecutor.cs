using UnityEngine;
using System.Collections;

public class PrioritizePayTaxExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController registrar)) { FinishAction(false); yield break; }

        registrar.thoughtBubble?.ShowPriorityMessage("Подходите с оплатой счетов!", 2f, Color.green);
        bool success = ClientQueueManager.Instance.CallClientWithSpecificGoal(ClientGoal.PayTax, registrar);
        if (success)
        {
            ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        }
        
        yield return new WaitForSeconds(1f);
        FinishAction(success);
    }
}