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

    public override float CalculateUtility(StaffController staff)
    {
        // Экспоненциальный рост желания. 
        // Если нужда (bladder) = 0.5, вес = 12.5 (работа важнее).
        // Если нужда = 0.9, вес = 72.9 (бросает работу и бежит).
        // Педанты терпят дольше (снижаем вес).
        float pedantryFactor = 1f - (staff.skills.pedantry * 0.3f);
        float need = Mathf.Clamp01(staff.bladder);
        
        if (need < 0.3f) return 0f; // До 30% вообще не хочет
        
        return Mathf.Pow(need, 3) * 100f * pedantryFactor; 
    }

    public override System.Type GetExecutorType()
    {
        return typeof(GoToToiletExecutor);
    }

    // Градация для дебаггера
    public override string GetDebugInfo(StaffController staff)
    {
        if (staff.IsOnBreak()) return "Уже на перерыве";
        
        float needPercent = staff.bladder / bladderThreshold;
        if (needPercent >= 1f) return "КРИТИЧНО! Бегу!";
        
        return $"Копит: {needPercent:P0}"; // Покажет, например: "Копит: 65%"
    }
}