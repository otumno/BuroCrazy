using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
public class DaySplashScreenController : MonoBehaviour
{
    public static DaySplashScreenController Instance { get; private set; }

    [Header("Ссылки")]
    public TextMeshProUGUI dayText;
    public Image backgroundImage;
    public List<Sprite> backgroundSprites;

    [Header("Настройки")]
    public float fadeTime = 0.3f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        // --- НОВАЯ ЛОГИКА "СИНГЛТОНА" ---
        // Делаем его "одиночкой", чтобы избежать дубликатов при загрузке сцен
        if (Instance == null)
        {
            Instance = this;
            // Не делаем DontDestroyOnLoad, так как он должен существовать только на игровой сцене
        }
        else if (Instance != this)
        {
            // Если мы дубликат, самоуничтожаемся
            Destroy(gameObject);
            return;
        }
        // ---------------------------------

        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(int dayNumber)
    {
        if (dayText != null)
        {
            dayText.text = $"ДЕНЬ {dayNumber}";
        }

        if (backgroundSprites != null && backgroundSprites.Count > 0)
        {
            if(backgroundImage.sprite == null) // Меняем фон, только если его еще нет
            {
                backgroundImage.sprite = backgroundSprites[Random.Range(0, backgroundSprites.Count)];
            }
        }
    }

    public IEnumerator Fade(bool fadeIn)
    {
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;

        // --- ИСПРАВЛЕНИЕ: Блокируем взаимодействие только в конце, а не в начале ---

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