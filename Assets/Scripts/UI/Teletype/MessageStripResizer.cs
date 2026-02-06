//Assets/Scripts/UI/Teletype/MessageStripResizer.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.Teletype
{
    public class MessageStripResizer : MonoBehaviour
    {
        [Header("Parts")]
        public Image imageStart;
        public Image imageTile;
        public Image imageEnd;
        public TextMeshProUGUI messageText;

        [Header("Settings")]
        public float leftMargin = 15f; 
        public float rightMargin = 25f;
        public float minWidth = 100f;

        private RectTransform rectTransform;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            // Убеждаемся, что пивот сообщения всегда слева
            rectTransform.pivot = new Vector2(0, 0.5f);
        }

        public float RefreshLayout()
        {
            if (messageText == null) return minWidth;

            messageText.ForceMeshUpdate();
            Vector2 preferredSize = messageText.GetPreferredValues(messageText.text);
            float textWidth = preferredSize.x;

            float totalWidth = leftMargin + textWidth + rightMargin;
            totalWidth = Mathf.Max(totalWidth, minWidth);

            // Устанавливаем итоговую ширину всей полоски
            rectTransform.sizeDelta = new Vector2(totalWidth, rectTransform.sizeDelta.y);

            PositionParts(totalWidth, textWidth);
            return totalWidth;
        }

        private void PositionParts(float totalWidth, float textWidth)
        {
            if (imageStart == null || imageEnd == null || imageTile == null) return;

            // Берем ширину спрайтов начала и конца
            float startWidth = ((RectTransform)imageStart.transform).sizeDelta.x;
            float endWidth = ((RectTransform)imageEnd.transform).sizeDelta.x;

            // 1. Старт — прижат к левому краю (0)
            RectTransform startRT = (RectTransform)imageStart.transform;
            startRT.anchoredPosition = Vector2.zero;

            // 2. Конец — прижат к правому краю (totalWidth - ширина конца)
            RectTransform endRT = (RectTransform)imageEnd.transform;
            endRT.anchoredPosition = new Vector2(totalWidth - endWidth, 0);

            // 3. Тайл — заполняет всё строго МЕЖДУ ними
            RectTransform tileRT = (RectTransform)imageTile.transform;
            float tileActualWidth = totalWidth - startWidth - endWidth;
            tileRT.anchoredPosition = new Vector2(startWidth, 0);
            tileRT.sizeDelta = new Vector2(Mathf.Max(0, tileActualWidth), tileRT.sizeDelta.y);
            
            // 4. Текст — сдвинут на отступ
            if (messageText != null)
            {
                RectTransform textRT = (RectTransform)messageText.transform;
                textRT.anchoredPosition = new Vector2(leftMargin, 0);
                textRT.sizeDelta = new Vector2(textWidth, textRT.sizeDelta.y);
            }
        }
    }
}