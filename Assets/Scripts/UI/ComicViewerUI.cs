// Assets/Scripts/UI/ComicViewerUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // <--- Добавлено для исправления ошибки CS1061

public class ComicViewerUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private Image pageImage;
    [SerializeField] private TextMeshProUGUI pageCounterText; 

    [Tooltip("Кнопки для перехода на СЛЕДУЮЩУЮ страницу")]
    [SerializeField] private List<Button> nextButtons;
    [Tooltip("Кнопки для перехода на ПРЕДЫДУЩУЮ страницу")]
    [SerializeField] private List<Button> prevButtons;
    [Tooltip("Кнопки для ЗАКРЫТИЯ просмотрщика")]
    [SerializeField] private List<Button> closeButtons;

    [Header("Настройки Перехода (Анимация)")]
    [Tooltip("Общее время перелистывания (N)")]
    [SerializeField] private float pageTurnDuration = 0.4f;
    
    [Tooltip("Цвет вспышки (обычно белый). Убедитесь, что Alpha = 1.")]
    [SerializeField] private Color flashColor = Color.white;
    
    [Tooltip("Image, которая лежит ПОВЕРХ страницы. Скрипт сам будет управлять её цветом и прозрачностью.")]
    [SerializeField] private Image transitionOverlay;

    [Tooltip("Image для анимации перелистывания (корешок).")]
    [SerializeField] private Image flipAnimationImage;
    
    [Tooltip("3 кадра анимации перелистывания.")]
    [SerializeField] private List<Sprite> flipFrames;

    [Header("Звуки")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private List<AudioClip> pageTurnSounds;

    private List<Sprite> currentComicPages;
    private int currentPageIndex = 0;
    private bool isTransitioning = false; 

    void Awake()
    {
        // Настройка кнопок
        if (nextButtons != null) foreach (var btn in nextButtons) if(btn) btn.onClick.AddListener(NextPage);
        if (prevButtons != null) foreach (var btn in prevButtons) if(btn) btn.onClick.AddListener(PrevPage);
        if (closeButtons != null) foreach (var btn in closeButtons) if(btn) btn.onClick.AddListener(CloseViewer);

        // Настройка аудио
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.ignoreListenerPause = true; // Чтобы звук работал на паузе
        
        // Скрываем оверлеи при старте
        if (transitionOverlay != null) 
        {
            transitionOverlay.gameObject.SetActive(false);
            transitionOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        }
        if (flipAnimationImage != null) flipAnimationImage.gameObject.SetActive(false);
    }
    
    private void OnDisable()
    {
        StopAllCoroutines();
        isTransitioning = false;
        if (transitionOverlay != null) transitionOverlay.gameObject.SetActive(false);
        if (flipAnimationImage != null) flipAnimationImage.gameObject.SetActive(false);
    }

    public void ShowComic(List<Sprite> pages)
    {
        if (pages == null || pages.Count == 0) return;
        
        PlaySound(openSound);
        currentComicPages = pages;
        currentPageIndex = 0;
        
        gameObject.SetActive(true);
        Time.timeScale = 0f; // Ставим игру на паузу
        
        isTransitioning = false;
        if (transitionOverlay != null)
        {
            transitionOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            transitionOverlay.gameObject.SetActive(false);
        }
        
        UpdatePageVisualsInstant();
    }

    private void CloseViewer()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f; // Снимаем паузу
        currentComicPages = null;
    }

    // --- Логика переключения ---

    private void NextPage()
    {
        if (isTransitioning || currentComicPages == null) return;
        if (currentPageIndex < currentComicPages.Count - 1)
        {
            StartCoroutine(PageTransitionRoutine(1));
        }
    }

    private void PrevPage()
    {
        if (isTransitioning || currentComicPages == null) return;
        if (currentPageIndex > 0)
        {
            StartCoroutine(PageTransitionRoutine(-1));
        }
    }

    // --- Главная Корутина Анимации ---
    private IEnumerator PageTransitionRoutine(int direction)
    {
        isTransitioning = true;
        SetButtonsInteractable(false);
        PlayRandomPageTurnSound();

        // Запускаем анимацию корешка (параллельно)
        StartCoroutine(RunFlipAnimation(direction));

        float halfDuration = pageTurnDuration / 2f;
        float timer = 0f;

        // 1. ЗАСВЕТ (Fade In Overlay)
        if (transitionOverlay != null)
        {
            transitionOverlay.gameObject.SetActive(true); // Включаем объект
            
            Color c = flashColor;
            c.a = 0f;
            transitionOverlay.color = c;
            
            while (timer < halfDuration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / halfDuration);
                
                c.a = progress; // Альфа от 0 до 1
                transitionOverlay.color = c;
                
                yield return null;
            }
            c.a = 1f;
            transitionOverlay.color = c;
        }
        else
        {
            yield return new WaitForSecondsRealtime(halfDuration);
        }

        // 2. МЕНЯЕМ КАРТИНКУ (под прикрытием засвета)
        currentPageIndex += direction;
        if (pageImage != null && currentComicPages != null && currentPageIndex >= 0 && currentPageIndex < currentComicPages.Count)
        {
            pageImage.sprite = currentComicPages[currentPageIndex];
        }
        UpdatePageCounter();

        // 3. ПРОЯВЛЕНИЕ (Fade Out Overlay)
        timer = 0f;
        if (transitionOverlay != null)
        {
            Color c = flashColor;
            while (timer < halfDuration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = 1f - Mathf.Clamp01(timer / halfDuration); // Альфа от 1 до 0
                
                c.a = progress;
                transitionOverlay.color = c;
                
                yield return null;
            }
            c.a = 0f;
            transitionOverlay.color = c;
            transitionOverlay.gameObject.SetActive(false); // Выключаем объект в конце
        }
        else
        {
            yield return new WaitForSecondsRealtime(halfDuration);
        }

        isTransitioning = false;
        SetButtonsInteractable(true);
    }

    // --- Корутина Анимации Корешка ---
    private IEnumerator RunFlipAnimation(int direction)
    {
        if (flipAnimationImage == null || flipFrames == null || flipFrames.Count < 3) yield break;

        flipAnimationImage.gameObject.SetActive(true);

        float timePerFrame = pageTurnDuration / 3f;
        int[] frameIndices = (direction > 0) ? new int[] { 0, 1, 2 } : new int[] { 2, 1, 0 };

        for (int i = 0; i < frameIndices.Length; i++)
        {
            flipAnimationImage.sprite = flipFrames[frameIndices[i]];
            yield return new WaitForSecondsRealtime(timePerFrame);
        }

        flipAnimationImage.gameObject.SetActive(false);
    }

    // --- Вспомогательные методы ---

    private void UpdatePageVisualsInstant()
    {
        if (pageImage != null && currentComicPages != null && currentComicPages.Count > 0)
        {
            pageImage.sprite = currentComicPages[currentPageIndex];
        }
        UpdatePageCounter();
        SetButtonsInteractable(true);
    }

    private void UpdatePageCounter()
    {
        if (pageCounterText != null && currentComicPages != null)
        {
            pageCounterText.text = $"Стр {currentPageIndex + 1} / {currentComicPages.Count}";
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        bool canGoPrev = interactable && (currentPageIndex > 0);
        bool canGoNext = interactable && (currentComicPages != null && currentPageIndex < currentComicPages.Count - 1);

        if (prevButtons != null) foreach (var btn in prevButtons) if (btn) btn.interactable = canGoPrev;
        if (nextButtons != null) foreach (var btn in nextButtons) if (btn) btn.interactable = canGoNext;
        if (closeButtons != null) foreach (var btn in closeButtons) if (btn) btn.interactable = interactable;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void PlayRandomPageTurnSound()
    {
        if (audioSource != null && pageTurnSounds != null && pageTurnSounds.Count > 0)
        {
            var validSounds = pageTurnSounds.Where(s => s != null).ToList();
            if (validSounds.Count > 0)
            {
                audioSource.PlayOneShot(validSounds[Random.Range(0, validSounds.Count)]);
            }
        }
    }
}