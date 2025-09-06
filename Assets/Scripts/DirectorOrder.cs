// Файл: DirectorOrder.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "DirectorOrder", menuName = "My Game/Director Order")]
public class DirectorOrder : ScriptableObject
{
    public string orderName;
    [TextArea(3, 5)]
    public string orderDescription;
    public Sprite icon;

    // TODO: Здесь будут храниться данные об эффектах
    // Например:
    // public float moveSpeedMultiplier = 1f;
    // public float stressGainMultiplier = 1f;
    // ...
}