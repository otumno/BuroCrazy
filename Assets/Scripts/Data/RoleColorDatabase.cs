using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Класс для хранения пары "Роль - Цвет"
[System.Serializable]
public class RoleColorEntry
{
    public StaffController.Role role;
    public Color color = Color.white; // Цвет по умолчанию
}

// ScriptableObject для хранения списка цветов
[CreateAssetMenu(fileName = "RoleColorDatabase", menuName = "Bureau/Role Color Database")]
public class RoleColorDatabase : ScriptableObject
{
    [Tooltip("Список сопоставлений Роли и ее цвета для UI")]
    public List<RoleColorEntry> roleColors;

    /// <summary>
    /// Возвращает цвет для указанной роли.
    /// </summary>
    /// <param name="role">Роль для поиска.</param>
    /// <param name="defaultColor">Цвет, возвращаемый, если роль не найдена.</param>
    /// <returns>Найденный цвет или цвет по умолчанию.</returns>
    public Color GetColorForRole(StaffController.Role role, Color defaultColor)
    {
        if (roleColors == null) return defaultColor; // Проверка на null

        // Ищем запись для нужной роли
        RoleColorEntry entry = roleColors.FirstOrDefault(rc => rc.role == role);

        // Если нашли - возвращаем ее цвет, иначе - цвет по умолчанию
        return entry != null ? entry.color : defaultColor;
    }
}