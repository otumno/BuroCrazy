// Файл: RoleData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "RoleData_New", menuName = "Bureau/Role Data")]
public class RoleData : ScriptableObject
{
    [Header("Идентификация")]
    public StaffController.Role roleType;

    [Header("Параметры Agent Mover")]
    public float moveSpeed = 3f;
    public int priority = 1;

    [Header("Внешний вид")]
    [Tooltip("Набор спрайтов (униформа) для этой роли")]
    public EmotionSpriteCollection spriteCollection;
    [Tooltip("Карта состояний и эмоций для этой роли")]
    public StateEmotionMap stateEmotionMap;
    [Tooltip("Префаб аксессуара для этой роли (необязательно)")]
    public GameObject accessoryPrefab;
    
    // Сюда можно добавлять любые другие параметры, специфичные для роли,
    // например, chaseSpeedMultiplier для охранника.
}