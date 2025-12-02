using UnityEngine;
using TMPro;
using Managers;
using Data.Calendar;

public class GameClockUI : MonoBehaviour
{
    public TextMeshProUGUI timeText;

    void Update()
    {
        if (TimeManager.Instance == null || timeText == null) return;

        // Получаем данные из TimeManager
        var settings = TimeManager.Instance.GetCurrentPeriodSettings();
        float timer = TimeManager.Instance.GetPeriodTimer();
        
        if (settings != null)
        {
            float duration = settings.durationInSeconds;
            // Считаем обратный отсчет
            float timeLeft = Mathf.Max(0, duration - timer);
            
            // Форматируем время (минуты:секунды)
            // Например: 01:30
            string formattedTime = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(timeLeft / 60), Mathf.FloorToInt(timeLeft % 60));
            
            // --- ИСПРАВЛЕНИЕ: Убрали "({settings.PeriodType})" ---
            timeText.text = formattedTime;
        }
    }
}