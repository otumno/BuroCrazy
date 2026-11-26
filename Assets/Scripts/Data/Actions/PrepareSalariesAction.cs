using UnityEngine;
using System.Linq;
using Managers;

[CreateAssetMenu(fileName = "Action_PrepareSalaries", menuName = "Bureau/Actions/PrepareSalaries")]
public class PrepareSalariesAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Только Бухгалтер заправляет зарплатные конверты.
        if (!(staff is ClerkController { clerkRole: ClerkController.ClerkRole.Accountant }) || staff.IsOnBreak())
            return false;

        // Проверяем, нужно ли кому-то готовить зарплату
        var stack = ScenePointsRegistry.Instance?.salaryStackPoint;
        return stack != null && HiringManager.Instance != null && PlayerWallet.Instance != null &&
            PlayerWallet.Instance.CanAfford(Gameplay.EnvelopeStack.PreparationCost) &&
            stack.CurrentEnvelopeCount < Mathf.Min(stack.maxCapacity, HiringManager.Instance.AllStaff.Count + 1) &&
            HiringManager.Instance.AllStaff.Any(s => s != null && s.unpaidPeriods > 0);
    }

    public override System.Type GetExecutorType() { return typeof(PrepareSalariesExecutor); }
}
