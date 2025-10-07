using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_HandleSituation", menuName = "Bureau/Actions/HandleSituation")]
public class HandleSituationAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условия: сотрудник не на перерыве и где-то на сцене есть хотя бы один клиент в состоянии Confused.
        if (staff.IsOnBreak())
        {
            return false;
        }

        // Ищем ближайшего "потеряшку". Если нашли, значит, условие выполнено.
        return ClientPathfinding.FindClosestConfusedClient(staff.transform.position) != null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(HandleSituationExecutor);
    }
}