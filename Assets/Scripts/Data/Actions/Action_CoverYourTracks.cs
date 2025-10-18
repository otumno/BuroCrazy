// Файл: Assets/Scripts/Data/Actions/Action_CoverYourTracks.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_CoverYourTracks", menuName = "Bureau/Actions/Tactic/CoverYourTracks")]
public class CoverYourTracksAction : StaffAction
{
    public CoverYourTracksAction()
    {
        category = ActionCategory.Tactic;
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (staff.IsOnBreak() || FinancialLedgerManager.Instance.globalCorruptionScore <= 0)
        {
            return false;
        }

        // Выполняется, когда перед сотрудником нет клиента
        if (staff.assignedWorkstation != null)
        {
            var zone = ClientSpawner.GetZoneByDeskId(staff.assignedWorkstation.deskId);
            return zone != null && !zone.GetOccupyingClients().Any();
        }
        return true;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CoverYourTracksExecutor);
    }
}