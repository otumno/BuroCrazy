using Managers;
using TMPro;
using UnityEngine;

namespace UI
{
    public class DayCounterUI : MonoBehaviour
    {
        [Tooltip("Ссылка на компонент TextMeshPro для отображения номера дня")]
        public TextMeshProUGUI dayCounterText;

        // Храним последнее значение, чтобы не обновлять текст каждый кадр (оптимизация)
        private int _lastDay = -1;

        void Update()
        {
            // Проверки на наличие ссылок
            if (dayCounterText == null || CalendarManager.Instance == null) return;

            // Получаем текущий день из календаря
            int currentDay = CalendarManager.Instance.CurrentDay;

            // Если день изменился с прошлого кадра
            if (currentDay != _lastDay)
            {
                // Mathf.Max(1, ...) гарантирует, что мы не покажем "День: 0" во время инициализации
                dayCounterText.text = $"ДЕНЬ: {Mathf.Max(1, currentDay)}";
                
                // Запоминаем текущее значение
                _lastDay = currentDay;
            }
        }
    }
}