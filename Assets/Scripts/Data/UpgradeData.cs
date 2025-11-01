// Файл: Assets/Scripts/Data/Upgrades/UpgradeData.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Upgrade_", menuName = "Bureau/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [Header("Основная информация")]
    public string upgradeName = "Новый апгрейд"; // Отображаемое имя
    [TextArea(3, 5)]
    public string description = "Описание эффекта апгрейда."; // Описание для UI
    public int cost = 100; // Стоимость покупки

    [Header("Визуальное представление в UI")]
    public Sprite iconGrayscale; // Иконка для некупленного/заблокированного
    public Sprite iconColor;     // Иконка для доступного/купленного

    [Header("Эффект: Активация Объектов")]
    [Tooltip("Список ТОЧНЫХ имен GameObject'ов на сцене, которые нужно активировать при покупке этого апгрейда.")]
    public List<string> objectsToActivate;
	
	[Tooltip("Список ТОЧНЫХ имен GameObject'ов на сцене, которые нужно СКРЫТЬ при покупке этого апгрейда.")]
    public List<string> objectsToDeactivate;

    [Header("Требования (Опционально)")]
    [Tooltip("Список других апгрейдов (UpgradeData ассетов), которые должны быть куплены перед этим.")]
    public List<UpgradeData> requirements;

    // Сюда позже можно будет добавить поля для других эффектов:
    // public float speedMultiplier = 1f;
    // public float stressReduction = 0f;
    // public StaffAction actionToUnlock;
    // public int influenceBonus = 0;
}