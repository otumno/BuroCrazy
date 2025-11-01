// Файл: Assets/Scripts/UI/ComicViewerUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic; // Убедись, что эта строка есть
using System.Linq; // Добавлено для .Count() > 0

public class ComicViewerUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private Image pageImage;
    [SerializeField] private TextMeshProUGUI pageCounterText; 

    // --- НАЧАЛО ИЗМЕНЕНИЙ (Замена на списки) ---
    [Tooltip("Кнопки для перехода на СЛЕДУЮЩУЮ страницу")]
    [SerializeField] private List<Button> nextButtons;
    [Tooltip("Кнопки для перехода на ПРЕДЫДУЩУЮ страницу")]
    [SerializeField] private List<Button> prevButtons;
    [Tooltip("Кнопки для ЗАКРЫТИЯ просмотрщика")]
    [SerializeField] private List<Button> closeButtons;
    // --- КОНЕЦ ИЗМЕНЕНИЙ ---

    [Header("Звуки")]
    [Tooltip("AudioSource для проигрывания звуков. Если пусто, будет искаться на этом объекте.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Звук, который проигрывается при открытии комикса.")]
    [SerializeField] private AudioClip openSound;
    [Tooltip("Список звуков, которые проигрываются при перелистывании страницы (выбирается случайно).")]
    [SerializeField] private List<AudioClip> pageTurnSounds;

    private List<Sprite> currentComicPages;
    private int currentPageIndex = 0;

    void Awake()
    {
        // --- НАЧАЛО ИЗМЕНЕНИЙ (Назначение слушателей через циклы) ---
        
        // Назначаем действия кнопкам "Вперед"
        if (nextButtons != null)
        {
            foreach (Button button in nextButtons)
            {
                if (button != null) button.onClick.AddListener(NextPage);
            }
        }

        // Назначаем действия кнопкам "Назад"
        if (prevButtons != null)
        {
            foreach (Button button in prevButtons)
            {
                if (button != null) button.onClick.AddListener(PrevPage);
            }
        }

        // Назначаем действия кнопкам "Закрыть"
        if (closeButtons != null)
        {
            foreach (Button button in closeButtons)
            {
                if (button != null) button.onClick.AddListener(CloseViewer);
            }
        }
        // --- КОНЕЦ ИЗМЕНЕНИЙ ---

        // Поиск AudioSource (без изменений)
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
		audioSource.ignoreListenerPause = true;
    }

    // Метод ShowComic (без изменений)
    public void ShowComic(List<Sprite> pages)
    {
        if (pages == null || pages.Count == 0) return;
        PlaySound(openSound);
        currentComicPages = pages;
        currentPageIndex = 0;
        gameObject.SetActive(true);
        Time.timeScale = 0f; 
        UpdatePage();
    }

    // Метод CloseViewer (без изменений)
    private void CloseViewer()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f; 
        currentComicPages = null; 
    }

    // Метод NextPage (без изменений)
    private void NextPage()
    {
        if (currentPageIndex < currentComicPages.Count - 1)
        {
            currentPageIndex++;
            UpdatePage();
            PlayRandomPageTurnSound();
        }
    }

    // Метод PrevPage (без изменений)
    private void PrevPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            UpdatePage();
            PlayRandomPageTurnSound();
        }
    }

    /// <summary>
    /// Обновляет спрайт страницы и состояние кнопок
    /// </summary>
    private void UpdatePage()
    {
        pageImage.sprite = currentComicPages[currentPageIndex];

        // --- НАЧАЛО ИЗМЕНЕНИЙ (Обновление интерактивности) ---
        bool canGoPrev = (currentPageIndex > 0);
        bool canGoNext = (currentPageIndex < currentComicPages.Count - 1);

        // Обновляем все кнопки "Назад"
        if (prevButtons != null)
        {
            foreach (Button button in prevButtons)
            {
                if (button != null) button.interactable = canGoPrev;
            }
        }
        
        // Обновляем все кнопки "Вперед"
        if (nextButtons != null)
        {
            foreach (Button button in nextButtons)
            {
                if (button != null) button.interactable = canGoNext;
            }
        }
        // --- КОНЕЦ ИЗМЕНЕНИЙ ---

        if (pageCounterText != null)
        {
            pageCounterText.text = $"Стр {currentPageIndex + 1} / {currentComicPages.Count}";
        }
    }

    // Метод PlaySound (без изменений)
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Метод PlayRandomPageTurnSound (без изменений)
    private void PlayRandomPageTurnSound()
    {
        if (audioSource != null && pageTurnSounds != null && pageTurnSounds.Count > 0)
        {
            var validSounds = pageTurnSounds.Where(s => s != null).ToList();
            if (validSounds.Count > 0)
            {
                AudioClip clipToPlay = validSounds[Random.Range(0, validSounds.Count)];
                audioSource.PlayOneShot(clipToPlay);
            }
        }
    }
}