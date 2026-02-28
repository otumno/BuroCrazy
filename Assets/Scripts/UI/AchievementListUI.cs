// Файл: Assets/Scripts/UI/AchievementListUI.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Managers;
using Enums;
using Data.Creation;

public class AchievementListUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private Transform contentContainer; 
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject achievementItemPrefab; 
    [SerializeField] private Button resetButton; // Перетащи сюда кнопку сброса

    [Header("Менеджеры")]
    [SerializeField] private ComicViewerUI comicViewer; 

    // --- НАЧАЛО ДОБАВЛЕНИЙ (ЗВУКИ) ---
    [Header("Звуки")]
    [Tooltip("AudioSource для проигрывания звуков. Если пусто, будет искаться на этом объекте.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Звук, который проигрывается при нажатии на разблокированную ачивку.")]
    [SerializeField] private AudioClip achievementClickSound;
    // --- КОНЕЦ ДОБАВЛЕНИЙ ---

    void Awake()
    {
        backButton.onClick.AddListener(HidePanel);
        
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetClicked);
        }

        // --- НАЧАЛО ДОБАВЛЕНИЙ (ПОИСК AUDIOSOURCE) ---
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        // Устанавливаем, чтобы звук работал на паузе (Time.timeScale = 0)
        audioSource.ignoreListenerPause = true; 
        // --- КОНЕЦ ДОБАВЛЕНИЙ ---
    }

    void Start()
    {
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementsReset += RefreshData;
            // Подпишемся на единичные ачивки, чтобы список обновлялся сам
            AchievementManager.Instance.OnAchievementUnlocked += (data) => RefreshData();
        }
        RefreshData();
    }

    void OnDestroy()
    {
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementsReset -= RefreshData;
            AchievementManager.Instance.OnAchievementUnlocked -= (data) => RefreshData();
        }
    }

    public void RefreshData()
    {
        StopAllCoroutines();
        StartCoroutine(PopulateListRoutine());
    }

    private IEnumerator PopulateListRoutine()
    {
        // Даем окну 0.35 сек на красивое появление без лагов
        yield return new WaitForSecondsRealtime(0.35f);

        if (AchievementManager.Instance == null || contentContainer == null || achievementItemPrefab == null) yield break;

        foreach (Transform child in contentContainer) Destroy(child.gameObject);

        List<AchievementData> allAchievements = AchievementManager.Instance.allAchievementsDatabase;

        foreach (AchievementData achData in allAchievements)
        {
            bool isUnlocked = AchievementManager.Instance.IsAchievementUnlocked(achData.achievementID);
            if (achData.isSecret && !isUnlocked && !achData.alwaysAvailable) continue;

            GameObject itemGO = Instantiate(achievementItemPrefab, contentContainer);
            AchievementItemUI itemUI = itemGO.GetComponent<AchievementItemUI>();
            itemUI.Setup(achData, this, isUnlocked);
        }
    }

    private void PopulateList()
    {
        // 1. Проверяем, что все на месте
        if (AchievementManager.Instance == null || contentContainer == null || achievementItemPrefab == null)
        {
            Debug.LogError("[AchievementListUI] Ошибка: Не назначен AchievementManager, Content Container или Префаб!");
            return;
        }

        // 2. Очищаем старый список
         foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        // 3. Получаем ВСЕ ачивки из базы
        List<AchievementData> allAchievements = AchievementManager.Instance.allAchievementsDatabase;

        // 4. Создаем ячейки для каждой ачивки
        foreach (AchievementData achData in allAchievements)
        {
            // Пропускаем секретные ачивки, которые еще не открыты
            bool isUnlocked = AchievementManager.Instance.IsAchievementUnlocked(achData.achievementID);
            if (achData.isSecret && !isUnlocked && !achData.alwaysAvailable)
            {
                continue; // Пропускаем
            }

            // Создаем префаб ячейки
            GameObject itemGO = Instantiate(achievementItemPrefab, contentContainer);
            AchievementItemUI itemUI = itemGO.GetComponent<AchievementItemUI>();
            
            // Настраиваем ячейку, передавая ей данные и статус (открыта/закрыта)
            itemUI.Setup(achData, this, isUnlocked);
        }
    }

    /// <summary>
    /// Вызывается из AchievementItemUI, когда игрок кликает на ачивку
    /// </summary>
    public void OnAchievementClicked(AchievementData data)
    {
        // --- ДОБАВЛЕНО (ЗВУК КЛИКА) ---
        if (audioSource != null && achievementClickSound != null)
        {
            audioSource.PlayOneShot(achievementClickSound);
        }
        // --- КОНЕЦ ---

        if (data.bookType == BookType.Director)
        {
            ShowDirectorBook(data);
        }
        else if (data.comicPages != null && data.comicPages.Count > 0)
        {
            comicViewer.ShowComic(data.comicPages);
        }
        else
        {
            Debug.LogWarning($"У ачивки '{data.displayName}' нет страниц комикса.");
        }
    }

    private void ShowDirectorBook(AchievementData data)
    {
        List<Sprite> pages = new List<Sprite>();

        if (data.directorBookPages != null && data.directorBookPages.Count > 0)
        {
            foreach (var page in data.directorBookPages)
            {
                if (page.characterIllustration != null)
                {
                    pages.Add(page.characterIllustration);
                }
            }
        }

        if (pages.Count > 0 && comicViewer != null)
        {
            comicViewer.ShowComic(pages);
        }
        else
        {
            Debug.LogWarning($"У книги директора '{data.displayName}' нет страниц для отображения.");
        }
    }
    
    /// <summary>
    /// Вызывается при нажатии кнопки "Сброс"
    /// </summary>
    private void OnResetClicked()
    {
        // Просто просим менеджер все сбросить.
        AchievementManager.Instance?.ResetAllAchievements();
    }


    private void HidePanel()
    {
        // Мы НЕ закрываем себя сами. Мы просим роутер (MainMenuActions) вернуть нас в меню.
        // Роутер сам вызовет наш UIWindowAnimator.Close()!
        MainMenuActions menu = FindFirstObjectByType<MainMenuActions>(FindObjectsInactive.Include);
        if (menu != null) 
        {
            menu.Action_BackToMainMenu();
        }
        else 
        {
            // Фоллбэк на случай ошибки
            var animator = GetComponent<UIWindowAnimator>();
            if (animator != null) animator.Close();
            else gameObject.SetActive(false);
        }
    }
}