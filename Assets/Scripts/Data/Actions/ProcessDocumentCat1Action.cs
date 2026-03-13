// Файл: Assets/Scripts/Data/Actions/Action_ProcessDocumentCat1.cs
using UnityEngine;
using System.Linq;
using Managers;
using Gameplay;

[CreateAssetMenu(fileName = "Action_ProcessDocCat1", menuName = "Бюрократия/Тактические/Работа в офисе (Кат. 1)")]
public class ProcessDocumentCat1Action : StaffAction
{
    public ProcessDocumentCat1Action()
    {
        category = ActionCategory.Tactic; // Убеждаемся, что это тактическое действие
    }

    // НОВАЯ ЛОГИКА: Условие теперь такое же, как у "Обслуживания за столом"
    public override bool AreConditionsMet(StaffController staff)
    {
        // Условие: я Клерк, на рабочем месте, и передо мной стоит клиент.
        if (!(staff is ClerkController clerk) || clerk.clerkRole != ClerkController.ClerkRole.Regular || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        
        // Проверка по зоне
        bool clientInZone = zone != null && zone.GetOccupyingClients().Any();
        
        // Проверка по физической дистанции (бронебойная)
        bool clientPhysicallyNear = false;
        if (clerk.assignedWorkstation.clientStandPoint != null)
        {
            clientPhysicallyNear = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
                .Any(c => c != null && !c.isLeavingSuccessfully && Vector2.Distance(c.transform.position, clerk.assignedWorkstation.clientStandPoint.transform.position) < 1.2f);
        }
        
        return clientInZone || clientPhysicallyNear;
    }

    public override float CalculateUtility(StaffController staff)
    {
        float utility = base.CalculateUtility(staff);
        
        // Добавляем бонус мастерства к обработке документов
        var aiConfig = AIBalanceConfig.Instance;
        float masteryBonus = aiConfig != null ? 1f + (staff.skills.paperworkMastery * aiConfig.masteryWorkMultiplier) : 1f;
        utility *= masteryBonus;
        
        return utility;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ProcessDocumentCat1Executor);
    }
}