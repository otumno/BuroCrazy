//Assets/Scripts/Managers/Teletype/TeletypeTerminalUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Data.Calendar;
using Managers;
using Managers.Teletype;
using DG.Tweening;

namespace UI.Teletype
{
    public class TeletypeTerminalUI : MonoBehaviour
    {
        [Header("References")]
        public Transform messagesContainer;
        public GameObject messagePrefab;
        public TextMeshProUGUI headerText;

        [Header("Settings")]
        public int maxMessages = 3;
        public float slideDuration = 0.5f;
        public float messageGap = 20f; 
        
        [Header("Visual")]
        [Range(0f, 1f)] public float oldMessageAlpha = 0.5f;
        [Range(0f, 1f)] public float spriteDarkenAmount = 0.3f;

        private List<GameObject> activeMessages = new List<GameObject>();

        private void Awake()
        {
            if (headerText != null) headerText.text = "/// ТЕЛЕТАЙП ///";
        }

        public void AddMessage(TeletypeMessage msg)
        {
            if (messagePrefab == null || messagesContainer == null) return;

            GameObject go = Instantiate(messagePrefab, messagesContainer);
            var resizer = go.GetComponent<MessageStripResizer>();
            var textComp = go.GetComponentInChildren<TextMeshProUGUI>();

            string periodPrefix = TimeManager.Instance.GetCurrentPeriodType().GetLocalization();
            if (textComp != null) {
                textComp.text = $"[{msg.Timestamp:HH:mm}] [{periodPrefix}] {msg.Text}";
                textComp.color = Color.black;
            }

            // Важно: получаем ширину ДО анимации
            float width = resizer != null ? resizer.RefreshLayout() : 100f;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-width - 100f, 0);
            
            activeMessages.Insert(0, go);

            while (activeMessages.Count > maxMessages)
            {
                GameObject oldest = activeMessages[activeMessages.Count - 1];
                activeMessages.RemoveAt(activeMessages.Count - 1);
                
                if (oldest != null)
                {
                    oldest.transform.DOScale(0.5f, slideDuration * 0.5f);
                    CanvasGroup cg = oldest.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.DOFade(0, slideDuration * 0.5f).OnComplete(() => {
                            if (oldest != null) Destroy(oldest);
                        });
                    }
                    else Destroy(oldest);
                }
            }

            RefreshPositions();
        }

        private void RefreshPositions()
        {
            float currentX = 0f;

            // Проходим по списку: 0 - самое новое (слева)
            for (int i = 0; i < activeMessages.Count; i++)
            {
                GameObject msg = activeMessages[i];
                if (msg == null) continue;

                RectTransform rt = msg.GetComponent<RectTransform>();
                float targetX = currentX;

                // Двигаем
                rt.DOAnchorPosX(targetX, slideDuration).SetEase(Ease.OutBack);

                // Визуал
                bool isNew = (i == 0);
                float alpha = isNew ? 1f : oldMessageAlpha;
                float brightness = isNew ? 1f : (1f - spriteDarkenAmount);

                CanvasGroup cg = msg.GetComponent<CanvasGroup>();
                if (cg == null) cg = msg.AddComponent<CanvasGroup>();
                cg.DOFade(alpha, slideDuration);

                foreach (var img in msg.GetComponentsInChildren<Image>()) {
                    img.DOColor(new Color(brightness, brightness, brightness, img.color.a), slideDuration);
                }

                // ОБНОВЛЕНИЕ: currentX увеличивается на ширину ТЕКУЩЕГО сообщения + зазор
                currentX += rt.sizeDelta.x + messageGap;
                
                msg.transform.SetAsLastSibling();
            }
        }

        public void ClearAll()
        {
            foreach (var msg in activeMessages) if (msg != null) Destroy(msg);
            activeMessages.Clear();
        }
    }
}