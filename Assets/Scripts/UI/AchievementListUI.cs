// Файл: Assets/Scripts/UI/AchievementListUI.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

    void OnEnable()
    {
        // Подписываемся на событие сброса
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementsReset += PopulateList;
        }
        
         // Каждый раз при открытии панели, мы перестраиваем список
        PopulateList();
    }
    
    void OnDisable()
    {
        // ОБЯЗАТЕЛЬНО отписываемся
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementsReset -= PopulateList;
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
            if (achData.isSecret && !isUnlocked)
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

        // Если у ачивки есть комикс, показываем его
        if (data.comicPages != null && data.comicPages.Count > 0)
        {
            comicViewer.ShowComic(data.comicPages);
        }
        else
        {
            Debug.LogWarning($"У ачивки '{data.displayName}' нет страниц комикса.");
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
        // Ищем MainMenuActions на сцене
        MainMenuActions menu = FindFirstObjectByType<MainMenuActions>();
        if (menu != null)
        {
            menu.Action_BackToMainMenu();
        }
        else
        {
            // Запасной вариант, если что-то пошло не так
            Debug.LogError("Не удалось найти MainMenuActions, чтобы вернуться назад!");
            gameObject.SetActive(false);
        }
    }
}