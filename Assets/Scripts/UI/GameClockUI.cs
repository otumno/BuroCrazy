using Managers;
using TMPro;
using UnityEngine;

namespace UI
{
    public class GameClockUI : MonoBehaviour
    {
        [Tooltip("Ссылка на компонент TextMeshPro для отображения времени")]
        public TextMeshProUGUI timeDisplay;

        void Update()
        {
            // Проверки на наличие ссылок
            if (timeDisplay == null || DayPeriodManager.Instance == null) return;

            // Получаем настройки текущего периода
            var currentPeriodPlan = DayPeriodManager.Instance.CurrentPeriodConfig;
            
            // Если игра еще не инициализировалась, выходим
            if (currentPeriodPlan == null) return;

            // Получаем данные о времени
            float duration = currentPeriodPlan.durationInSeconds;
            float timer = DayPeriodManager.Instance.PeriodTimer;
            
            // Вычисляем оставшееся время
            float timeLeft = Mathf.Max(0, duration - timer);

            // Форматируем в ММ:СС
            string formattedTime = string.Format("{0:00}:{1:00}", 
                Mathf.FloorToInt(timeLeft / 60), 
                Mathf.FloorToInt(timeLeft % 60));

            timeDisplay.text = formattedTime;
        }
    }
}