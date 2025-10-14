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

    public override System.Type GetExecutorType()
    {
        return typeof(GoToCoolerExecutor);
    }
}