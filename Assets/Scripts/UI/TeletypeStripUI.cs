using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Data.Calendar;
using DG.Tweening;
using Managers;
using Managers.Teletype;

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
            public TeletypeMessageType type;
        }

        private void Awake()
        {
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.AddListener(OnScroll);
            }
        }

        public void AddMessage(TeletypeMessage msg)
        {
            if (messagePrefab == null || messagesContainer == null) return;

            GameObject go = Instantiate(messagePrefab, messagesContainer);
            var textComponent = go.GetComponent<TextMeshProUGUI>();
            
            if (textComponent != null)
            {
                var period = TimeManager.Instance.GetCurrentPeriodType();
                string periodPrefix = period.GetLocalization();
                string typeColor = msg.Type.GetColor();
                textComponent.text = $"<color={typeColor}>[{periodPrefix}]</color> {msg.Text}";
            }

            activeMessages.Add(new MessageItem { go = go, type = msg.Type });

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
    }
}
