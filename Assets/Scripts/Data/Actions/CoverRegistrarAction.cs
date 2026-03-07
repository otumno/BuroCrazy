// Файл: Assets/Scripts/Data/Actions/CoverRegistrarAction.cs
using UnityEngine;
using System.Linq;
using Managers;

[CreateAssetMenu(fileName = "Action_CoverRegistrar", menuName = "Bureau/Actions/CoverRegistrar")]
public class CoverRegistrarAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условие 1: Это должен быть стажер, и он не должен быть на перерыве.
        if (!(staff is InternController intern) || intern.IsOnBreak())
        {
            return false;
        }

        // ПРАВИЛЬНОЕ УСЛОВИЕ: Ищем регистратора, который сейчас на перерыве
        return Managers.HiringManager.Instance.AllStaff.Any(s =>
            s is ClerkController c &&
            c.clerkRole == ClerkController.ClerkRole.Registrar &&
            c.IsOnBreak() &&
            c.assignedWorkstation != null);
    }

    public override System.Type GetExecutorType() { return typeof(CoverDeskExecutor); }
}