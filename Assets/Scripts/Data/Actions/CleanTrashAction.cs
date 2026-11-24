using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CleanTrash", menuName = "Bureau/Actions/CleanTrash")]
public class CleanTrashAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условия: это уборщик, он не на перерыве, и где-то есть мусор.
        if (!(staff is ServiceWorkerController worker) || worker.IsOnBreak())
        {
            return false;
        }

        // Ищем в MessManager ближайший мусор.
        return MessManager.Instance != null && MessManager.Instance.GetSortedMessList(worker.transform.position, availableOnly: true)
            .Any(m => m != null && m.type == MessPoint.MessType.Trash);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CleanTrashExecutor);
    }
}
