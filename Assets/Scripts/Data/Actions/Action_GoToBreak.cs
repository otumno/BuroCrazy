// Файл: Assets/Scripts/Data/Actions/Action_GoToBreak.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Action_GoToBreak", menuName = "Bureau/Actions/System/GoToBreak")]
public class Action_GoToBreak : StaffAction
{
    [Header("Настройки потребности")]
    [Range(0f, 1f)] public float energyThreshold = 0.3f; // 30%

    public Action_GoToBreak()
    {
        category = ActionCategory.System;
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // ИСПРАВЛЕНИЕ: Умножаем порог на 100, т.к. энергия 0-100
        return staff.energy <= (energyThreshold * 100f) && !staff.IsOnBreak();
    }

    public override float CalculateUtility(StaffController staff)
    {
        // Считаем усталость от 0 до 1
        float exhaustion = 1f - (staff.energy / 100f);
        if (exhaustion < 0.2f) return 0f;
        
        // Экспоненциальный рост: чем больше устал, тем сильнее перевешивает работу
        return Mathf.Pow(exhaustion, 3) * 200f; 
    }

    public override string GetDebugInfo(StaffController staff)
    {
        if (staff.IsOnBreak()) return "Уже отдыхает";
        float exhaustion = 1f - (staff.energy / 100f);
        if (exhaustion >= 0.95f) return "КРИТИЧНО! Падаю!";
        return $"Усталость: {exhaustion:P0}";
    }

    public override System.Type GetExecutorType() => typeof(GoToBreakExecutor);
}
