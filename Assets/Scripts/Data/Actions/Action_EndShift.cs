// Assets/Scripts/Data/Actions/Action_EndShift.cs
using UnityEngine;
using Managers;

[CreateAssetMenu(fileName = "Action_EndShift", menuName = "Bureau/Actions/System/EndShift")]
public class Action_EndShift : StaffAction
{
    public Action_EndShift()
    {
        category = ActionCategory.System;
        priority = 100; // Очень высокий базовый приоритет
        actionType = ActionType.GoHome;
        displayName = "Уйти домой";
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        if (TimeManager.Instance == null) return false;
        
        // Действие доступно ТОЛЬКО если текущее время НЕ входит в график работы сотрудника
        var currentPeriod = TimeManager.Instance.GetCurrentPeriodType();
        return (staff.WorkShiftMask & currentPeriod) == 0;
    }

    public override float CalculateUtility(StaffController staff)
    {
        float utility = priority; // Базовые 100 очков

        // 1. ПЕДАНТЫ: Хотят уйти ровно по расписанию (+ к желанию уйти)
        utility += staff.skills.pedantry * 50f;

        // 2. ТРУДОГОЛИКИ: Не хотят уходить, если есть дела (- к желанию уйти)
        utility -= staff.skills.paperworkMastery * 80f;

        // 3. УСТАЛОСТЬ/СТРЕСС: Чем больше стресс и меньше энергии, тем сильнее хочется домой
        utility += staff.GetCurrentStress() * 60f;
        utility += (1f - (staff.energy / 100f)) * 40f;

        return utility;
    }

    public override System.Type GetExecutorType() => typeof(DefaultActionExecutor);
}
