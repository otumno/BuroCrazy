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
}