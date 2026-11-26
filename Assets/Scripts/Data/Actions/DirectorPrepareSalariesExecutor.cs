// Assets/Scripts/Data/Actions/DirectorPrepareSalariesExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class DirectorPrepareSalariesExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        // Получаем точку стопки (теперь это EnvelopeStack)
        var envelopeStack = ScenePointsRegistry.Instance?.salaryStackPoint; 
        
        if (envelopeStack == null)
        {
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.payroll.no_safe"), 3f, Color.red);
            FinishAction(false);
            yield break;
        }

        // Идем к сейфу
        yield return staff.MoveToTarget(envelopeStack.transform.position, "Working");

        int envelopesToMake = 5;

        for (int i = 0; i < envelopesToMake; i++)
        {
            yield return new WaitForSeconds(1.5f);
            if (envelopeStack == null || Vector2.Distance(staff.transform.position, envelopeStack.transform.position) > 1f)
            { FinishAction(false); yield break; }
            if (PlayerWallet.Instance == null || !PlayerWallet.Instance.CanAfford(Gameplay.EnvelopeStack.PreparationCost))
            {
                staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.payroll.no_money"), 2f, Color.red);
                break;
            }

            // ИСПРАВЛЕНИЕ: Вызываем метод у компонента, а не у трансформа
            if (envelopeStack.CurrentEnvelopeCount >= envelopeStack.maxCapacity)
            {
                staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.payroll.safe_full"), 2f, Color.red);
                break;
            }

            if (!envelopeStack.TryPrepareEnvelope(PlayerWallet.Instance, UI.GameText.Get("ledger.payroll_preparation")))
            { FinishAction(false); yield break; }
            
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.payroll.added"), 1f, Color.green);
        }

        FinishAction(true);
    }
}
