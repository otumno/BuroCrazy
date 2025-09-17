// Файл: FallingLeaf.cs - ОБНОВЛЕННАЯ ВЕРСИЯ
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

    // --- ИЗМЕНЕНИЕ: Добавлены новые параметры с значениями по умолчанию ---
    public IEnumerator Animate(Vector3 startPos, Vector3 endPos, float duration, bool fadeToBlack, bool useEaseIn, float dwellDuration = 2.0f, float fadeOutDuration = 0.5f)
    {
        float timer = 0f;
        rectTransform.position = startPos;

        Color startColor = fadeToBlack ? originalColor : Color.black;
        Color endColor = fadeToBlack ? Color.black : originalColor;
        
        // --- ЭТАП 1: Движение листа (как и было) ---
        while (timer < duration)
        {
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

        // --- ЭТАП 2 (НОВЫЙ): Задержка на месте ---
        // Лист лежит на "земле" заданное время
        yield return new WaitForSecondsRealtime(dwellDuration);

        // --- ЭТАП 3 (НОВЫЙ): Плавное исчезновение ---
        timer = 0f;
        Color currentColor = leafImage.color;
        Color transparentColor = new Color(currentColor.r, currentColor.g, currentColor.b, 0);

        while(timer < fadeOutDuration)
        {
            if (this == null || leafImage == null) yield break;
            timer += Time.unscaledDeltaTime;
            leafImage.color = Color.Lerp(currentColor, transparentColor, timer / fadeOutDuration);
            yield return null;
        }

        // --- ЭТАП 4: Уничтожение объекта ---
        // Происходит только после полного исчезновения
        Destroy(gameObject);
    }
}