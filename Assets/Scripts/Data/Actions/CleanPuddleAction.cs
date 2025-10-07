using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CleanPuddle", menuName = "Bureau/Actions/CleanPuddle")]
public class CleanPuddleAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ServiceWorkerController worker) || worker.IsOnBreak())
        {
            return false;
        }
        
        return MessManager.Instance.GetSortedMessList(worker.transform.position)
            .Any(m => m != null && m.type == MessPoint.MessType.Puddle);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CleanPuddleExecutor);
    }
}