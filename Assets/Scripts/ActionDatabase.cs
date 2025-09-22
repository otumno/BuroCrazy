// Файл: ActionDatabase.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ActionDatabase", menuName = "Bureau/Action Database")]
public class ActionDatabase : ScriptableObject
{
    public List<StaffAction> allActions;
}