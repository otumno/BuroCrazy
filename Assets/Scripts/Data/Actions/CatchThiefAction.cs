// Assets/Scripts/Data/Actions/CatchThiefAction.cs
using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_CatchThief", menuName = "Bureau/Actions/Guard/CatchThief")]
public class CatchThiefAction : StaffAction
{
    public override bool AreConditionsMet(StaffController staff)
    {
        // ИСПРАВЛЕНИЕ: Получаем компонент через GetComponent
        var guard = staff.GetComponent<GuardMovement>();
        
        // Проверяем, что это охранник и он на смене
        if (guard == null || guard.IsOnBreak()) return false;

        // Есть ли вор?
        return GuardManager.Instance != null && GuardManager.Instance.currentThief != null;
    }

    public override float CalculateUtility(StaffController staff)
    {
        return 1000f; 
    }

    public override System.Type GetExecutorType()
    {
        return typeof(CatchThiefExecutor);
    }
}