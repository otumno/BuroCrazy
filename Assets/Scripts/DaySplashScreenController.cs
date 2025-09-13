// Файл: DaySplashScreenController.cs - Исправленная и Упрощенная версия
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic; // <--- ДОБАВЛЕНА ЭТА СТРОКА

[RequireComponent(typeof(CanvasGroup))]
public class DaySplashScreenController : MonoBehaviour
{
    [Header("Ссылки")]
    public TextMeshProUGUI dayText;
    public Image backgroundImage;
    public List<Sprite> backgroundSprites;
    
    [Header("Настройки")]
    public float fadeTime = 0.3f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(int dayNumber)
    {
        dayText.text = $"день {dayNumber}";
        if (backgroundSprites != null && backgroundSprites.Count > 0)
        {
            backgroundImage.sprite = backgroundSprites[Random.Range(0, backgroundSprites.Count)];
        }
    }

    public IEnumerator Fade(bool fadeIn)
    {
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        
        canvasGroup.blocksRaycasts = true;
        
        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadeTime);
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;
        canvasGroup.interactable = fadeIn;
        canvasGroup.blocksRaycasts = fadeIn;
    }
}