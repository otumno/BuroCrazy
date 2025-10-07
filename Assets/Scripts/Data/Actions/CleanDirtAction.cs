using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CleanDirt", menuName = "Bureau/Actions/CleanDirt")]
public class CleanDirtAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ServiceWorkerController worker) || worker.IsOnBreak())
        {
            return false;
        }

        return MessManager.Instance.GetSortedMessList(worker.transform.position)
            .Any(m => m != null && m.type == MessPoint.MessType.Dirt);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CleanDirtExecutor);
    }
}