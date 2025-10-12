using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CoverClerk", menuName = "Bureau/Actions/CoverClerk")]
public class CoverClerkAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is InternController intern) || intern.IsOnBreak()) return false;
        return HiringManager.Instance.AllStaff.OfType<ClerkController>()
            .Any(c => c.role == ClerkController.ClerkRole.Regular && c.IsOnBreak() && c.assignedWorkstation != null && ClientSpawner.GetServiceProviderAtDesk(c.assignedWorkstation.deskId) == null);
    }

    public override System.Type GetExecutorType() { return typeof(CoverDeskExecutor); }
}