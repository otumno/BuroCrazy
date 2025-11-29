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
        private int _dayIndex = -1;

        private void Update()
        {
            if (dayCounterText == null || ClientSpawner.Instance == null) return;

            var currentDay = ClientSpawner.Instance.GetCurrentDay();
            if (currentDay == _dayIndex)
                return;
            
            _dayIndex = currentDay;
            
            // todo: localization (alias + args)
            dayCounterText.text = $"ДЕНЬ: {currentDay + 1}";
        }
    }
}