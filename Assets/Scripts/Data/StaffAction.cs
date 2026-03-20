using UnityEngine;
using System.Collections.Generic;
using Utilities;

[System.Serializable]
public class SkillInfluence
{
    public SkillType skill;
    public bool isPositiveEffect = true;
    [Range(0f, 1f)] public float strength = 0.5f;
}

public enum ActionCategory
{
    Tactic, // Назначается игроком
    System  // Скрытое действие для ИИ
}

public abstract class StaffAction : ScriptableObject
{
    [Header("Основная информация")]
    public ActionType actionType;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public ActionCategory category = ActionCategory.Tactic;

    [Header("Настройки геймплея")]
    public int priority = 0;
    [Range(0f, 1f)] public float baseSuccessChance = 0.8f;
    [Range(0f, 1f)] public float minSuccessChance = 0.3f;
    [Range(0f, 1f)] public float maxSuccessChance = 0.95f;
    
    public SkillInfluence primarySkill;
    public bool useSecondarySkill;
    public SkillInfluence secondarySkill;
    
    public float actionDuration = 30f;
    public int patrolPointsToVisit = 3;
    public float actionCooldown = 60f;
    
    [Header("Требования и тип")]
    public int minRankRequired = 0;
    public List<StaffController.Role> applicableRoles;
    public bool isUnique = false;
    
    public abstract bool AreConditionsMet(StaffController staff);
    public abstract System.Type GetExecutorType();

    public virtual float CalculateUtility(StaffController staff)
    {
        float utility = priority * 10f;

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

        if (Managers.PolicyManager.Instance != null)
        {
            utility *= Managers.PolicyManager.Instance.GlobalWorkSpeedMod;
        }

        return utility;
    }

    public virtual string GetDebugInfo(StaffController staff)
    {
        if (staff.IsOnBreak()) return "На перерыве";
        return AreConditionsMet(staff) ? "Условия выполнены" : "Нет условий";
    }
}
