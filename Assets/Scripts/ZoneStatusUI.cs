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

        int currentOccupancy = monitoredZone.GetCurrentOccupancy();
        int capacity = monitoredZone.capacity;

        if (currentOccupancy >= capacity)
        {
            statusText.text = $"{currentOccupancy}/{capacity} ЗАНЯТО";
            statusText.color = Color.red;
        }
        else
        {
            statusText.text = $"{currentOccupancy}/{capacity} СВОБОДНО";
            statusText.color = Color.green;
        }
    }
}