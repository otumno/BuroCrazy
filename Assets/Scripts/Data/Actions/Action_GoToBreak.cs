// Файл: Assets/Scripts/Data/Actions/Action_GoToBreak.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Action_GoToBreak", menuName = "Bureau/Actions/System/GoToBreak")]
public class Action_GoToBreak : StaffAction
{
    [Header("Настройки потребности")]
    [Tooltip("Порог 'Энергии', ниже которого это действие становится возможным.")]
    [Range(0f, 1f)]
    public float energyThreshold = 0.3f; // 30%

    public Action_GoToBreak()
    {
        category = ActionCategory.System;
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        return staff.energy <= energyThreshold && !staff.IsOnBreak();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(GoToBreakExecutor);
    }
}