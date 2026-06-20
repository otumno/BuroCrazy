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

    public string GetSkillShortText(SkillType type)
    {
        var value = GetSkillValue(type);
        var translation = GetSkillString(type);
        return $"{translation} : {(value * 100).ToString("0")}%"; // пишем только целую часть числа
    }
    
    public static string GetSkillString(SkillType type) =>
        type switch
        {
            SkillType.PaperworkMastery => "Мастерство бумажной работы",
            SkillType.SedentaryResilience => "Устойчивость к малоподвижному образу жизни",
            SkillType.Pedantry => "Педантичность",
            SkillType.SoftSkills => "Умение общаться",
            SkillType.Corruption => "Коррумпированность",
            SkillType.DirtyHands => "Грязные руки",
            _ => ""
        };
}