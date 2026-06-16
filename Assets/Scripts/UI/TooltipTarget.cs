using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Tooltips
{
    /// <summary>
    /// Компонент для объектов, которые могут показывать тултип при наведении мыши.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Текст подсказки")]
        public string tooltipText = "Интерактивный объект";

        [Tooltip("Приоритет (чем выше, тем важнее)")]
        public int priority = 0;

        [Tooltip("Задержка перед показом тултипа (сек)")]
        public float displayDelay = 0.5f;

        private bool isHovered = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }

        // Резервный путь: работает и без EventSystem / Physics2DRaycaster —
        // OnMouseEnter вызывается самим Unity по любому Collider2D на объекте.
        private void OnMouseEnter()
        {
            SetHovered(true);
        }

        private void OnMouseExit()
        {
            SetHovered(false);
        }

        private void SetHovered(bool value)
        {
            if (value == isHovered) return;
            isHovered = value;
            if (value)
            {
                TooltipManager.Instance?.RegisterHover(this);
            }
            else
            {
                TooltipManager.Instance?.UnregisterHover(this);
            }
        }

        public bool IsHovered() => isHovered;
    }
}
