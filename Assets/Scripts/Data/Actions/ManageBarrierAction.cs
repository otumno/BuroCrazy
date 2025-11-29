using Data.Calendar;
using Managers;
using UnityEngine;

// todo: move operate conditions somewhere to remove code duplicates
[CreateAssetMenu(fileName = "Action_ManageBarrier", menuName = "Bureau/Actions/ManageBarrier")]
public class ManageBarrierAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is GuardMovement) || GuardManager.Instance == null) return false;
        var barrier = GuardManager.Instance.securityBarrier;
        if (barrier == null) return false;

        var currentPeriodName = DayPeriodManager.Instance.CurrentPeriodType;

        if (currentPeriodName == CalendarDayPeriodType.Morning && barrier.IsActive())
            return true;

        var activeClients = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        if (currentPeriodName == CalendarDayPeriodType.Night && !barrier.IsActive() && activeClients.Length == 0)
            return true;

        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ManageBarrierExecutor);
    }
}