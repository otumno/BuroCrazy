using UnityEngine;
using System.Collections;

public class PrioritizeDirectorDocExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController registrar)) { FinishAction(); yield break; }

        registrar.thoughtBubble?.ShowPriorityMessage("Кто на подпись к директору?", 2f, Color.green);

        // Вызываем наш новый метод в менеджере очереди
        bool success = ClientQueueManager.Instance.CallClientWithSpecificGoal(ClientGoal.DirectorApproval, registrar);

        if (success)
        {
            ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        }

        // Это "мгновенное" действие, оно не занимает много времени
        yield return new WaitForSeconds(1f);
        FinishAction();
    }
}