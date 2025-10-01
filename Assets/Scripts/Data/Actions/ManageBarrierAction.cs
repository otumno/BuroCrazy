using UnityEngine;

[CreateAssetMenu(fileName = "Action_ManageBarrier", menuName = "Bureau/Actions/ManageBarrier")]
public class ManageBarrierAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is GuardMovement) || GuardManager.Instance == null) return false;
        var barrier = GuardManager.Instance.securityBarrier;
        if (barrier == null) return false;

        string currentPeriodName = ClientSpawner.CurrentPeriodName;

        if (currentPeriodName == "Утро" && barrier.IsActive()) return true;

        var activeClients = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        if (currentPeriodName == "Ночь" && !barrier.IsActive() && activeClients.Length == 0) return true;

        return false;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ManageBarrierExecutor);
    }
}