// Файл: ZoneStatusUI.cs
using UnityEngine;
using TMPro;

public class ZoneStatusUI : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Зона, состояние которой мы отслеживаем")]
    public LimitedCapacityZone monitoredZone;
    [Tooltip("Текстовый элемент для вывода статуса")]
    public TextMeshProUGUI statusText;

    void Update()
    {
        if (monitoredZone == null || statusText == null)
        {
            return;
        }

        // --- ИЗМЕНЕНИЕ: Получаем текущие и максимальные значения ---
        int currentOccupancy = monitoredZone.GetCurrentOccupancy();
        int capacity = monitoredZone.capacity;

        if (currentOccupancy >= capacity)
        {
            // Формируем строку для занятого состояния
            statusText.text = $"{currentOccupancy}/{capacity} ЗАНЯТО";
            statusText.color = Color.red;
        }
        else
        {
            // Формируем строку для свободного состояния
            statusText.text = $"{currentOccupancy}/{capacity} СВОБОДНО";
            statusText.color = Color.green;
        }
    }
}