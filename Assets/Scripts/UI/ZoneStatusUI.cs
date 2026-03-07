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

    // --- НОВЫЕ ПОЛЯ ДЛЯ JUICY ЭФФЕКТА ---
    private string lastStatusString = "";
    private SmartRoomLabel smartLabel;

    private void Start()
    {
        smartLabel = GetComponentInParent<SmartRoomLabel>();
    }

    void Update()
    {
        if (monitoredZone == null || statusText == null)
        {
            return;
        }

        int currentOccupancy = monitoredZone.GetCurrentOccupancy();
        int capacity = monitoredZone.capacity;

        string newStatus = "";
        Color newColor = Color.white;

        if (currentOccupancy >= capacity)
        {
            newStatus = $"{currentOccupancy}/{capacity} ЗАНЯТО";
            newColor = Color.red;
        }
        else
        {
            newStatus = $"{currentOccupancy}/{capacity} СВОБОДНО";
            newColor = Color.green;
        }

        // --- ЛОГИКА "ПИНГА" ПРИ ИЗМЕНЕНИИ ---
        if (newStatus != lastStatusString)
        {
            // Не пингуем при самой первой инициализации
            if (!string.IsNullOrEmpty(lastStatusString) && smartLabel != null)
            {
                smartLabel.Ping(2.0f); // Для очередей можно показывать чуть меньше (2 сек)
            }
            
            lastStatusString = newStatus;
            statusText.text = newStatus;
            statusText.color = newColor;
        }
    }
}