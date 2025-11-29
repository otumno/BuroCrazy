using Managers;
using TMPro;
using UnityEngine;

namespace UI
{
    public class DayCounterUI : MonoBehaviour
    {
        [Tooltip("Ссылка на текстовое поле дня")]
        public TextMeshProUGUI dayCounterText;

        // Оптимизация: храним последнее значение, чтобы не перерисовывать текст каждый кадр
        private int _lastDay = -1;

        void Update()
        {
            if (dayCounterText == null || ClientSpawner.Instance == null) return;

            int currentDay = ClientSpawner.Instance.GetCurrentDay();

            // Обновляем текст только если день изменился
            if (currentDay != _lastDay)
            {
                // Используем Mathf.Max(1, ...), чтобы не показывать 0-й день (технический)
                dayCounterText.text = $"ДЕНЬ: {Mathf.Max(1, currentDay)}";
                _lastDay = currentDay;
            }
        }
    }
}