using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CoverCashier", menuName = "Bureau/Actions/CoverCashier")]
public class CoverCashierAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is InternController intern) || intern.IsOnBreak()) return false;

        // Ищем именно КАССИРА на перерыве
        return HiringManager.Instance.AllStaff.OfType<ClerkController>()
            .Any(c => c.role == ClerkController.ClerkRole.Cashier && c.IsOnBreak() && c.assignedServicePoint != null && ClientSpawner.GetServiceProviderAtDesk(c.assignedServicePoint.deskId) == null);
    }

    public override System.Type GetExecutorType() { return typeof(CoverDeskExecutor); }
}