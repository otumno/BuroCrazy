using UnityEngine;

[CreateAssetMenu(fileName = "Action_InternPatrol", menuName = "Bureau/Actions/InternPatrol")]
public class InternPatrolAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условие простое: если ты стажер и не на перерыве, ты всегда можешь патрулировать.
        // Из-за низкого приоритета это действие будет выбрано, только если больше нечего делать.
        if (!(staff is InternController intern) || intern.IsOnBreak())
        {
            return false;
        }
        return true;
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