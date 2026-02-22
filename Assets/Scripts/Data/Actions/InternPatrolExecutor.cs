// Assets/Scripts/Data/Actions/InternPatrolExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;

public class InternPatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var points = ScenePointsRegistry.Instance?.internPatrolPoints;

        if (points == null || points.Count == 0)
        {
            Debug.LogWarning($"[InternPatrolExecutor] {staff?.characterName ?? "Unknown"}: Нет точек патруля (null или пусто)");
            FinishAction(false);
            yield break;
        }

        var validPoints = points.Where(p => p != null && p.transform != null).ToList();
        
        if (validPoints.Count == 0)
        {
            Debug.LogWarning($"[InternPatrolExecutor] {staff?.characterName ?? "Unknown"}: Нет валидных точек патруля (все null)");
            FinishAction(false);
            yield break;
        }

        Debug.Log($"[InternPatrolExecutor] {staff?.characterName}: Начинаю патруль. Точек: {validPoints.Count}");

        while (staff.IsOnDuty())
        {
            var target = validPoints[Random.Range(0, validPoints.Count)];
            Debug.Log($"[InternPatrolExecutor] {staff?.characterName}: Иду к точке {target.name}");
            
            yield return staff.StartCoroutine(staff.MoveToTarget(target.transform.position, "Patrolling"));
            yield return new WaitForSeconds(Random.Range(3f, 7f));

            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log($"[InternPatrolExecutor] {staff?.characterName}: Патруль завершён (смена окончена)");
        FinishAction(true);
    }
}