using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class FallingLeaf : MonoBehaviour
{
    private Image leafImage;
    private Color originalColor;
    private RectTransform rectTransform;

    // <<< НОВОЕ: Переменные для запоминания пути >>>
    private Vector3 startPosition;
    private Vector3 endPosition;
    private float movementDuration;
    private bool useEaseInForMovement;

    void Awake()
    {
        leafImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        originalColor = leafImage.color;
    }
    
    void Update()
    {
        if (TransitionManager.Instance != null)
        {
            leafImage.color = Color.Lerp(Color.black, originalColor, TransitionManager.GlobalFadeValue);
        }
    }

    /// <summary>
    /// Анимация ПОЯВЛЕНИЯ листа.
    /// </summary>
    public IEnumerator AnimateMovement(Vector3 startPos, Vector3 endPos, float duration, bool useEaseIn)
    {
        // <<< ИЗМЕНЕНИЕ: Запоминаем параметры для обратного пути >>>
        this.startPosition = startPos;
        this.endPosition = endPos;
        this.movementDuration = duration;
        this.useEaseInForMovement = useEaseIn;

        float timer = 0f;
        rectTransform.position = startPos;
        
        while (timer < movementDuration)
        {
            if (this == null || rectTransform == null) yield break;
            
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            
            // Используем EaseOut (быстро в начале, медленно в конце)
            float easedProgress = 1 - Mathf.Pow(1 - progress, 3);

            rectTransform.position = Vector3.LerpUnclamped(startPos, endPos, easedProgress);
            
            yield return null;
        }
    }

    /// <summary>
    /// <<< НОВЫЙ МЕТОД: Анимация ИСЧЕЗНОВЕНИЯ листа >>>
    /// </summary>
    public IEnumerator AnimateExit()
    {
        float timer = 0f;
        Vector3 currentPos = rectTransform.position; // Начинаем с текущей позиции
        Vector3 targetPos = startPosition;          // Летим обратно на старт

        while (timer < movementDuration)
        {
            if (this == null || rectTransform == null) yield break;

            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / movementDuration);

            // Используем EaseIn (медленно в начале, быстро в конце) для эффекта "взлета"
            float easedProgress = progress * progress * progress;

            rectTransform.position = Vector3.LerpUnclamped(currentPos, targetPos, easedProgress);

            yield return null;
        }

        // В конце анимации лист самоуничтожается
        Destroy(gameObject);
    }
}