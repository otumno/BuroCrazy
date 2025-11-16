using UnityEngine;
using System.Collections;
using System.Linq;

public class CoverDeskExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null) { FinishAction(); yield break; }

        // Находим сотрудника на перерыве, который соответствует роли из нашего Action-ассета
        var clerkOnBreak = HiringManager.Instance.AllStaff.OfType<ClerkController>()
            .FirstOrDefault(c => actionData.applicableRoles.Contains(c.currentRole) && c.IsOnBreak());

        if (clerkOnBreak == null || clerkOnBreak.assignedServicePoint == null) { FinishAction(); yield break; }
        var targetPoint = clerkOnBreak.assignedServicePoint;

        // 1. Идем на пост
        intern.SetState(InternController.InternState.CoveringDesk);
        intern.thoughtBubble?.ShowPriorityMessage("Подменю!", 2f, Color.cyan);
        yield return staff.StartCoroutine(intern.MoveToTarget(targetPoint.clerkStandPoint.position, InternController.InternState.CoveringDesk));

        // 2. "Заступаем на смену"
        intern.AssignCoveredWorkstation(targetPoint);
        ClientSpawner.AssignServiceProviderToDesk(intern, targetPoint.deskId);
        Debug.Log($"{intern.name} подменяет {clerkOnBreak.name} на посту.");

        // 3. Работаем, пока основной сотрудник не вернется
        yield return new WaitUntil(() => clerkOnBreak == null || !clerkOnBreak.IsOnBreak());

        // 4. "Сдаем смену"
        Debug.Log($"{clerkOnBreak.name} вернулся. {intern.name} уходит с поста.");
        ClientSpawner.UnassignServiceProviderFromDesk(targetPoint.deskId);
        intern.AssignCoveredWorkstation(null);
        intern.SetState(InternController.InternState.Patrolling);

        FinishAction();
    }
}