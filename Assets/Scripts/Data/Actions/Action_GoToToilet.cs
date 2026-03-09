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
        // Шкала 0-100, поэтому делим порог на 100f
        return staff.bladder >= (bladderThreshold * 100f) && !staff.IsOnBreak();
    }

    public override float CalculateUtility(StaffController staff)
    {
        // Экспоненциальный рост желания.
        // Шкала 0-100, поэтому делим на 100f.
        // Если нужда = 50, вес = 12.5 (работа важнее).
        // Если нужда = 90, вес = 72.9 (бросает работу и бежит).
        // Педанты терпят дольше (снижаем вес).
        float pedantryFactor = 1f - (staff.skills.pedantry * 0.3f);
        float need = staff.bladder / 100f;
        
        if (need < 0.3f) return 0f; // До 30% вообще не хочет
        
        return Mathf.Pow(need, 3) * 150f * pedantryFactor;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(GoToToiletExecutor);
    }

    // Градация для дебаггера
    public override string GetDebugInfo(StaffController staff)
    {
        if (staff.IsOnBreak()) return "Уже на перерыве";
        
        // Шкала 0-100, поэтому делим на 100f для отображения процентов
        float needPercent = (staff.bladder / 100f) / bladderThreshold;
        if (needPercent >= 1f) return "КРИТИЧНО! Бегу!";
        
        return $"Копит: {needPercent:P0}"; // Покажет, например: "Копит: 65%"
    }
}