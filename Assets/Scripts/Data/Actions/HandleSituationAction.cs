// Assets/Scripts/Data/Actions/HandleSituationAction.cs
using UnityEngine;
using System.Linq;
using Gameplay;

[CreateAssetMenu(fileName = "Action_HandleSituation", menuName = "Bureau/Actions/HandleSituation")]
public class HandleSituationAction : StaffAction
{
    [Header("Настройки для Регистратора")]
    [Tooltip("Радиус, в котором регистратор будет 'кричать' подсказки, не вставая с места.")]
    public float remoteHelpRadius = 4f;

    public HandleSituationAction()
    {
        category = ActionCategory.Tactic;
        priority = 50; // Высокий приоритет!
    }

    public override bool AreConditionsMet(StaffController staff)
    {
        // Общие проверки: сотрудник не должен быть на перерыве
        if (staff.IsOnBreak())
        {
            return false;
        }

        // Находим ближайшего запутавшегося клиента
        ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
        if (confusedClient == null)
        {
            return false; // Если таких клиентов нет, действие невозможно
        }
  
        // ПРОВЕРКА: Помогаем только если клиент никем не занят, либо занят НАМИ
        if (confusedClient.assignedHelper != null && confusedClient.assignedHelper != staff)
        {
            return false; // Не можем помочь, если кто-то другой уже назначен
        }
  
        // ----- НОВАЯ ЛОГИКА: Проверка роли -----
        // Если это регистратор, применяем особое правило
        if (staff is ClerkController clerk && clerk.clerkRole == ClerkController.ClerkRole.Registrar)
        {
            // Условие для регистратора: "Запутавшийся клиент находится в пределах моего радиуса помощи"
            return Vector2.Distance(staff.transform.position, confusedClient.transform.position) < remoteHelpRadius;
        }

        // Для всех остальных (например, стажёра) действует старое правило:
        // "Если есть хоть один запутавшийся клиент, действие возможно"
        return true;
    }

    // Бросаем всё и бежим помогать (вес 150+)
    public override float CalculateUtility(StaffController staff)
    {
        float utility = base.CalculateUtility(staff);
        
        // Добавляем бонус soft skills к помощи клиентам
        var aiConfig = AIBalanceConfig.Instance;
        float softSkillsBonus = aiConfig != null ? staff.skills.softSkills * aiConfig.softSkillsHelpBonus : 0f;
        utility += softSkillsBonus;
        
        return utility + 100f;
    }

    // Рапорт для дебаггера
    public override string GetDebugInfo(StaffController staff)
    {
        ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
        if (confusedClient == null) return "Нет потерявшихся";
        if (confusedClient.assignedHelper != null && confusedClient.assignedHelper != staff) return "Уже помогают";
        return "Цель найдена!";
    }

    public override System.Type GetExecutorType()
    {
        return typeof(HandleSituationExecutor);
    }
}
