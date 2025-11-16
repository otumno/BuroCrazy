using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CoverRegistrar", menuName = "Bureau/Actions/CoverRegistrar")]
public class CoverRegistrarAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is InternController intern) || intern.IsOnBreak()) return false;

        // Ищем всех нанятых регистраторов
        var registrars = HiringManager.Instance.AllStaff.OfType<ClerkController>()
            .Where(c => c.role == ClerkController.ClerkRole.Registrar);

        // Ищем, есть ли среди них тот, кто на перерыве, и чей стол сейчас пуст
        foreach (var r in registrars)
        {
            if (r.IsOnBreak() && r.assignedServicePoint != null && ClientSpawner.GetServiceProviderAtDesk(r.assignedServicePoint.deskId) == null)
            {
                return true; // Найдена работа!
            }
        }
        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CoverRegistrarExecutor);
    }
}