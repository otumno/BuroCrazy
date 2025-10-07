using UnityEngine;
using System.Collections;

public class PrioritizeConsultationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController registrar)) { FinishAction(); yield break; }

        registrar.thoughtBubble?.ShowPriorityMessage("Следующий, кто просто спросить!", 2f, Color.green);

        // Вызываем наш новый метод в менеджере очереди
        bool success = ClientQueueManager.Instance.CallClientWithSpecificGoal(ClientGoal.AskAndLeave, registrar);

        if (success)
        {
            ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        }

        // Это "мгновенное" действие, оно не занимает много времени
        yield return new WaitForSeconds(1f);
        FinishAction();
    }
}