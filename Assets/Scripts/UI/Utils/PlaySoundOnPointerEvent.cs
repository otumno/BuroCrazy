using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Managers;
using Scriptables.Audio;

namespace UI.Utils
{
    public class PlaySoundOnPointerEvent : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("Настройки Звуков")]
        [Tooltip("Звук при наведении курсора")]
        public SoundID hoverSound = SoundID.UI_Hover;

        [Tooltip("Звук при клике")]
        public SoundID clickSound = SoundID.UI_Click_Default;

        [Header("Переключатели")]
        public bool enableHover = true;
        public bool enableClick = true;

        // Selectable - это вообще любой UI элемент кликабельный
        private Selectable _selectable;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enableHover || !IsInteractable())
                return;

            AudioManager.Instance.PlaySound(hoverSound);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!enableClick || !IsInteractable())
                return;

            AudioManager.Instance.PlaySound(clickSound);
        }

        private bool IsInteractable() => _selectable != null && _selectable.interactable;
    }
}