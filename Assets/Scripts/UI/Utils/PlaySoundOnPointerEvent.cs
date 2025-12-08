using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Managers; // Подключаем пространство имен

namespace UI.Utils
{
    public class PlaySoundOnPointerEvent : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("Настройки Звуков")]
        [Tooltip("Звук при наведении курсора")]
        // Явно указываем Managers.SoundID, чтобы избежать путаницы
        public Managers.SoundID hoverSound = Managers.SoundID.UI_Hover;
        
        [Tooltip("Звук при клике")]
        public Managers.SoundID clickSound = Managers.SoundID.UI_Click_Default;

        [Header("Переключатели")]
        public bool enableHover = true;
        public bool enableClick = true;

        private Button button;
        private Toggle toggle;

        void Awake()
        {
            button = GetComponent<Button>();
            toggle = GetComponent<Toggle>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enableHover) return;
            if (!IsInteractable()) return;

            // Теперь типы совпадают
            AudioManager.Instance?.PlaySound(hoverSound);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!enableClick) return;
            if (!IsInteractable()) return;

            // Теперь типы совпадают
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