using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_PrioritizePayTax", menuName = "Bureau/Actions/PrioritizePayTax")]
public class PrioritizePayTaxAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController registrar) || registrar.IsOnBreak() || registrar.clerkRole != ClerkController.ClerkRole.Registrar)
        {
            return false;
        }

        // Условие: в общей очереди есть хотя бы один клиент с целью "Просто спросить"
        return registrar.IsAvailableToServe && ClientQueueManager.Instance != null && ClientQueueManager.Instance.HasCallableClient(ClientGoal.PayTax);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(PrioritizePayTaxExecutor);
    }
}