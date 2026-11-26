// Файл: CharacterSkills.cs
using UnityEngine;
using System.Collections.Generic;
using Utilities;

[CreateAssetMenu(fileName = "CharacterSkills", menuName = "My Game/Character Skills")]
public class CharacterSkills : ScriptableObject
{
    [Range(0.0f, 1.0f)]
    public float paperworkMastery;
    [Range(0.0f, 1.0f)]
    public float sedentaryResilience;
    [Range(0.0f, 1.0f)]
    public float pedantry;
    [Range(0.0f, 1.0f)]
    public float softSkills;
    [Tooltip("Скрытый параметр, влияет на коррупцию")]
    [Range(0.0f, 1.0f)]
    public float corruption;
    [Tooltip("Грязные руки — параметр бухгалтера. Больше = чаще находит деньги и эффективнее покрывает схемы.")]
    [Range(0.0f, 1.0f)]
    public float dirtyHands;

    public float GetSkillValue(SkillType type)
    {
        switch (type)
        {
            case SkillType.PaperworkMastery: return paperworkMastery;
            case SkillType.SedentaryResilience: return sedentaryResilience;
            case SkillType.Pedantry: return pedantry;
            case SkillType.SoftSkills: return softSkills;
            case SkillType.Corruption: return corruption;
            case SkillType.DirtyHands: return dirtyHands;
            default: return 0f;
        }
    }

    public string GetSkillShortText(SkillType type) => FormatSkill(type, GetSkillValue(type));

    public static string FormatSkill(SkillType type, float value) =>
        $"{GetSkillString(type)}: {Mathf.RoundToInt(value * 100)}%";

    public static string GetSkillString(SkillType type) =>
        type switch
        {
            SkillType.PaperworkMastery => UI.GameText.Get("staff.skill.PaperworkMastery"),
            SkillType.SedentaryResilience => UI.GameText.Get("staff.skill.SedentaryResilience"),
            SkillType.Pedantry => UI.GameText.Get("staff.skill.Pedantry"),
            SkillType.SoftSkills => UI.GameText.Get("staff.skill.SoftSkills"),
            SkillType.Corruption => UI.GameText.Get("staff.skill.Corruption"),
            SkillType.DirtyHands => UI.GameText.Get("staff.skill.DirtyHands"),
            _ => ""
        };
}