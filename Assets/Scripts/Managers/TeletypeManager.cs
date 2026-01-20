// Assets/Scripts/Managers/TeletypeManager.cs
using UnityEngine;
using System.Collections.Generic;
using UI;
using Data.Calendar; // Для получения текущего периода

namespace Managers
{
    public class TeletypeManager : MonoBehaviour
    {
        public static TeletypeManager Instance { get; private set; }

        [Header("UI Ссылки")]
        [Tooltip("Контейнер (Vertical Layout Group), куда падают ленты")]
        public Transform stripsContainer;
        [Tooltip("Префаб полоски")]
        public GameObject stripPrefab;

        [Header("Настройки")]
        public int maxStrips = 3;
        public int maxChars = 60; // Ограничение длины

        private List<TeletypeStripUI> activeStrips = new List<TeletypeStripUI>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Отправляет сообщение в ленту.
        /// </summary>
        public void Log(string message)
        {
            if (stripsContainer == null || stripPrefab == null) return;

            // 1. Обрезаем текст, если слишком длинный
            if (message.Length > maxChars)
            {
                message = message.Substring(0, maxChars - 3) + "...";
            }

            // 2. Определяем префикс времени
            string prefix = "ДЕНЬ";
            if (TimeManager.Instance != null)
            {
                var period = TimeManager.Instance.GetCurrentPeriodType();
                prefix = GetPeriodShortName(period);
            }

            // 3. Удаляем лишние, если лимит превышен
            if (activeStrips.Count >= maxStrips)
            {
                // Удаляем самый старый (первый в списке, если layout сверху-вниз, или наоборот)
                // Обычно в VerticalLayout новый добавляется в конец. Значит удаляем нулевой.
                var oldStrip = activeStrips[0];
                activeStrips.RemoveAt(0);
                oldStrip.RemoveStrip();
            }

            // 4. Создаем новый
            GameObject go = Instantiate(stripPrefab, stripsContainer);
            TeletypeStripUI stripUI = go.GetComponent<TeletypeStripUI>();
            
            // Если LayoutGroup сортирует сверху вниз, новый элемент появится снизу.
            // Если хотим, чтобы новые толкали старые вверх (как чат), нужно чтобы VerticalLayout был Bottom-to-Top
            // Или просто добавлять через transform.SetAsLastSibling();
            
            if (stripUI != null)
            {
                stripUI.Setup(message, prefix);
                activeStrips.Add(stripUI);
            }
        }

        private string GetPeriodShortName(CalendarDayPeriodType p)
        {
            if ((p & CalendarDayPeriodType.Morning) != 0) return "УТРО";
            if ((p & CalendarDayPeriodType.Evening) != 0) return "ВЕЧЕР";
            if ((p & CalendarDayPeriodTypeExtensions.FullNight) != 0) return "НОЧЬ";
            return "ДЕНЬ";
        }
    }
}