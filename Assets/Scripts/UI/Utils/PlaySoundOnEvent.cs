using UnityEngine;
using UnityEngine.EventSystems; // Нужен для обработки событий мыши
using UnityEngine.UI; // Нужен для проверки интерактивности кнопки
using Managers;

namespace UI.Utils
{
    public class PlaySoundOnEvent : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("Настройки Звуков")]
        [Tooltip("Звук при наведении курсора")]
        public SoundID hoverSound = SoundID.UI_Hover;
        [Tooltip("Звук при клике")]
        public SoundID clickSound = SoundID.UI_Click_Default;

        [Header("Переключатели")]
        public bool enableHover = true;
        public bool enableClick = true;

        private Button button;
        private Toggle toggle;

        void Awake()
        {
            // Пытаемся найти кнопку или тоггл, чтобы не играть звук, если они выключены (not interactable)
            button = GetComponent<Button>();
            toggle = GetComponent<Toggle>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enableHover) return;
            if (!IsInteractable()) return;

            AudioManager.Instance?.PlaySound(hoverSound);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!enableClick) return;
            if (!IsInteractable()) return;

            AudioManager.Instance?.PlaySound(clickSound);
        }

        private bool IsInteractable()
        {
            if (button != null && !button.interactable) return false;
            if (toggle != null && !toggle.interactable) return false;
            return true;
        }
    }
}