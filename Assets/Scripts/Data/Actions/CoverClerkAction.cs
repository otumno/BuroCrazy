using UnityEngine;
using System.Linq;
using Managers;

[CreateAssetMenu(fileName = "Action_CoverClerk", menuName = "Bureau/Actions/CoverClerk")]
public class CoverClerkAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условие 1: Это должен быть стажер, и он не должен быть на перерыве.
        if (!(staff is InternController intern) || intern.IsOnBreak())
        {
            return false;
        }

        // ПРАВИЛЬНОЕ УСЛОВИЕ: Ищем клерка (Regular), который сейчас на перерыве
        return Managers.HiringManager.Instance.AllStaff.Any(s =>
            s is ClerkController c &&
            c.clerkRole == ClerkController.ClerkRole.Regular &&
            c.IsOnBreak() &&
            c.assignedWorkstation != null);
    }

    public override System.Type GetExecutorType() { return typeof(CoverDeskExecutor); }
}