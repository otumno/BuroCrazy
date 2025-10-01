// Файл: Assets/Scripts/Characters/Controllers/Actions/PatrolExecutor.cs
using UnityEngine;
using System.Collections;

public class PatrolExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        // Пытаемся получить компонент GuardMovement с нашего сотрудника
        GuardMovement guard = staff as GuardMovement;
        if (guard == null)
        {
            Debug.LogError("PatrolExecutor может быть использован только на сотруднике с компонентом GuardMovement!");
            FinishAction();
            yield break;
        }

        guard.SetState(GuardMovement.GuardState.Patrolling);

        // Получаем данные для патруля из нашего ассета StaffAction
        int pointsToVisit = actionData.patrolPointsToVisit;
        if (pointsToVisit <= 0) pointsToVisit = 1; // Патрулируем хотя бы 1 точку для надежности

        for (int i = 0; i < pointsToVisit; i++)
        {
            var patrolTarget = guard.SelectNewPatrolPoint();
            if (patrolTarget != null)
            {
                // Мы не можем вызывать StartCoroutine из этого класса напрямую,
                // поэтому просим "носителя" (staff) запустить корутину для нас.
                yield return staff.StartCoroutine(guard.MoveToTarget(patrolTarget.position, GuardMovement.GuardState.WaitingAtWaypoint));
                ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
                yield return new WaitForSeconds(Random.Range(guard.minWaitTime, guard.maxWaitTime));
            }
            else
            {
                // Если точек для патруля не найдено, ждем и завершаем действие
                guard.thoughtBubble?.ShowPriorityMessage("Некуда патрулировать!", 2f, Color.red);
                yield return new WaitForSeconds(3f);
                break;
            }
        }

        guard.unwrittenReportPoints++; 
    Debug.Log($"{guard.name} завершил патруль. Очков для протокола: {guard.unwrittenReportPoints}");

    guard.thoughtBubble?.ShowPriorityMessage("Патрулирование окончено.", 2f, Color.gray);
    FinishAction();
    }
}