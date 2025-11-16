using UnityEngine;
using System.Collections;
using System.Linq;

public class CoverRegistrarExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Подмена - важная задача

    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null) { FinishAction(); yield break; }

        // Находим регистратора на перерыве и его стол
        var registrarOnBreak = HiringManager.Instance.AllStaff.OfType<ClerkController>()
            .FirstOrDefault(r => r.role == ClerkController.ClerkRole.Registrar && r.IsOnBreak());

        if (registrarOnBreak == null || registrarOnBreak.assignedServicePoint == null) { FinishAction(); yield break; }

        var targetPoint = registrarOnBreak.assignedServicePoint;

        // 1. Идем на пост
        intern.SetState(InternController.InternState.CoveringDesk);
        intern.thoughtBubble?.ShowPriorityMessage("Подменю!", 2f, Color.cyan);
        yield return staff.StartCoroutine(intern.MoveToTarget(targetPoint.clerkStandPoint.position, InternController.InternState.CoveringDesk));

        // 2. "Заступаем на смену"
        intern.AssignCoveredWorkstation(targetPoint); // Сообщаем стажеру, какой стол он подменяет
        ClientSpawner.AssignServiceProviderToDesk(intern, targetPoint.deskId);
        Debug.Log($"{intern.name} подменяет {registrarOnBreak.name} на посту регистрации.");

        // 3. Работаем, пока основной сотрудник не вернется
        yield return new WaitUntil(() => registrarOnBreak == null || !registrarOnBreak.IsOnBreak());

        // 4. "Сдаем смену"
        Debug.Log($"{registrarOnBreak.name} вернулся. {intern.name} уходит с поста.");
        ClientSpawner.UnassignServiceProviderFromDesk(targetPoint.deskId);
        intern.AssignCoveredWorkstation(null);
        intern.SetState(InternController.InternState.Patrolling);

        FinishAction();
    }
}