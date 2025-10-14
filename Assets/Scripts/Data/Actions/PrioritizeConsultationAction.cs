using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_PrioritizeConsultation", menuName = "Bureau/Actions/PrioritizeConsultation")]
public class PrioritizeConsultationAction : StaffAction
{
    public PrioritizeConsultationAction()
    {
        category = ActionCategory.Tactic;
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // THE FIX IS HERE: We now compare ClerkController.ClerkRole with ClerkController.ClerkRole
        if (!(staff is ClerkController registrar) || registrar.IsOnBreak() || registrar.role != ClerkController.ClerkRole.Registrar)
        {
            return false;
        }

        bool hasClientAtDesk = ClientSpawner.GetZoneByDeskId(registrar.assignedWorkstation.deskId).GetOccupyingClients().Any();
        bool hasTargetInQueue = ClientQueueManager.Instance.queue.Any(c => c.Key != null && c.Key.mainGoal == ClientGoal.AskAndLeave);
        
        return !hasClientAtDesk && hasTargetInQueue;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(PrioritizeConsultationExecutor);
    }
}