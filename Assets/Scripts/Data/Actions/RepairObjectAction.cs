// Assets/Scripts/Data/Actions/RepairObjectAction.cs
using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_RepairObject", menuName = "Bureau/Actions/RepairObject")]
public class RepairObjectAction : StaffAction
{
    public RepairObjectAction()
    {
        category = ActionCategory.System;
        priority = 10; // Высокий приоритет!
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ServiceWorkerController worker) || worker.IsOnBreak())
        {
            return false;
        }

        // ОПТИМИЗАЦИЯ: Спрашиваем у менеджера, есть ли работа
        if (DurabilityManager.Instance == null) return false;
        
        return DurabilityManager.Instance.HasObjectsToRepair();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(RepairObjectExecutor);
    }
}