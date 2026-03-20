// Файл: Assets/Scripts/Data/Actions/Action_GoToCooler.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Action_GoToCooler", menuName = "Bureau/Actions/System/GoToCooler")]
public class Action_GoToCooler : StaffAction
{
    [Header("Настройки потребности")]
    [Range(0f, 1f)] public float moraleThreshold = 0.4f;

    public Action_GoToCooler()
    {
        category = ActionCategory.System;
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // --- ФИКС: Если мы УЖЕ выполняем это действие, не отменяем его на полпути! ---
        if (staff.currentAction == this) return true;
        
        float modifiedThreshold = moraleThreshold + (staff.skills.sedentaryResilience * 0.3f);
        // ИСПРАВЛЕНИЕ: Мораль тоже от 0 до 100
        return staff.morale <= (modifiedThreshold * 100f) && !staff.IsOnBreak();
    }

    public override float CalculateUtility(StaffController staff)
    {
        // Инверсия: при 100 морали желание пить = 0, при 20 морали желание = 80
        float score = Mathf.Clamp(100f - staff.morale, 0f, 100f);
        if (score < 20f) return 0f;
        return Mathf.Pow(score / 100f, 3) * 150f;
    }

    public override string GetDebugInfo(StaffController staff)
    {
        if (staff.IsOnBreak()) return "Уже на перерыве";
        float thirst = 1f - (staff.morale / 100f);
        if (thirst >= 0.95f) return "Иду пить!";
        return $"Жажда: {thirst:P0}";
    }

    public override System.Type GetExecutorType() => typeof(GoToCoolerExecutor);
}
