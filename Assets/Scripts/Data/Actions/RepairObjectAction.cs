// Assets/Scripts/Data/Actions/RepairObjectAction.cs
using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_RepairObject", menuName = "Bureau/Actions/RepairObject")]
public class RepairObjectAction : StaffAction
{
    public RepairObjectAction()
    {
        category = ActionCategory.System;
        priority = 10; 
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ServiceWorkerController worker) || worker.IsOnBreak()) return false;
        if (DurabilityManager.Instance == null) return false;
        
        return DurabilityManager.Instance.HasObjectsToRepair();
    }

    public override float CalculateUtility(StaffController staff)
    {
        float utility = base.CalculateUtility(staff);

        if (DurabilityManager.Instance != null)
        {
            var target = DurabilityManager.Instance.GetPriorityRepairTarget(staff.transform.position);
            if (target != null)
            {
                float damagePercent = 1f - (target.currentHealth / target.maxHealth);
                
                if (!target.IsUsable()) 
                {
                    utility += 150f; 
                }
                else
                {
                    utility += damagePercent * 50f;
                }

                utility += staff.skills.pedantry * 15f; 
            }
        }
        return utility;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(RepairObjectExecutor);
    }
}