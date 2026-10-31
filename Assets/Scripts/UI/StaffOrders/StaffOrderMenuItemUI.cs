using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.StaffOrders
{
    /// <summary>
    /// Один пункт меню приказов: кнопка с текстом.
    /// </summary>
    public class StaffOrderMenuItemUI : MonoBehaviour
    {
        private const float DisabledLabelAlpha = 0.45f;

        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;

        public void Setup(string text, bool isInteractable, Action onClick)
        {
            if (label != null)
            {
                label.text = text;
                // Кнопка при отключении тонирует только фон, текст приглушаем сами.
                label.alpha = isInteractable ? 1f : DisabledLabelAlpha;
            }
            if (button == null) return;

            button.interactable = isInteractable;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
    }
}
