// Файл: Assets/Scripts/Data/Actions/Action_GoToToilet.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Action_GoToToilet", menuName = "Bureau/Actions/System/GoToToilet")]
public class Action_GoToToilet : StaffAction
{
    [Header("Настройки потребности")]
    [Tooltip("Порог потребности 'Мочевой пузырь', при котором это действие становится возможным.")]
    [Range(0f, 1f)]
    public float bladderThreshold = 0.7f; // 70%

    public Action_GoToToilet()
    {
        category = ActionCategory.System; // Помечаем как системное действие
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // Условие: потребность выше порога и сотрудник не на перерыве по другой причине
        return staff.bladder >= bladderThreshold && !staff.IsOnBreak();
    }

    public override System.Type GetExecutorType()
    {
        return typeof(GoToToiletExecutor);
    }
}