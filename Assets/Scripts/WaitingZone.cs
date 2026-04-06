// Файл WaitingZone.cs
using UnityEngine;
using System.Collections.Generic;

public class WaitingZone : MonoBehaviour
{
    [Header("Точки для ожидания стоя")]
    public List<Transform> standingPoints;

    [Header("Точки для сидения (стулья)")]
    public List<Transform> seatPoints;

    // Метод для получения случайной точки для стояния
    public Transform GetRandomStandingPoint()
    {
        Debug.Log($"[WaitingZone.GetRandomStandingPoint] zone={name}, standingPoints={(standingPoints == null ? "NULL" : standingPoints.Count.ToString())}");
        if (standingPoints == null || standingPoints.Count == 0) {
            Debug.LogWarning($"[WaitingZone.GetRandomStandingPoint] zone={name}: standingPoints пустой! Возвращаю transform (это может быть ошибкой!)");
            return transform;
        }
        return standingPoints[Random.Range(0, standingPoints.Count)];
    }
}