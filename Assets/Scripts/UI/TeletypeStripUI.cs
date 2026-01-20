// Assets/Scripts/UI/TeletypeStripUI.cs
using UnityEngine;
using TMPro;
using DG.Tweening; // Используем DOTween для анимации выезда

namespace UI
{
    public class TeletypeStripUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private CanvasGroup canvasGroup;

        public void Setup(string text, string periodPrefix)
        {
            if (messageText != null)
            {
                // Формат: [УТРО] Текст сообщения...
                messageText.text = $"<b>[{periodPrefix}]</b> {text}";
            }
            
            // Анимация появления (выползает сбоку или проявляется)
            transform.localScale = new Vector3(1f, 0f, 1f);
            transform.DOScaleY(1f, 0.3f).SetEase(Ease.OutBack);
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, 0.3f);
            }
        }

        public void RemoveStrip()
        {
            // Исчезает и уничтожается
            if (canvasGroup != null) canvasGroup.DOFade(0f, 0.2f);
            transform.DOScaleY(0f, 0.2f).OnComplete(() => Destroy(gameObject));
        }
    }
}