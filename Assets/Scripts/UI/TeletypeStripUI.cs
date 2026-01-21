using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;
using Managers;

namespace UI
{
    public class TeletypeStripUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Transform messagesContainer;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject messagePrefab;

        [Header("Settings")]
        [SerializeField] private int maxVisibleMessages = 3;

        private List<MessageItem> activeMessages = new List<MessageItem>();

        private class MessageItem
        {
            public GameObject go;
            public TeletypeManager.TeletypeMessageType type;
        }

        private void Awake()
        {
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.AddListener(OnScroll);
            }
        }

        public void AddMessage(TeletypeManager.TeletypeMessage msg)
        {
            if (messagePrefab == null || messagesContainer == null) return;

            GameObject go = Instantiate(messagePrefab, messagesContainer);
            var textComponent = go.GetComponent<TextMeshProUGUI>();
            
            if (textComponent != null)
            {
                string periodPrefix = GetPeriodPrefix();
                string typeColor = GetTypeColor(msg.type);
                textComponent.text = $"<color={typeColor}>[{periodPrefix}]</color> {msg.text}";
            }

            activeMessages.Add(new MessageItem { go = go, type = msg.type });

            // Анимация появления
            go.transform.localScale = new Vector3(0.8f, 0f, 0.8f);
            go.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

            // Управляем видимыми сообщениями
            while (activeMessages.Count > maxVisibleMessages + 5)
            {
                var oldest = activeMessages[0];
                if (oldest.go != null)
                {
                    oldest.go.transform.DOScale(0f, 0.2f).OnComplete(() => Destroy(oldest.go));
                }
                activeMessages.RemoveAt(0);
            }
        }

        public void ResetScroll()
        {
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void OnScroll(Vector2 pos)
        {
            // Можно добавить логику для показа/скрытия кнопок навигации
        }

        private string GetPeriodPrefix()
        {
            if (TimeManager.Instance != null)
            {
                var period = TimeManager.Instance.GetCurrentPeriodType();
                if ((period & Data.Calendar.CalendarDayPeriodType.Morning) != 0) return "УТРО";
                if ((period & Data.Calendar.CalendarDayPeriodType.Day) != 0) return "ДЕНЬ";
                if ((period & Data.Calendar.CalendarDayPeriodType.LateDay) != 0) return "ВЕЧЕР";
                if ((period & Data.Calendar.CalendarDayPeriodType.StartNight) != 0) return "НОЧЬ";
                if ((period & Data.Calendar.CalendarDayPeriodType.EndNight) != 0) return "НОЧЬ";
            }
            return "ДЕНЬ";
        }

        private string GetTypeColor(TeletypeManager.TeletypeMessageType type)
        {
            switch (type)
            {
                case TeletypeManager.TeletypeMessageType.Warning: return "#FFAA00";
                case TeletypeManager.TeletypeMessageType.Success: return "#44FF44";
                case TeletypeManager.TeletypeMessageType.Important: return "#FF4444";
                case TeletypeManager.TeletypeMessageType.Policy: return "#AA44FF";
                default: return "#FFFFFF";
            }
        }
    }
}
