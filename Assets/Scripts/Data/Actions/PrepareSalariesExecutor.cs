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

        if (envelopeStack == null)
        {
            FinishAction(false);
            yield break;
        }

        // Динамическая ёмкость: сотрудники + 1 (по ТЗ).
        // Сначала зафиксируем начальное количество сотрудников, чтобы не зациклиться при найме во время работы.
        int targetCapacity = Mathf.Max(1, HiringManager.Instance.AllStaff.Count + 1);

        var accountant = staff as ClerkController;
        if (accountant != null && accountant.assignedWorkstation != null)
        {
            yield return staff.StartCoroutine(accountant.MoveToTarget(accountant.assignedWorkstation.clerkStandPoint.position, "Working"));
        }
        else
        {
            yield return staff.StartCoroutine(staff.MoveToTarget(envelopeStack.transform.position, "Working"));
        }

        while (true)
        {
            // Проверка переполнения (динамический лимит)
            if (envelopeStack.CurrentEnvelopeCount >= targetCapacity)
            {
                staff.thoughtBubble?.ShowPriorityMessage("Достаточно конвертов", 3f, Color.yellow);
                break;
            }

            // Тратим время
            yield return new WaitForSeconds(4f);

            int cost = 100;
            if (PlayerWallet.Instance.GetCurrentMoney() >= cost)
            {
                PlayerWallet.Instance.AddMoney(-cost, "Зарплатный фонд");
                envelopeStack.AddEnvelope();
                staff.thoughtBubble?.ShowPriorityMessage("Готов конверт", 1f, Color.green);
            }
            else
            {
                staff.thoughtBubble?.ShowPriorityMessage("Нет бюджета!", 2f, Color.red);
                yield return new WaitForSeconds(2f); // Ждем и пробуем снова
            }
        }

        FinishAction(true);
    }
}