using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace UI.Notifications
{
    public class NotificationUI : MonoBehaviour
    {
        [Header("UI элементы")]
        public Image backgroundImage;
        public TextMeshProUGUI messageText;
        public Image iconImage;

        [Header("Настройки цвета")]
        public Color successColor = Color.green;
        public Color failureColor = Color.red;
        public Color warningColor = Color.yellow;
        public Color priorityColor = new Color(1f, 0.5f, 0f);
        public Color importantDocumentColor = new Color(0.5f, 0.5f, 1f);
        public Color stressColor = new Color(0.6f, 0.2f, 0.6f);

        private NotificationType currentType;
        private float duration;
        private Coroutine fadeCoroutine;

        public void Setup(NotificationType type, string message, float duration)
        {
            currentType = type;
            this.duration = duration;

            if (messageText != null)
            {
                messageText.text = message;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = GetColorForType(type);
            }

            if (iconImage != null)
            {
                iconImage.sprite = GetIconForType(type);
            }

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeRoutine());
        }

        private Color GetColorForType(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Success: return successColor;
                case NotificationType.Failure: return failureColor;
                case NotificationType.Warning: return warningColor;
                case NotificationType.Priority: return priorityColor;
                case NotificationType.ImportantDocument: return importantDocumentColor;
                case NotificationType.Stress: return stressColor;
                default: return Color.white;
            }
        }

        private Sprite GetIconForType(NotificationType type)
        {
            // Здесь можно добавить загрузку спрайтов иконок
            return null;
        }

        private IEnumerator FadeRoutine()
        {
            // Fade in
            float elapsed = 0f;
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            while (elapsed < NotificationManager.Instance.fadeInTime)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = elapsed / NotificationManager.Instance.fadeInTime;
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // Wait
            yield return new WaitForSeconds(duration);

            // Fade out
            elapsed = 0f;
            while (elapsed < NotificationManager.Instance.fadeOutTime)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - (elapsed / NotificationManager.Instance.fadeOutTime);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
        }
    }
}
