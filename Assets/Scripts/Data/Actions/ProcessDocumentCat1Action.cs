// Файл: Assets/Scripts/Data/Actions/Action_ProcessDocumentCat1.cs
using UnityEngine;
using System.Linq;

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
        if (!(staff is ClerkController clerk) || clerk.role != ClerkController.ClerkRole.Regular || clerk.IsOnBreak() || clerk.assignedWorkstation == null)
        {
            return false;
        }
        
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        return zone != null && zone.GetOccupyingClients().Any();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ProcessDocumentCat1Executor);
    }
}