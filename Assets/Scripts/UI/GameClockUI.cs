using Managers;
using TMPro;
using UnityEngine;

namespace UI
{
    public class GameClockUI : MonoBehaviour
    {
        [Tooltip("Ссылка на текстовое поле времени")]
        public TextMeshProUGUI timeDisplay;

        void Update()
        {
            if (timeDisplay == null || ClientSpawner.Instance == null) return;

            // Получаем текущий план периода
            var currentPeriodPlan = ClientSpawner.Instance.GetCurrentPeriodPlan();
            if (currentPeriodPlan == null) return;

            // Считаем время
            float duration = currentPeriodPlan.durationInSeconds;
            float timer = ClientSpawner.Instance.GetPeriodTimer();
            
            // Вычисляем остаток
            float timeLeft = Mathf.Max(0, duration - timer);

            // Форматируем
            string formattedTime = string.Format("{0:00}:{1:00}", 
                Mathf.FloorToInt(timeLeft / 60), 
                Mathf.FloorToInt(timeLeft % 60));

            timeDisplay.text = formattedTime;
        }
    }
}