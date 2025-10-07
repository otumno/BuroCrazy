using UnityEngine;

[CreateAssetMenu(fileName = "Action_JanitorPatrol", menuName = "Bureau/Actions/JanitorPatrol")]
public class JanitorPatrolAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ServiceWorkerController worker) || worker.IsOnBreak()) return false;
        // Условие всегда истинно, если нет другой работы
        return true;
    }
    public override System.Type GetExecutorType() { return typeof(JanitorPatrolExecutor); }
}