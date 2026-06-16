// Файл: Assets/Scripts/UI/AchievementListUI.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Managers;
using Enums;
using Data.Creation;
using Scriptables.Audio;
using UI.Effects;
using UI.Utils;

public class AchievementListUI : MonoBehaviour
{
    [Header("Ссылки на UI")]
    [SerializeField] private Transform contentContainer;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject achievementItemPrefab;
    [SerializeField] private Button resetButton; // Перетащи сюда кнопку сброса

    [Header("Менеджеры")]
    [SerializeField] private ComicViewerUI comicViewer;

    [Header("Сетка")]
    [Tooltip("Ширина ячейки (px), если autoFitGrid выключен или контейнер ещё не измерен.")]
    [SerializeField] private float defaultCellWidth = 500f;
    [Tooltip("Минимальная ширина ячейки (px) при autoFitGrid.")]
    [SerializeField] private float minCellWidth = 500f;
    [Tooltip("Максимальная ширина ячейки (px) при autoFitGrid.")]
    [SerializeField] private float maxCellWidth = 500f;
    [Tooltip("Высота ячейки (px).")]
    [SerializeField] private float cellHeight = 700f;
    [Tooltip("Отступы внутри сетки (left, right, top, bottom).")]
    [SerializeField] private RectOffset gridPadding = new RectOffset(16, 16, 16, 16);
    [Tooltip("Расстояние между ячейками по X и Y.")]
    [SerializeField] private Vector2 cellSpacing = new Vector2(30f, 30f);
    [Tooltip("Если true — ширина ячейки рассчитывается по ширине contentContainer.")]
    [SerializeField] private bool autoFitGrid = false;

    // --- НАЧАЛО ДОБАВЛЕНИЙ (ЗВУКИ) ---
    [Header("Звуки")]
    [Tooltip("AudioSource для проигрывания звуков. Если пусто, будет искаться на этом объекте.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Звук, который проигрывается при нажатии на разблокированную ачивку.")]
    [SerializeField] private AudioClip achievementClickSound;
    // --- КОНЕЦ ДОБАВЛЕНИЙ ---

#if UNITY_EDITOR
    // --- ТЕСТОВЫЙ DEBUG-ТУМБЛЕР (Editor only, удалить когда не нужен) ---
    private bool allUnlockedDebug = false;
#endif

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
    }

    void OnEnable()
    {
        // При каждом показе панели — обновить список
        RefreshData();
    }

#if UNITY_EDITOR
    void Update()
    {
        // F12 — тестовый тумблер «всё разблокировано» (только PlayMode в Editor)
        if (!Input.GetKeyDown(KeyCode.F12)) return;
        if (AchievementManager.Instance == null) return;

        allUnlockedDebug = !allUnlockedDebug;

        if (allUnlockedDebug)
        {
            // Один тост для обратной связи
            RaiseSingleDebugToast();
        }

        // Перерисовываем список: ячейки получат правильный interactable / иконки
        RefreshData();
    }

    private void RaiseSingleDebugToast()
    {
        var db = AchievementManager.Instance.allAchievementsDatabase;
        if (db == null) return;
        AchievementData first = null;
        foreach (var d in db) { if (d != null) { first = d; break; } }
        if (first == null) return;
        AchievementManager.Instance.DebugRaiseUnlockToast(first);
    }
