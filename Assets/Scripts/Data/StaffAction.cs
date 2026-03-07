// Файл: Assets/Scripts/Data/StaffAction.cs
using UnityEngine;
using System.Collections.Generic;
using Utilities;

[System.Serializable]
public class SkillInfluence
{
    public SkillType skill;
    public bool isPositiveEffect = true;
    [Range(0f, 1f)]
    public float strength = 0.5f;
}

// ----- НАЧАЛО ИЗМЕНЕНИЙ -----
public enum ActionCategory
{
    Tactic, // Тактическое, назначается игроком
    System  // Системное, используется ИИ для потребностей и безделья
}
// ----- КОНЕЦ ИЗМЕНЕНИЙ -----

public abstract class StaffAction : ScriptableObject
{
    [Header("Основная информация")]
    public ActionType actionType;
    public string displayName;
    [TextArea(2, 4)]
    public string description;

    // ----- НАЧАЛО ИЗМЕНЕНИЙ -----
    [Tooltip("Tactic: Действие, назначаемое игроком. System: Скрытое действие для ИИ.")]
    public ActionCategory category = ActionCategory.Tactic;
    // ----- КОНЕЦ ИЗМЕНЕНИЙ -----

    [Header("Настройки геймплея")]
	[Tooltip("Приоритет этого действия. Чем выше число, тем важнее действие.")]
	public int priority = 0;
    [Tooltip("Базовый шанс успеха (от 0.0 до 1.0)")]
    [Range(0f, 1f)]
    public float baseSuccessChance = 0.8f;
    [Tooltip("Минимальный итоговый шанс успеха (от 0.0 до 1.0)")]
    [Range(0f, 1f)]
    public float minSuccessChance = 0.3f;
    [Tooltip("Максимальный итоговый шанс успеха (от 0.0 до 1.0)")]
    [Range(0f, 1f)]
    public float maxSuccessChance = 0.95f;
    [Tooltip("Основная характеристика, влияющая на успех")]
    public SkillInfluence primarySkill;
    [Tooltip("Включить влияние второй характеристики?")]
    public bool useSecondarySkill;
    [Tooltip("Вторая (опциональная) характеристика")]
    public SkillInfluence secondarySkill;
    [Tooltip("Для циклических действий (патруль): как долго (в сек.) сотрудник будет их выполнять.")]
    public float actionDuration = 30f;
    [Tooltip("Для патруля: сколько точек нужно обойти за один цикл. Если 0, используется actionDuration.")]
    public int patrolPointsToVisit = 3;
    [Tooltip("На сколько секунд это действие уходит 'на перезарядку' после выполнения.")]
    public float actionCooldown = 60f;
    
    [Header("Требования и тип")]
    public int minRankRequired = 0;
    public List<StaffController.Role> applicableRoles;
    public bool isUnique = false;
    
    public abstract bool AreConditionsMet(StaffController staff);
    public abstract System.Type GetExecutorType();

    // --- НОВЫЙ МЕТОД UTILITY AI ---
    // Базовый расчет веса действия. Наследники будут его переопределять.
    public virtual float CalculateUtility(StaffController staff)
    {
        float utility = priority * 10f; // Базовый вес

        // Влияние навыков на желание делать эту работу
        if (primarySkill != null && primarySkill.skill != SkillType.PaperworkMastery)
        {
            float skillVal = 0f;
            switch (primarySkill.skill)
            {
                case SkillType.SedentaryResilience: skillVal = staff.skills.sedentaryResilience; break;
                case SkillType.Pedantry: skillVal = staff.skills.pedantry; break;
                case SkillType.SoftSkills: skillVal = staff.skills.softSkills; break;
                case SkillType.Corruption: skillVal = staff.skills.corruption; break;
                default: skillVal = 1f; break;
            }
            utility += (primarySkill.isPositiveEffect ? skillVal : -skillVal) * (primarySkill.strength * 10f);
        }

        // Модификаторы от политик Директора
        if (Managers.PolicyManager.Instance != null)
        {
            utility *= Managers.PolicyManager.Instance.GlobalWorkSpeedMod;
        }

        return utility;
    }

    // --- МЕТОД ДЛЯ ДЕБАГГЕРА ---
    // Позволяет действию словами описать, почему оно доступно или недоступно
    public virtual string GetDebugInfo(StaffController staff)
    {
        if (staff.IsOnBreak()) return "На перерыве";
        return AreConditionsMet(staff) ? "Условия выполнены" : "Нет условий";
    }
}