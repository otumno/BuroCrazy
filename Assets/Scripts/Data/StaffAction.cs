// Файл: StaffAction.cs
using UnityEngine;
using System.Collections.Generic;

// Этот атрибут позволяет нам создавать "ассеты" этого типа прямо в Unity
[CreateAssetMenu(fileName = "NewStaffAction", menuName = "Bureau/Staff Action")]
public class StaffAction : ScriptableObject
{
    [Header("Основная информация")]
    public ActionType actionType; // Связь с существующей логикой
    public string displayName;    // Имя для UI
    [TextArea(2, 4)]
    public string description;    // Описание для UI

    [Header("Требования и тип")]
    public int minRankRequired = 0; // Минимальный ранг для доступа
    public List<StaffController.Role> applicableRoles; // Для каких ролей доступно
    public bool isUnique = false; // Это уникальный перк?
}