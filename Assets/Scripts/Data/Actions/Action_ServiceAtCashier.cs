// Файл: Assets/Scripts/Data/Actions/Action_ServiceAtCashier.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_ServiceAtCashier", menuName = "Bureau/Actions/ServiceAtCashier")]
public class Action_ServiceAtCashier : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.role != ClerkController.ClerkRole.Cashier || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        
        // ----- ГЛАВНОЕ ИЗМЕНЕНИЕ -----
        // Условие теперь: "В зоне есть клиент, у которого ЕСТЬ счет ИЛИ его цель - оплатить налог".
        return zone != null && zone.GetOccupyingClients().Any(c => c.billToPay > 0 || c.mainGoal == ClientGoal.PayTax);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ServiceAtCashierExecutor);
    }
}