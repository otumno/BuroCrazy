using UnityEngine;
using System.Linq;
using Managers;

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
        if (!(staff is ClerkController registrar) || registrar.IsOnBreak() || registrar.clerkRole != ClerkController.ClerkRole.Registrar)
        {
            return false;
        }

        if (registrar.assignedWorkstation == null) return false;
        var zone = registrar.assignedWorkstation.GetComponentInParent<LimitedCapacityZone>();
        bool hasClientAtDesk = zone != null && zone.GetOccupyingClients().Any();
        bool hasTargetInQueue = registrar.IsAvailableToServe && ClientQueueManager.Instance != null && ClientQueueManager.Instance.HasCallableClient(ClientGoal.AskAndLeave);
        
        return !hasClientAtDesk && hasTargetInQueue;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(PrioritizeConsultationExecutor);
    }
}