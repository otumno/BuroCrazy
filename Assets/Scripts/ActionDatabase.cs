using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ActionDatabase", menuName = "Bureau/Action Database")]
public class ActionDatabase : ScriptableObject
{
    public List<StaffAction> allActions;

    // --- ДОБАВЛЯЕМ ЭТОТ МЕТОД ---
    public StaffAction GetActionByType(ActionType type)
    {
        if (allActions == null) return null;
        return allActions.Find(a => a.actionType == type);
    }
}