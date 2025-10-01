using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_OperateBarrier", menuName = "Bureau/Actions/OperateBarrier")]
public class OperateBarrierAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // Убедимся, что это охранник, и что менеджер доступен
        if (!(staff is GuardMovement) || GuardManager.Instance == null)
        {
            return false;
        }

        var barrier = GuardManager.Instance.securityBarrier;
        if (barrier == null) return false;

        var clientSpawner = ClientSpawner.Instance;
        if (clientSpawner == null) return false;

        string currentPeriodName = ClientSpawner.CurrentPeriodName;

        // Условие 1: Сейчас "Утро", и барьер АКТИВЕН (закрыт) -> нужно ОТКРЫТЬ
        if (currentPeriodName == "Утро" && barrier.IsActive())
        {
            return true;
        }

        // Условие 2: Сейчас "Ночь", барьер НЕ АКТИВЕН (открыт), и на сцене нет клиентов -> нужно ЗАКРЫТЬ
        var activeClients = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        if (currentPeriodName == "Ночь" && !barrier.IsActive() && activeClients.Length == 0)
        {
            return true;
        }

        return false;
    }

    public override System.Type GetExecutorType()
    {
        // Это действие выполняется скриптом OperateBarrierExecutor
        return typeof(OperateBarrierExecutor);
    }
}