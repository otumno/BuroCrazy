using Managers;
using TMPro;
using UnityEngine;

namespace UI
{
    public class GameClockUI : MonoBehaviour
    {
        [Tooltip("Ссылка на текстовое поле времени")]
        public TextMeshProUGUI timeDisplay;

        private float _nextRefreshTime;
        
        private const float REFRESH_COOLDOWN = 0.5f;

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextRefreshTime)
                return;
            
            _nextRefreshTime = Time.realtimeSinceStartup + REFRESH_COOLDOWN;
            if (timeDisplay == null || ClientSpawner.Instance == null)
                return;

            // Получаем текущий план периода
            var currentPeriodPlan = ClientSpawner.Instance.GetCurrentPeriodPlan();
            if (currentPeriodPlan == null)
                return;

            // Считаем время
            float duration = currentPeriodPlan.durationInSeconds;
            float timer = ClientSpawner.Instance.GetPeriodTimer();
            
            // Вычисляем остаток
            float timeLeft = Mathf.Max(0, duration - timer);

            // Форматируем
            var seconds = Mathf.FloorToInt(timeLeft % 60);
            var minutes = Mathf.FloorToInt(timeLeft / 60);
            var formattedTime = $"{minutes}:{seconds}";

            if (formattedTime != timeDisplay.text)
                timeDisplay.text = formattedTime;
        }
    }
}