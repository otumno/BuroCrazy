// Файл: CharacterSkills.cs
using UnityEngine;
using System.Collections.Generic;

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
	public float GetSkillValue(SkillType type)
{
    switch (type)
    {
        case SkillType.PaperworkMastery: return this.paperworkMastery;
        case SkillType.SedentaryResilience: return this.sedentaryResilience;
        case SkillType.Pedantry: return this.pedantry;
        case SkillType.SoftSkills: return this.softSkills;
        case SkillType.Corruption: return this.corruption;
        default: return 0f;
    }
}
}