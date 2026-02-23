using System.Collections;
using UnityEngine;
using TMPro;

namespace UI
{
    public class RadioMusicNotification : MonoBehaviour
    {
        public static RadioMusicNotification Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI musicText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 3f;

        private Coroutine hideCoroutine;
        private string currentMessage = "";

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (canvasGroup != null)
                canvasGroup.alpha = 0;
        }

        public void ShowMusicNotification(string trackName)
        {
            if (musicText == null) return;

            if (hideCoroutine != null)
                StopCoroutine(hideCoroutine);

            currentMessage = trackName.Replace('_', ' ');
            musicText.text = $"♪ {currentMessage}";

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1;
            }
            else
            {
                gameObject.SetActive(true);
            }

            hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        public void ClearNotification()
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0;
            else
                gameObject.SetActive(false);
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displayDuration);
            ClearNotification();
        }
    }
}
