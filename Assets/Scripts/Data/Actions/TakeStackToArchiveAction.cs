using UnityEngine;
using System.Linq;
using Managers;
using Gameplay;

[CreateAssetMenu(fileName = "Action_TakeStackToArchive", menuName = "Bureau/Actions/TakeStackToArchive")]
public class TakeStackToArchiveAction : StaffAction
{
    // Конструктор для установки приоритета
    public TakeStackToArchiveAction()
    {
        category = ActionCategory.System;
        priority = 5; // Выше, чем "Сортировать бумаги" (1)
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak())
        {
            return false;
        }

        if (clerk.assignedWorkstation == null || clerk.assignedWorkstation.documentStack == null)
        {
            return false;
        }

        // Носим бумаги, если накопилось хотя бы 3 штуки, и перед столом нет клиентов
        var stack = clerk.assignedWorkstation.documentStack;
        if (stack.CurrentSize >= 3)
        {
            var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
            bool hasClients = zone != null && zone.GetOccupyingClients().Any();
            
            bool physicallyNear = false;
            if (clerk.assignedWorkstation.clientStandPoint != null)
            {
                physicallyNear = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
                    .Any(c => c != null && !c.isLeavingSuccessfully && Vector2.Distance(c.transform.position, clerk.assignedWorkstation.clientStandPoint.transform.position) < 1.2f);
            }
            
            return !hasClients && !physicallyNear;
        }
        
        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(TakeStackToArchiveExecutor);
    }
}
