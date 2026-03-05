using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace UI.Effects
{
    [RequireComponent(typeof(Selectable))]
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Настройки анимации (Scale)")]
        public float hoverScale = 1.05f; // При наведении чуть больше
        public float pressScale = 0.95f; // При клике проминается внутрь
        public float animationSpeed = 15f;


        private Vector3 originalScale;
        private Vector3 targetScale;
        private Selectable selectable;


        private void Awake()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
            selectable = GetComponent<Selectable>();
        }


        private void Update()
        {
            // Плавная анимация к целевому размеру (работает даже при паузе в игре)
            if (transform.localScale != targetScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
            }
        }


        public void OnPointerEnter(PointerEventData eventData)
        {
            // Не анимируем серые (неактивные) кнопки
            if (selectable != null && !selectable.interactable) return; 
            targetScale = originalScale * hoverScale;
        }


        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = originalScale;
        }


        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable) return;
            targetScale = originalScale * pressScale;
        }


        public void OnPointerUp(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable) return;
            
            // Если после клика мышка всё ещё над кнопкой, оставляем увеличенной. Если ушла — сбрасываем.
            targetScale = eventData.pointerCurrentRaycast.gameObject == gameObject 
                ? originalScale * hoverScale 
                : originalScale;
        }


        private void OnDisable()
        {
            // Страховка: если меню резко закрыли, кнопка не застрянет увеличенной
            transform.localScale = originalScale;
            targetScale = originalScale;
        }
    }
}
