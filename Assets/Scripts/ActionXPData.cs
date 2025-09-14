// Файл: ActionXPData.cs - ОБНОВЛЕННАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ActionXPEntry
{
    // Заменяем string на наш новый enum
    public ActionType actionType;
    public int xpGranted;
}

[CreateAssetMenu(fileName = "ActionXP_Database", menuName = "Bureau/Action XP Configuration")]
public class ActionXPData : ScriptableObject
{
    public List<ActionXPEntry> xpEntries;

    // Метод теперь тоже принимает enum для 100% надежности
    public int GetXpForAction(ActionType type)
    {
        var entry = xpEntries.FirstOrDefault(e => e.actionType == type);
        if (entry != null)
        {
            return entry.xpGranted;
        }
        Debug.LogWarning($"Не найдена запись об опыте для действия: {type}");
        return 0;
    }
}