using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_PrioritizeDirectorDoc", menuName = "Bureau/Actions/PrioritizeDirectorDoc")]
public class PrioritizeDirectorDocAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController registrar) || registrar.IsOnBreak() || registrar.role != ClerkController.ClerkRole.Registrar)
        {
            return false;
        }

        // Условие: в общей очереди есть хотя бы один клиент с целью "Просто спросить"
        return ClientQueueManager.Instance.queue.Any(c => c.Key != null && c.Key.mainGoal == ClientGoal.DirectorApproval);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(PrioritizeDirectorDocAction);
    }
}