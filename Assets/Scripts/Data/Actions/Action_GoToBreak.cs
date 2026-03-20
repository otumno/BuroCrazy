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
        // --- ФИКС: Если мы УЖЕ выполняем это действие, не отменяем его на полпути! ---
        if (staff.currentAction == this) return true;
        
        // ИСПРАВЛЕНИЕ: Умножаем порог на 100, т.к. энергия 0-100
        return staff.energy <= (energyThreshold * 100f) && !staff.IsOnBreak();
    }

    public override float CalculateUtility(StaffController staff)
    {
        // Инверсия: при 100 энергии желание отдыхать = 0, при 20 энергии желание = 80
        float score = Mathf.Clamp(100f - staff.energy, 0f, 100f);
        if (score < 20f) return 0f;
        
        // Экспоненциальный рост: чем больше устал, тем сильнее перевешивает работу
        return Mathf.Pow(score / 100f, 3) * 200f;
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
