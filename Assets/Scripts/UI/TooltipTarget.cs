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
            if (!enabled || string.IsNullOrEmpty(tooltipText)) return;
            isHovered = true;
            TooltipManager.Instance?.RegisterHover(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            TooltipManager.Instance?.UnregisterHover(this);
        }

        public bool IsHovered() => isHovered;
    }
}