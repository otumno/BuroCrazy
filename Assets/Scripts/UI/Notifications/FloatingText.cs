using UnityEngine;
using TMPro;
using System.Collections;

namespace UI.Notifications
{
    public class FloatingText : MonoBehaviour
    {
        [Header("UI элементы")]
        public TextMeshProUGUI textComponent;
        public RectTransform rectTransform;

        [Header("Настройки движения")]
        public Vector3 moveDirection = Vector3.up;
        public float moveSpeed = 50f;
        public float fadeSpeed = 1f;

        private Color textColor;
        private float duration;
        private float elapsed = 0f;

        public void Setup(string text, Color color, float duration)
        {
            if (textComponent != null)
            {
                textComponent.text = text;
                textComponent.color = color;
            }
            textColor = color;
            this.duration = duration;

            rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // Движение вверх
            if (rectTransform != null)
            {
                rectTransform.position += moveDirection * moveSpeed * Time.deltaTime;
            }

            // Затухание
            float alpha = 1f - (elapsed / duration);
            if (textComponent != null)
            {
                Color c = textColor;
                c.a = alpha;
                textComponent.color = c;
            }

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
