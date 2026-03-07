// Файл: Assets/Scripts/Data/Actions/Action_GoToCooler.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Action_GoToCooler", menuName = "Bureau/Actions/System/GoToCooler")]
public class Action_GoToCooler : StaffAction
{
    [Header("Настройки потребности")]
    [Tooltip("Порог 'Морали', ниже которого это действие становится возможным.")]
    [Range(0f, 1f)]
    public float moraleThreshold = 0.4f; // 40%

    public Action_GoToCooler()
    {
        category = ActionCategory.System;
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // Усидчивые сотрудники реже ходят к кулеру
        float modifiedThreshold = moraleThreshold + (staff.skills.sedentaryResilience * 0.3f);
        return staff.morale <= modifiedThreshold && !staff.IsOnBreak();
    }

    public override float CalculateUtility(StaffController staff)
    {
        float need = Mathf.Clamp01(1f - staff.morale);
        if (need < 0.2f) return 0f;
        return Mathf.Pow(need, 2) * 60f;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(GoToCoolerExecutor);
    }
}