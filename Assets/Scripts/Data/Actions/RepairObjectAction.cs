// Assets/Scripts/Data/Actions/RepairObjectAction.cs
using UnityEngine;
using System.Linq;
using Gameplay;

[CreateAssetMenu(fileName = "Action_RepairObject", menuName = "Bureau/Actions/RepairObject")]
public class RepairObjectAction : StaffAction
{
    public RepairObjectAction()
    {
        // Это системное действие, уборщик сам должен искать сломанное
        category = ActionCategory.System;
        priority = 10; // Высокий приоритет! Важнее уборки мусора.
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (!(staff is ServiceWorkerController worker) || worker.IsOnBreak())
        {
            return false;
        }

        // Ищем любой объект с Durability, который сломан (Worn или Broken)
        // ВНИМАНИЕ: FindObjectsByType может быть медленным, если объектов тысячи. 
        // Если будет тормозить, создадим DurabilityManager по аналогии с MessManager.
        var allBreakables = FindObjectsByType<OfficeObjectDurability>(FindObjectsSortMode.None);
        
        // Чиним только то, что имеет < 100% здоровья (или < порога Worn, если хотите экономить силы)
        return allBreakables.Any(d => d.currentHealth < d.maxHealth);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(RepairObjectExecutor);
    }
}