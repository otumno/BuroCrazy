// Файл: FallingLeaf.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class FallingLeaf : MonoBehaviour
{
    private Image leafImage;
    private Color originalColor;
    private RectTransform rectTransform;

    void Awake()
    {
        leafImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        originalColor = leafImage.color;
    }

    public IEnumerator Animate(Vector3 startPos, Vector3 endPos, float duration, bool fadeToBlack, bool useEaseIn)
    {
        float timer = 0f;
        rectTransform.position = startPos;

        Color startColor = fadeToBlack ? originalColor : Color.black;
        Color endColor = fadeToBlack ? Color.black : originalColor;
        while (timer < duration)
        {
            // --- Проверка на случай, если объект уничтожили раньше времени ---
            if (this == null || rectTransform == null) yield break; 
            
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float easedProgress;

            if (useEaseIn)
            {
                easedProgress = progress * progress * progress;
            }
            else
            {
                easedProgress = 1 - Mathf.Pow(1 - progress, 3);
            }

            rectTransform.position = Vector3.LerpUnclamped(startPos, endPos, easedProgress);
            leafImage.color = Color.Lerp(startColor, endColor, progress);

            yield return null;
        }

        // --- ДОБАВЬТЕ ЭТУ СТРОКУ ---
        // Когда анимация завершена, лист сам себя уничтожает.
        Destroy(gameObject);
    }
}