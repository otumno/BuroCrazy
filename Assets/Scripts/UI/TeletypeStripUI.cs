using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
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
        public Transform messagesContainer;
        public ScrollRect scrollRect;
        public GameObject messagePrefab;

        [Header("Typewriter Settings")]
        public float charactersPerSecond = 30f;

        [Header("Settings")]
        public int maxVisibleMessages = 3;

        private List<MessageItem> activeMessages = new List<MessageItem>();
        private Coroutine typeRoutine;

        private class MessageItem
        {
            public GameObject go;
            public TextMeshProUGUI textComponent;
            public string fullText;
            public int typeIndex;
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
                string timestamp = msg.Timestamp.ToString("HH:mm");

                string fullText = $"<color={typeColor}>[{timestamp}] [{periodPrefix}]</color> {msg.Text}";

                var item = new MessageItem
                {
                    go = go,
                    textComponent = textComponent,
                    fullText = fullText,
                    typeIndex = 0,
                    type = msg.Type
                };

                textComponent.text = "";
                activeMessages.Add(item);

                if (typeRoutine == null)
                {
                    typeRoutine = StartCoroutine(TypewriterRoutine());
                }
            }

            ManageVisibleMessages();
        }

        private IEnumerator TypewriterRoutine()
        {
            while (activeMessages.Count > 0)
            {
                bool anyTyping = false;

                for (int i = activeMessages.Count - 1; i >= 0; i--)
                {
                    var item = activeMessages[i];

                    if (item.typeIndex < item.fullText.Length)
                    {
                        anyTyping = true;
                        item.typeIndex++;
                        item.textComponent.text = item.fullText.Substring(0, item.typeIndex);

                        float delay = 1f / Mathf.Clamp(charactersPerSecond, 1f, 100f);
                        yield return new WaitForSeconds(delay);
                    }
                    else if (item.go == null)
                    {
                        activeMessages.RemoveAt(i);
                    }
                }

                if (!anyTyping)
                {
                    typeRoutine = null;
                    yield break;
                }
            }

            typeRoutine = null;
        }

        private void ManageVisibleMessages()
        {
            while (activeMessages.Count > maxVisibleMessages + 5)
            {
                var oldest = activeMessages[0];
                if (oldest.go != null)
                {
                    oldest.go.transform.DOScale(0f, 0.2f).OnComplete(() =>
                    {
                        if (oldest.go != null) Destroy(oldest.go);
                    });
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

        public void SetTypingSpeed(float cps)
        {
            charactersPerSecond = Mathf.Clamp(cps, 1f, 100f);
        }

        public void ClearAll()
        {
            if (typeRoutine != null)
            {
                StopCoroutine(typeRoutine);
                typeRoutine = null;
            }

            foreach (var item in activeMessages)
            {
                if (item.go != null) Destroy(item.go);
            }
            activeMessages.Clear();
        }

        private void OnScroll(Vector2 pos)
        {
        }
    }
}
