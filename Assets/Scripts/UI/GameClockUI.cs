using UnityEngine;
using TMPro;
using Managers;

public class GameClockUI : MonoBehaviour
{
    public TextMeshProUGUI timeText;

    private float _nextRefreshTime;
    private const float REFRESH_CD = 0.1f;

    private void Update()
    {
        if (TimeManager.Instance == null || timeText == null)
            return;

        var settings = TimeManager.Instance.GetCurrentPeriodSettings();
        if (settings == null)
            return;

        if (Time.time < _nextRefreshTime)
            return;
        
        _nextRefreshTime = Time.time + REFRESH_CD;
        
        var currentTime = TimeManager.Instance.GetPeriodTimer();
        var timeLeft = Mathf.Max(0, settings.durationInSeconds - currentTime);

        var minutes = Mathf.FloorToInt(timeLeft / 60);
        var seconds = Mathf.FloorToInt(timeLeft % 60);
        var formattedTime = $"{minutes:00}:{seconds:00}";
            
        timeText.text = formattedTime;
    }
}