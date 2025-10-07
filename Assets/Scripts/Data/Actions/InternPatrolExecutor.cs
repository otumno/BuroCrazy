using UnityEngine;
using System.Collections;
using System.Linq;

public class InternPatrolExecutor : ActionExecutor
{
    // ВАЖНО: Разрешаем прерывание этого действия
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var intern = staff as InternController;
        if (intern == null) { FinishAction(); yield break; }

        intern.SetState(InternController.InternState.Patrolling);
        intern.thoughtBubble?.ShowPriorityMessage("Патрулирую...", 5f, Color.gray);

        // Получаем маршрут для патрулирования из общего реестра точек
        var patrolRoute = ScenePointsRegistry.Instance?.internPatrolPoints;
        if (patrolRoute == null || !patrolRoute.Any())
        {
            Debug.LogWarning($"Для стажера {intern.name} не настроены точки патрулирования (internPatrolPoints)!");
            yield return new WaitForSeconds(10f); // Если маршрута нет, просто ждем на месте
            FinishAction();
            yield break;
        }

        // Посещаем несколько случайных точек
        int pointsToVisit = actionData.patrolPointsToVisit;
        for (int i = 0; i < pointsToVisit; i++)
        {
            var randomPoint = patrolRoute[Random.Range(0, patrolRoute.Count)];
            yield return staff.StartCoroutine(intern.MoveToTarget(randomPoint.position, InternController.InternState.Patrolling));

            // Небольшая пауза на точке
            yield return new WaitForSeconds(Random.Range(2f, 5f));
        }

        FinishAction();
    }
}