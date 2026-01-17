// Assets/Scripts/Data/Actions/ReplenishResourceAction.cs
using UnityEngine;
using System.Linq;
using Managers;
using Gameplay;

[CreateAssetMenu(fileName = "Action_ReplenishResource", menuName = "Bureau/Actions/ReplenishResource")]
public class ReplenishResourceAction : StaffAction
{
    public ReplenishResourceAction()
    {
        category = ActionCategory.System;
        priority = 15; // Важно, но документы (20) важнее
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // Проверяем, есть ли на сцене контейнеры, которым нужно пополнение
        var containers = Object.FindObjectsByType<ResourceContainer>(FindObjectsSortMode.None);
        return containers.Any(c => c.NeedsReplenishment);
    }

    public override System.Type GetExecutorType()
    {
        return typeof(ReplenishResourceExecutor);
    }
}