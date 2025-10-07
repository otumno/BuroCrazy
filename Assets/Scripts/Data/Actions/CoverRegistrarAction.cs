using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CoverRegistrar", menuName = "Bureau/Actions/CoverRegistrar")]
public class CoverRegistrarAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Правило 1: Только для Стажера
        if (!(staff is InternController intern) || intern.IsOnBreak()) return false;

        // Правило 2: Ищем именно РЕГИСТРАТОРА на перерыве
        return HiringManager.Instance.AllStaff.OfType<ClerkController>()
            .Any(c => c.role == ClerkController.ClerkRole.Registrar && c.IsOnBreak() && c.assignedServicePoint != null && ClientSpawner.GetServiceProviderAtDesk(c.assignedServicePoint.deskId) == null);
    }

    public override System.Type GetExecutorType() { return typeof(CoverDeskExecutor); }
}