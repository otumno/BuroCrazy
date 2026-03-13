// Файл: Assets/Scripts/Data/Actions/Action_ServiceAtCashier.cs
using UnityEngine;
using System.Linq;
using Managers;
using Gameplay;

[CreateAssetMenu(fileName = "Action_ServiceAtCashier", menuName = "Bureau/Actions/ServiceAtCashier")]
public class Action_ServiceAtCashier : StaffAction
{
    // Конструктор для установки приоритета
    public Action_ServiceAtCashier()
    {
        category = ActionCategory.Tactic;
        priority = 30; // Выше, чем у бухгалтерии (10)
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController clerk) || clerk.clerkRole != ClerkController.ClerkRole.Cashier || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        
        // Проверка по зоне
        bool clientInZone = zone != null && zone.GetOccupyingClients().Any(c => c.billToPay > 0 || c.mainGoal == ClientGoal.PayTax);
        
        // Проверка по физической дистанции (бронебойная)
        bool clientPhysicallyNear = false;
        if (clerk.assignedWorkstation.clientStandPoint != null)
        {
            clientPhysicallyNear = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
                .Any(c => c != null && !c.isLeavingSuccessfully && (c.billToPay > 0 || c.mainGoal == ClientGoal.PayTax) && Vector2.Distance(c.transform.position, clerk.assignedWorkstation.clientStandPoint.transform.position) < 1.2f);
        }
        
        return clientInZone || clientPhysicallyNear;
    }

    public override float CalculateUtility(StaffController staff)
    {
        float utility = base.CalculateUtility(staff);
        
        // Добавляем бонус мастерства к работе на кассе
        var aiConfig = AIBalanceConfig.Instance;
        float masteryBonus = aiConfig != null ? 1f + (staff.skills.paperworkMastery * aiConfig.masteryWorkMultiplier) : 1f;
        utility *= masteryBonus;

        // Проверяем клиента в зоне и бонус за взятку
        if (staff is ClerkController clerk && clerk.assignedWorkstation != null)
        {
            var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
            if (zone != null)
            {
                var clients = zone.GetOccupyingClients();
                foreach (var client in clients)
                {
                    // Если client.billToPay > 100, добавляем бонус коррупции
                    if (client.billToPay > 100f)
                    {
                        float corruptionBonus = aiConfig?.GetCorruptionCashierBonus(staff.skills.corruption) ?? 0f;
                        utility += corruptionBonus;
                        break; // Достаточно одного клиента с большим счетом
                    }
                }
            }
        }
        
        return utility;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ServiceAtCashierExecutor);
    }
}