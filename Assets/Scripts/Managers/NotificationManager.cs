using UnityEngine;
using TMPro;

namespace UI.Notifications
{
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }

        [Header("Префабы")]
        public GameObject notificationPrefab;
        public GameObject floatingTextPrefab;

        [Header("Настройки")]
        public float defaultDuration = 3f;
        public float fadeInTime = 0.3f;
        public float fadeOutTime = 0.3f;

        [Header("UI Canvas")]
        public Canvas mainCanvas;

        private Transform notificationContainer;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (mainCanvas == null)
            {
                mainCanvas = FindFirstObjectByType<Canvas>();
            }

            if (mainCanvas != null)
            {
                var containerObj = new GameObject("NotificationContainer");
                containerObj.transform.SetParent(mainCanvas.transform, false);
                notificationContainer = containerObj.transform;
            }
        }

        public void ShowNotification(GameObject target, NotificationType type, string message, float duration = 0)
        {
            if (target == null) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(target.transform.position);

            ShowNotificationAtPosition(screenPos, type, message, duration);
        }

        public void ShowNotificationAtPosition(Vector3 position, NotificationType type, string message, float duration = 0)
        {
            if (notificationPrefab == null || notificationContainer == null) return;

            var notificationObj = Instantiate(notificationPrefab, notificationContainer);
            notificationObj.transform.position = position;

            var notificationUI = notificationObj.GetComponent<NotificationUI>();
            if (notificationUI != null)
            {
                notificationUI.Setup(type, message, duration > 0 ? duration : defaultDuration);
            }
        }

        public void ShowFloatingText(Vector3 worldPosition, string text, Color color, float duration = 2f)
        {
            if (floatingTextPrefab == null || mainCanvas == null) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
            var textObj = Instantiate(floatingTextPrefab, mainCanvas.transform);
            textObj.transform.position = screenPos;

            var floatingText = textObj.GetComponent<FloatingText>();
            if (floatingText != null)
            {
                floatingText.Setup(text, color, duration);
            }
        }

        public void ShowPriorityMessage(GameObject target, string message, float duration, Color color)
        {
            ShowNotification(target, NotificationType.Priority, message, duration);
        }

        public void ShowSuccessMessage(GameObject target, string message)
        {
            ShowNotification(target, NotificationType.Success, message);
        }

        public void ShowFailureMessage(GameObject target, string message)
        {
            ShowNotification(target, NotificationType.Failure, message);
        }

        public void ShowWarningMessage(GameObject target, string message)
        {
            ShowNotification(target, NotificationType.Warning, message);
        }

        public void ShowImportantDocumentNotification(GameObject target, string documentName)
        {
            ShowNotification(target, NotificationType.ImportantDocument, $"Важный документ: {documentName}");
        }

        public void ShowBrokenObjectNotification(GameObject brokenObject)
        {
            ShowNotification(brokenObject, NotificationType.Warning, "Объект сломан!");
        }
    }

    public enum NotificationType
    {
        Success,
        Failure,
        Warning,
        Priority,
        ImportantDocument,
        Stress,
        System
    }
}
