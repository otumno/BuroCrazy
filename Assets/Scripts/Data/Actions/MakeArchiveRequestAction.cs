using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_MakeArchiveRequest", menuName = "Bureau/Actions/MakeArchiveRequest")]
public class MakeArchiveRequestAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ClerkController registrar) || registrar.role != ClerkController.ClerkRole.Registrar || registrar.IsOnBreak()) return false;

        var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedServicePoint.deskId);
        if (zone == null) return false;

        // 1. Проверяем, есть ли у регистратора клиент с нужной целью
        bool hasClientForRequest = zone.GetOccupyingClients().Any(c => c.mainGoal == ClientGoal.GetArchiveRecord);
        if (!hasClientForRequest) return false;

        // --- НАЧАЛО ЗАЩИТЫ ОТ ДУРАКА ---

        // 2. Проверяем, есть ли на работе ХОТЯ БЫ ОДИН Архивариус,
        //    у которого есть действие "Найти документ по запросу".
        var retrieveActionType = ActionType.RetrieveDocument; 
        bool isArchivistAvailable = HiringManager.Instance.AllStaff
            .Any(s => 
                s.currentRole == StaffController.Role.Archivist && 
                s.IsOnDuty() && 
                !s.IsOnBreak() &&
                s.activeActions.Any(a => a.actionType == retrieveActionType)
            );

        // --- КОНЕЦ ЗАЩИТЫ ОТ ДУРАКА ---

        return isArchivistAvailable; // Действие доступно, только если есть и клиент, и исполнитель
    }
    public override System.Type GetExecutorType() { return typeof(MakeArchiveRequestExecutor); }
}