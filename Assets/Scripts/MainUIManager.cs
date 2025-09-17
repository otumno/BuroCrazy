// Файл: MainUIManager.cs - ОБНОВЛЕННАЯ ВЕРСИЯ
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainUIManager : MonoBehaviour
{
    public static MainUIManager Instance { get; set; }

    [Header("Ссылки на компоненты UI")]
    public AudioSource uiAudioSource;
    [SerializeField] private GameObject pausePanel;
    [Header("Панели главного меню")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject saveLoadPanel;
    
    // --- НОВОЕ ПОЛЕ ДЛЯ НАСТРОЙКИ ---
    [Header("Настройки переходов")]
    [Tooltip("Как долго будет видна заставка 'День N' (в секундах)")]
    public float splashScreenDwellTime = 1.0f;

    public bool isTransitioning { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isTransitioning = true;
        if (TransitionManager.Instance != null)
        {
            // true = FadeIn (экран становится черным)
            yield return TransitionManager.Instance.AnimateTransition(true);
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        if (sceneName == "GameScene")
        {
            // --- ИЗМЕНЕНИЕ: Мы находим объекты, но передаем их в новую, более умную корутину ---
            StartOfDayPanel startOfDayPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            OrderSelectionUI orderSelectionUI = FindFirstObjectByType<OrderSelectionUI>(FindObjectsInactive.Include);
            DaySplashScreenController daySplashScreenController = FindFirstObjectByType<DaySplashScreenController>(FindObjectsInactive.Include);
            
            // Запускаем нашу новую, полностью переписанную последовательность
            yield return StartCoroutine(UnveilSequence(startOfDayPanel, orderSelectionUI, daySplashScreenController));
        }
        
        if (sceneName == "MainMenuScene")
        {
            isTransitioning = false;
        }
    }
    
    // --- ПОЛНОСТЬЮ ПЕРЕПИСАННАЯ КОРУТИНА ---
private IEnumerator UnveilSequence(StartOfDayPanel startOfDayPanel, OrderSelectionUI orderSelectionUI, DaySplashScreenController daySplashScreenController)
{
    // --- ЭТАП 1: ПОДГОТОВКА ЗА ЧЕРНЫМ ЭКРАНОМ ---
    PauseGame(true);

    if (SaveLoadManager.Instance.isNewGame)
    {
        Debug.Log("<color=cyan>[MainUIManager] Это НОВАЯ ИГРА. Сбрасываем состояния менеджеров.</color>");
        DirectorManager.Instance.ResetState();
        HiringManager.Instance.ResetState();
        PlayerWallet.Instance.ResetState();
        ClientSpawner.Instance.ResetState();
        ArchiveManager.Instance.ResetState();
    }
    else
    {
        // <<< ВОТ ИСПРАВЛЕНИЕ! >>>
        // Если это не новая игра, значит, мы должны загрузить данные.
        // Делаем это ЗДЕСЬ, когда все менеджеры уже существуют.
        Debug.Log("<color=green>[MainUIManager] Это ЗАГРУЗКА ИГРЫ. Вызываем SaveLoadManager.LoadGame().</color>");
        SaveLoadManager.Instance.LoadGame(SaveLoadManager.Instance.GetCurrentSlot());
    }

    DirectorManager.Instance.PrepareDay();
    HiringManager.Instance.GenerateNewCandidates();

    if (DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.directorChairPoint != null)
    {
        DirectorAvatarController.Instance.TeleportTo(DirectorAvatarController.Instance.directorChairPoint.position);
        DirectorAvatarController.Instance.ForceSetAtDeskState(true);
    }
	
    if (startOfDayPanel != null)
    {
        startOfDayPanel.gameObject.SetActive(true);
        var cg = startOfDayPanel.GetComponent<CanvasGroup>();
        cg.alpha = 1;
        cg.interactable = false; // Нельзя нажимать
    }
    if (orderSelectionUI != null)
    {
        orderSelectionUI.gameObject.SetActive(true);
        var cg = orderSelectionUI.GetComponent<CanvasGroup>();
        cg.alpha = 1;
        cg.interactable = false; // Нельзя нажимать
    }
    if (daySplashScreenController != null)
    {
        daySplashScreenController.gameObject.SetActive(true);
        daySplashScreenController.Setup(ClientSpawner.Instance.GetCurrentDay() + 1);
        var cg = daySplashScreenController.GetComponent<CanvasGroup>();
        cg.alpha = 1;
    }
    
    // --- ЭТАП 2: УБИРАЕМ "ШТОРЫ" ПО ОЧЕРЕДИ ---
    
    // 1. Убираем черный экран перехода. Под ним уже готова заставка "День N".
    if (TransitionManager.Instance != null)
    {
        yield return TransitionManager.Instance.AnimateTransition(false);
    }

    // 2. Ждем, пока игрок посмотрит на заставку.
    yield return new WaitForSecondsRealtime(splashScreenDwellTime);

    // 3. Убираем заставку "День N". Под ней уже готова панель приказов.
    if (daySplashScreenController != null)
    {
        yield return daySplashScreenController.Fade(false);
    }
    
    // 4. Делаем панель приказов активной для нажатий.
    if (orderSelectionUI != null)
    {
        orderSelectionUI.Setup();
        orderSelectionUI.GetComponent<CanvasGroup>().interactable = true;
        orderSelectionUI.GetComponent<CanvasGroup>().blocksRaycasts = true;
    }
    
    // На этом этапе управление переходит к игроку. Он должен выбрать приказ.
    isTransitioning = false;
}

    #region Навигация и вызовы из UI (без изменений)

public void OnSaveSlotClicked(int slotIndex)
{
    if (isTransitioning) return;
    Debug.Log($"[MainUIManager] ПОЛУЧЕНА КОМАНДА на загрузку игры из слота #{slotIndex}...");

    // 1. Просто запоминаем, какой слот надо будет загрузить.
    SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
    SaveLoadManager.Instance.isNewGame = false;

    // 2. СРАЗУ ЗАПУСКАЕМ загрузку сцены. Загрузка данных произойдет позже.
    StartCoroutine(LoadSceneRoutine("GameScene"));
}

public void OnNewGameClicked(int slotIndex)
{
    if (isTransitioning) return;
    Debug.Log($"[MainUIManager] Создание новой игры в слоте #{slotIndex}...");

    // 1. Устанавливаем текущий слот
    SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
    SaveLoadManager.Instance.isNewGame = true; // Указываем, что это новая игра

    // 2. СОЗДАЕМ И СОХРАНЯЕМ СТАРТОВЫЕ ДАННЫЕ! (Этого шага не хватало)
    // Это создаст файл save_slot_N.json, который будет виден при следующем запуске
    SaveData newGameData = new SaveData
    {
        day = 1,
        money = 150, // Ваши стартовые деньги
        archiveDocumentCount = 0,
        activePermanentOrderNames = new System.Collections.Generic.List<string>(),
        completedOneTimeOrderNames = new System.Collections.Generic.List<string>(),
        allStaffData = new System.Collections.Generic.List<StaffSaveData>(),
        allDocumentStackData = new System.Collections.Generic.List<DocumentStackSaveData>()
    };
    SaveLoadManager.Instance.SaveNewGame(slotIndex, newGameData);
    
    // 3. Запускаем переход на игровую сцену
    StartCoroutine(LoadSceneRoutine("GameScene"));
}
    
    public void OnClick_Continue()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        var savePanel = FindFirstObjectByType<SaveLoadPanelController>()?.gameObject;
        if(savePanel != null)
        {
            savePanel.SetActive(true);
        }
        SaveLoadManager.Instance.isNewGame = false;
    }

    public void OnClick_NewGame()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        var savePanel = FindFirstObjectByType<SaveLoadPanelController>()?.gameObject;
        if(savePanel != null)
        {
            savePanel.SetActive(true);
        }
        SaveLoadManager.Instance.isNewGame = true;
    }

    public void OnClick_BackToMainMenu()
    {
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }
    public void OnClick_QuitGame()
    {
        Debug.Log("Выход из игры...");
        Application.Quit();
    }

    public void GoToMainMenu()
    {
        if(isTransitioning) return;
        StartCoroutine(LoadSceneRoutine("MainMenuScene"));
    }

    public void TriggerNextDayTransition()
    {
        if (isTransitioning) return;
        StartCoroutine(LoadSceneRoutine("GameScene"));
    }

    #endregion
    
    #region Управление UI панелями (без изменений)

    public void ShowPausePanel(bool show)
    {
        if (isTransitioning) return;
        if (show)
        {
            PauseGame();
            if (pausePanel != null) pausePanel.SetActive(true);
        }
        else
        {
            ResumeGame();
            if (pausePanel != null) pausePanel.SetActive(false);
        }
    }

    public void ShowOrderSelection()
    {
        OrderSelectionUI orderUI = FindFirstObjectByType<OrderSelectionUI>();
        if(orderUI != null)
        {
            StartCoroutine(orderUI.Fade(true));
        }
    }

    #endregion
    
    #region Управление временем и геймплеем (без изменений)
    public void PauseGame(bool playMusic = true)
    {
        Time.timeScale = 0f;
        if (playMusic && MusicPlayer.Instance != null)
        {
            MusicPlayer.Instance.PauseGameplayMusicAndPlayOfficeTheme();
        }
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        if (MusicPlayer.Instance != null)
        {
            MusicPlayer.Instance.ResumeGameplayMusic();
        }
    }
    
    public void StartOrResumeGameplay()
    {
        if (isTransitioning) return;
        StartCoroutine(StartGameplaySequence());
    }

private IEnumerator StartGameplaySequence()
{
    isTransitioning = true;
    StartOfDayPanel sodp = FindFirstObjectByType<StartOfDayPanel>();
    OrderSelectionUI osui = FindFirstObjectByType<OrderSelectionUI>();
    GameObject igub = FindFirstObjectByType<InGameUI_Actions>()?.gameObject;

    if (sodp != null) yield return StartCoroutine(sodp.Fade(false, false));
    if (osui != null && osui.gameObject.activeSelf) yield return StartCoroutine(osui.Fade(false));

    if (igub != null)
    {
        igub.SetActive(true);
    }
    
    ResumeGame();
    isTransitioning = false;

    // <<< ДОБАВЬТЕ ЭТУ СТРОКУ В САМОМ КОНЦЕ >>>
    // Даем команду MusicPlayer'у начать проигрывать музыку геймплея (день/ночь)
    if (MusicPlayer.Instance != null)
    {
        MusicPlayer.Instance.StartGameplayMusic();
    }
}
    #endregion
}