// Assets/Scripts/Data/Actions/PrepareSalariesExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class PrepareSalariesExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        // Ссылка на компонент EnvelopeStack
        var envelopeStack = ScenePointsRegistry.Instance?.salaryStackPoint;

        if (envelopeStack == null || HiringManager.Instance == null)
        {
            FinishAction(false);
            yield break;
        }

        // Динамическая ёмкость: сотрудники + 1 (по ТЗ).
        // Сначала зафиксируем начальное количество сотрудников, чтобы не зациклиться при найме во время работы.
        int targetCapacity = Mathf.Min(envelopeStack.maxCapacity, HiringManager.Instance.AllStaff.Count + 1);

        var accountant = staff as ClerkController;
        var workPoint = accountant != null && accountant.assignedWorkstation != null
            ? accountant.assignedWorkstation.clerkStandPoint : envelopeStack.transform;
        if (workPoint == null) { FinishAction(false); yield break; }
        yield return staff.MoveToTarget(workPoint.position, "Working");

        while (envelopeStack != null && workPoint != null && Vector2.Distance(staff.transform.position, workPoint.position) <= 1f)
        {
            // Проверка переполнения (динамический лимит)
            if (envelopeStack.CurrentEnvelopeCount >= targetCapacity)
            {
                staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.payroll.enough"), 3f, Color.yellow);
                break;
            }

            // Тратим время
            yield return new WaitForSeconds(4f);

            if (envelopeStack == null || workPoint == null || Vector2.Distance(staff.transform.position, workPoint.position) > 1f)
            { FinishAction(false); yield break; }
            if (envelopeStack.CurrentEnvelopeCount >= Mathf.Min(targetCapacity, envelopeStack.maxCapacity)) break;
            if (!envelopeStack.TryPrepareEnvelope(PlayerWallet.Instance, UI.GameText.Get("ledger.payroll_fund")))
            {
                staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.payroll.no_budget"), 2f, Color.red);
                FinishAction(false);
                yield break;
            }
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.payroll.ready"), 1f, Color.green);
        }

        FinishAction(envelopeStack != null && workPoint != null && Vector2.Distance(staff.transform.position, workPoint.position) <= 1f);
    }
}
