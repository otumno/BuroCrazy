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

        if (monitoredZone.GetCurrentOccupancy() >= monitoredZone.capacity)
        {
            statusText.text = "ЗАНЯТО";
            statusText.color = Color.red;
        }
        else
        {
            statusText.text = "СВОБОДНО";
            statusText.color = Color.green;
        }
    }
}