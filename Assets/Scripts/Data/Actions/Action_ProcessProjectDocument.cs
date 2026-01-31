// Assets/Scripts/Data/Actions/Action_ProcessProjectDocument.cs
using UnityEngine;
using Managers;
using Gameplay.Documents;

[CreateAssetMenu(fileName = "Action_ProcessProjectDocument", menuName = "Bureau/Actions/ProcessProjectDocument")]
public class Action_ProcessProjectDocument : StaffAction
{
    public Action_ProcessProjectDocument()
    {
        // Это тактическое действие, но с высоким приоритетом,
        // чтобы клерки делали это в свободное от клиентов время.
        category = ActionCategory.Tactic; 
        priority = 5; 
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // 1. Проверяем роль (только Регистратор или Кассир)
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        if (clerk.clerkRole != ClerkController.ClerkRole.Registrar && clerk.clerkRole != ClerkController.ClerkRole.Cashier)
        {
            return false;
        }

        // 2. Проверяем стопку на столе: есть ли там подходящий документ?
        var stack = clerk.assignedWorkstation.documentStack;
        if (stack == null || stack.IsEmpty) return false;

        // Используем новый метод из DocumentStack
        return stack.FindPendingProjectDocument(staff.currentRole) != null;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ProcessProjectDocumentExecutor);
    }
}