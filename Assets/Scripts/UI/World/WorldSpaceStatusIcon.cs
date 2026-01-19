// Assets/Scripts/UI/World/WorldSpaceStatusIcon.cs
using UnityEngine;

namespace UI.World
{
    /// <summary>
    /// Управляет иконкой статуса над объектом в мировом пространстве.
    /// </summary>
    public class WorldSpaceStatusIcon : MonoBehaviour
    {
        [Header("Настройки")]
        [Tooltip("Спрайт иконки (Гаечный ключ, Огонь и т.д.)")]
        public SpriteRenderer iconRenderer;
        
        [Tooltip("Анимация покачивания")]
        public bool animateFloat = true;
        public float floatSpeed = 2f;
        public float floatAmplitude = 0.1f;

        private Vector3 initialLocalPos;

        private void Awake()
        {
            if (iconRenderer == null) iconRenderer = GetComponent<SpriteRenderer>();
            if (iconRenderer != null)
            {
                initialLocalPos = iconRenderer.transform.localPosition;
                iconRenderer.enabled = false; // Скрываем по умолчанию
            }
        }

        private void Update()
        {
            if (iconRenderer != null && iconRenderer.enabled && animateFloat)
            {
                // Не используем Time.time, чтобы анимация замирала на паузе (Time.timeScale = 0)
                // Но для UI иногда хотят анимацию и на паузе. 
                // Если хочешь анимацию на паузе - используй Time.unscaledTime.
                // В данном случае используем обычное время, так как это часть игрового мира.
                float newY = initialLocalPos.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
                iconRenderer.transform.localPosition = new Vector3(initialLocalPos.x, newY, initialLocalPos.z);
            }
        }

        public void SetIconState(bool isVisible, Sprite icon = null, Color? color = null)
        {
            if (iconRenderer == null) return;

            iconRenderer.enabled = isVisible;
            
            if (isVisible)
            {
                if (icon != null) iconRenderer.sprite = icon;
                if (color.HasValue) iconRenderer.color = color.Value;
            }
        }
    }
}