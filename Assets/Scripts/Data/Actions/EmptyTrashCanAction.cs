using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_EmptyTrashCan", menuName = "Bureau/Actions/EmptyTrashCan")]
public class EmptyTrashCanAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ServiceWorkerController worker) || worker.IsOnBreak()) return false;

        // Условие: на сцене есть хотя бы один ПОЛНЫЙ мусорный бак
        return TrashCan.AllTrashCans.Any(can => can.IsFull);
    }

    public override System.Type GetExecutorType() { return typeof(EmptyTrashCanExecutor); }
}