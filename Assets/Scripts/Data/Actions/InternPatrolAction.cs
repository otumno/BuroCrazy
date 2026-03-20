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
	      category = ActionCategory.System;
	      priority = 50; // Основное занятие стажеров - патруль
	  }
	  
	  public override float CalculateUtility(StaffController staff)
	  {
	      // Базовый вес 50, плюс небольшой бонус за патрулирование
	      return 50f + Random.Range(0f, 5f);
	  }

    public override System.Type GetExecutorType()
    {
        return typeof(InternPatrolExecutor);
    }
}