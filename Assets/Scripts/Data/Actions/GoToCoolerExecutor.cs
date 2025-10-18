using UnityEngine;
using System.Collections;

public class GoToCoolerExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        Transform coolerPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();
        if (coolerPoint == null)
        {
            FinishAction(false);
            yield break;
        }

        staff.thoughtBubble?.ShowPriorityMessage("Пойду поболтаю...", 2f, Color.gray);
        
        if (staff is ClerkController clerk)
        {
            clerk.SetState(ClerkController.ClerkState.GoingToBreak);
            yield return staff.StartCoroutine(staff.MoveToTarget(coolerPoint.position, ClerkController.ClerkState.OnBreak.ToString()));
        }
        
        yield return new WaitForSeconds(Random.Range(10f, 20f));

        staff.morale = 1f;
        staff.bladder = Mathf.Clamp01(staff.bladder + 0.3f);
        
        ScenePointsRegistry.Instance.FreeKitchenPoint(coolerPoint);
        FinishAction(true);
    }
}