#endif

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
        if (AchievementManager.Instance == null || contentContainer == null || achievementItemPrefab == null) yield break;

        // --- Ждём, пока контейнер будет виден и измерен ---
        // (при первом запуске панель ещё не активна, ширина = 0)
        float waited = 0f;
        const float maxWait = 2f;
        RectTransform containerRect = contentContainer as RectTransform;
        while (waited < maxWait)
        {
            if (containerRect == null) yield break;

            // Скролл-вьюха должна быть активна и контейнер должен иметь ширину
            if (gameObject.activeInHierarchy && containerRect.rect.width > 1f) break;
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        if (containerRect == null || containerRect.rect.width <= 1f) yield break;

        // Даем окну 0.15 сек на красивое появление без лагов
        yield return new WaitForSecondsRealtime(0.15f);

        // --- СЕТКА ---
        EnsureGridLayout();
        if (autoFitGrid) ConfigureGridCellSize();

        foreach (Transform child in contentContainer) Destroy(child.gameObject);

        List<AchievementData> allAchievements = AchievementManager.Instance.allAchievementsDatabase;

        foreach (AchievementData achData in allAchievements)
        {
            bool isUnlocked = AchievementManager.Instance.IsAchievementUnlocked(achData.achievementID);
#if UNITY_EDITOR
            // F12 debug: тумблер «всё открыто» подменяет isUnlocked на true для ВСЕХ ачивок
            if (allUnlockedDebug) isUnlocked = true;
#endif
            if (achData.isSecret && !isUnlocked && !achData.alwaysAvailable) continue;

            GameObject itemGO = Instantiate(achievementItemPrefab, contentContainer);
            AchievementItemUI itemUI = itemGO.GetComponent<AchievementItemUI>();
            itemUI.Setup(achData, this, isUnlocked);

            // Принудительно задаём размер ячейки (префаб может иметь свой LayoutElement)
            ForceItemSize(itemGO);

            // Добавляем эффекты как у обычных кнопок (увеличение при наведении + звук)
            AttachButtonEffects(itemGO);
        }

        // Дополнительный прогрев компоновки после наполнения
        yield return null;
        if (contentContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)contentContainer);
        }
    }

    /// <summary>
    /// Гарантирует наличие GridLayoutGroup + ContentSizeFitter на contentContainer.
    /// Настраивает отступы, расстояние между ячейками, размер контента.
    /// </summary>
    private void EnsureGridLayout()
    {
        var grid = contentContainer.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = contentContainer.gameObject.AddComponent<GridLayoutGroup>();

        grid.padding = gridPadding;
        grid.spacing = cellSpacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.enabled = true;

        // Установим разумный дефолтный размер, пока не отработал ConfigureGridCellSize
        grid.cellSize = new Vector2(defaultCellWidth, cellHeight);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
        grid.constraintCount = 1;

        // Скролл-контейнер должен уметь расширяться по высоте содержимого
        var fitter = contentContainer.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = contentContainer.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Рассчитывает оптимальную ширину ячейки, чтобы они заняли всю ширину контейнера.
    /// Учитывает padding, spacing и желаемое количество колонок.
    /// </summary>
    private void ConfigureGridCellSize()
    {
        if (!(contentContainer is RectTransform containerRect)) return;

        Canvas.ForceUpdateCanvases();
        float containerWidth = containerRect.rect.width;
        if (containerWidth <= 1f) return; // ещё не измерился

        float availableWidth = containerWidth - gridPadding.left - gridPadding.right;
        if (availableWidth <= 0f) return;

        // Перебираем возможное число колонок от 6 до 1
        int bestColumns = 1;
        float bestCellWidth = availableWidth;
        for (int cols = 6; cols >= 1; cols--)
        {
            float totalSpacing = cellSpacing.x * (cols - 1);
            float cellWidth = (availableWidth - totalSpacing) / cols;
            if (cellWidth >= minCellWidth && cellWidth <= maxCellWidth)
            {
                bestColumns = cols;
                bestCellWidth = cellWidth;
                break;
            }
        }

        // Если даже 1 колонка не укладывается в maxCellWidth — берём maxCellWidth
        if (bestCellWidth > maxCellWidth) bestCellWidth = maxCellWidth;

        var grid = contentContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(bestCellWidth, cellHeight);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = bestColumns;
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

    /// <summary>
    /// Навешивает на кнопку-ачивку эффекты как у обычных кнопок: увеличение при наведении/клике
    /// (UIButtonJuice) и звук (PlaySoundOnPointerEvent). Добавляет только если их ещё нет.
    /// </summary>
    private void AttachButtonEffects(GameObject itemGO)
    {
        if (itemGO == null) return;

        // Увеличение при наведении / нажатии
        var juice = itemGO.GetComponent<UIButtonJuice>();
        if (juice == null) juice = itemGO.AddComponent<UIButtonJuice>();

        // Звук: hover + click
        var sound = itemGO.GetComponent<PlaySoundOnPointerEvent>();
        if (sound == null) sound = itemGO.AddComponent<PlaySoundOnPointerEvent>();
        sound.hoverSound = SoundID.UI_Hover;
        sound.clickSound = SoundID.UI_Click_Default;
    }

    /// <summary>
    /// Принудительно задаёт размер префаба ячейки: снимает LayoutElement (если есть) и
    /// устанавливает RectTransform.sizeDelta по defaultCellWidth/cellHeight. Нужно потому что
    /// LayoutElement на префабе имеет приоритет над GridLayoutGroup.cellSize.
    /// </summary>
    private void ForceItemSize(GameObject itemGO)
    {
        if (itemGO == null) return;

        // LayoutElement на префабе заставляет ячейку иметь его preferred size (220×220)
        var le = itemGO.GetComponent<LayoutElement>();
        if (le != null) Destroy(le);

        // Также удалим возможные дочерние LayoutElement, которые могут влиять на размер корня
        foreach (var child in itemGO.GetComponentsInChildren<LayoutElement>(true))
        {
            // Не трогаем дочерние — только корневой
            if (child.gameObject == itemGO) Destroy(child);
        }

        // Прямое задание sizeDelta
        var rt = itemGO.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(defaultCellWidth, cellHeight);
            // Отключаем preserveAspect на дочерних Image, чтобы не было конфликта
            foreach (var img in itemGO.GetComponentsInChildren<Image>(true))
            {
                img.preserveAspect = false;
            }
        }
    }
}