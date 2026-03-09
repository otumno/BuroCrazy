using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay; 

public class GoToCoolerExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var cooler = Object.FindObjectsByType<ResourceContainer>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.resourceType == ResourceType.Water);

        if (cooler == null) { FinishAction(false); yield break; }

        if (staff is ClerkController clerk) {
            clerk.SetState(ClerkController.ClerkState.GoingToBreak);
            yield return staff.StartCoroutine(staff.MoveToTarget(cooler.transform.position, ClerkController.ClerkState.OnBreak.ToString()));
        } else {
            yield return staff.StartCoroutine(staff.MoveToTarget(cooler.transform.position, "Idle")); 
        }

        if (!cooler.IsEmpty) {
            cooler.Consume();
            staff.thoughtBubble?.ShowPriorityMessage("*Глыг-глыг*", 2f, Color.blue);
            
            // ИСПРАВЛЕНИЕ: Восстанавливаем жажду до 100, а не энергию до 1
            staff.morale = 100f; 
            staff.bladder = Mathf.Clamp(staff.bladder + 20f, 0f, 100f); // Вода дает в туалет
        } else {
            staff.thoughtBubble?.ShowPriorityMessage("Пусто...", 2f, Color.red);
            staff.stress = Mathf.Clamp(staff.stress + 15f, 0f, 100f); // Расстройство
        }

        yield return new WaitForSeconds(2f); // Небольшая пауза

        if (staff is ClerkController c) c.SetState(ClerkController.ClerkState.ReturningToWork);
        FinishAction(true);
    }
}
