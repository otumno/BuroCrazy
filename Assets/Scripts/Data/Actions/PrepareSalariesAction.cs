using UnityEngine;
using System.Linq;
using Managers;

[CreateAssetMenu(fileName = "Action_PrepareSalaries", menuName = "Bureau/Actions/PrepareSalaries")]
public class PrepareSalariesAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условие: это Кассир/Бухгалтер, и есть хотя бы один сотрудник с неоплаченными периодами.
        if (!(staff is ClerkController { clerkRole: ClerkController.ClerkRole.Cashier }) || staff.IsOnBreak())
        {
            return false;
        }

        // Проверяем, нужно ли кому-то готовить зарплату
        return HiringManager.Instance.AllStaff.Any(s => s.unpaidPeriods > 0);
    }

    public override System.Type GetExecutorType() { return typeof(PrepareSalariesExecutor); }
}