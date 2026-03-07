using UnityEngine;

[CreateAssetMenu(fileName = "Action_InternPatrol", menuName = "Bureau/Actions/InternPatrol")]
public class InternPatrolAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is InternController intern) || intern.IsOnBreak()) return false;

        // ПРОВЕРКА: Разрешаем патруль, только если на сцене реально есть расставленные точки!
        var points = Managers.ScenePointsRegistry.Instance?.internPatrolPoints;
        return points != null && points.Count > 0 && points[0] != null;
    }

	public InternPatrolAction()
    {
        category = ActionCategory.System; // Добавить эту строку
    }

    public override System.Type GetExecutorType()
    {
        return typeof(InternPatrolExecutor);
    }
}