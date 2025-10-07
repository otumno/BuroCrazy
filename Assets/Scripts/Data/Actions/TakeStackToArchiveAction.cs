using UnityEngine;

[CreateAssetMenu(fileName = "Action_TakeStackToArchive", menuName = "Bureau/Actions/TakeStackToArchive")]
public class TakeStackToArchiveAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak())
        {
            return false;
        }

        // Проверяем, что у клерка есть стол и на нем есть стопка документов
        if (clerk.assignedServicePoint == null || clerk.assignedServicePoint.documentStack == null)
        {
            return false;
        }

        // --- ГЛАВНОЕ УСЛОВИЕ ---
        // Действие доступно, ТОЛЬКО ЕСЛИ стопка на столе клерка ПОЛНА.
        return clerk.assignedServicePoint.documentStack.IsFull;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(TakeStackToArchiveExecutor);
    }
}