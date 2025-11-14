using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainUIManager : MonoBehaviour
{
    public static MainUIManager Instance { get; set; }
    [Header("Ссылки на компоненты UI")]
    public AudioSource uiAudioSource;
    [SerializeField] private GameObject pausePanel;
    [Header("Настройки переходов")]
    public float splashScreenDwellTime = 2.0f;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";
    public bool isTransitioning { get; private set; } = false;
	
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // --- <<< ЭТОТ КОД ДОЛЖЕН ЗДЕСЬ БЫТЬ >>> ---
            transform.SetParent(null); 
            DontDestroyOnLoad(gameObject); 
            // --- <<< КОНЕЦ >>> ---
            Debug.Log($"<color=green>[MainUIManager]</color> Awake: Я стал Singleton. Объект 'gameObject' сделан бессмертным.");
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[MainUIManager] Awake: Найден дубликат. Уничтожаю *себя* (этот GameObject).");
            
            Destroy(gameObject); 
        }
    }
	
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) 
        {
            StartOfDayPanel deskPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            bool isDirectorDeskOpen = deskPanel != null && deskPanel.gameObject.activeInHierarchy;
            
            bool isAnyOtherMajorPanelOpen = false; 
            
            if (isDirectorDeskOpen || isAnyOtherMajorPanelOpen)
            {
                //
            }
            else
            {
                bool isPaused = Time.timeScale == 0f;
                ShowPausePanel(!isPaused);
            }
        }
    }

    public void ShowDirectorDesk()
    {
        if (isTransitioning) return;

        StartOfDayPanel deskPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include); 

        if (deskPanel != null)
        {
            PauseGame(true);
            
            StartCoroutine(deskPanel.Fade(true, true));
        }
        else
        {
            Debug.LogError("[MainUIManager] Не удалось найти StartOfDayPanel на сцене!");
        }
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isTransitioning = true;
        
        if (TransitionManager.Instance != null)
        {
            yield return TransitionManager.Instance.TransitionToScene(sceneName);
        }
        else
        {
            Debug.LogWarning("TransitionManager не найден, сцена загружается без перехода.");
            yield return SceneManager.LoadSceneAsync(sceneName);
        }

        if (sceneName == gameSceneName)
        {
            StartOfDayPanel startOfDayPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            OrderSelectionUI orderSelectionUI = FindFirstObjectByType<OrderSelectionUI>(FindObjectsInactive.Include);
            DaySplashScreenController daySplashScreenController = FindFirstObjectByType<DaySplashScreenController>(FindObjectsInactive.Include);
            yield return StartCoroutine(UnveilSequence(startOfDayPanel, orderSelectionUI, daySplashScreenController));
        }
        else
        {
            isTransitioning = false;
        }
    }
    
    #region Остальные методы (без изменений)
    private IEnumerator UnveilSequence(StartOfDayPanel startOfDayPanel, OrderSelectionUI orderSelectionUI, DaySplashScreenController daySplashScreenController)
    {
        PauseGame(true);

        if (SaveLoadManager.Instance.isNewGame) { DirectorManager.Instance.ResetState(); } else { SaveLoadManager.Instance.LoadGame(SaveLoadManager.Instance.GetCurrentSlot()); }

        DirectorManager.Instance.PrepareDay();

        DirectorAvatarController directorController = FindFirstObjectByType<DirectorAvatarController>();
        if (directorController != null && directorController.directorChairPoint != null)
        {
            directorController.TeleportTo(directorController.directorChairPoint.position);
            directorController.ForceSetAtDeskState(true);
        }
        
        if (daySplashScreenController != null)
        {
            daySplashScreenController.gameObject.SetActive(true);
            daySplashScreenController.Setup(ClientSpawner.Instance.GetCurrentDay()); 
            daySplashScreenController.GetComponent<CanvasGroup>().alpha = 1f;
        }

        if (orderSelectionUI != null)
        {
            orderSelectionUI.gameObject.SetActive(true);
            orderSelectionUI.Setup();
            var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
            orderCG.alpha = 1f;
            orderCG.interactable = false;
            orderCG.blocksRaycasts = false;
        }

        yield return new WaitForSecondsRealtime(splashScreenDwellTime);

        if (daySplashScreenController != null) { yield return daySplashScreenController.Fade(false); }

        if (orderSelectionUI != null) {
            var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
            orderCG.interactable = true;
            orderCG.blocksRaycasts = true;
        }

        isTransitioning = false;
    }
    public void OnSaveSlotClicked(int slotIndex)
    {
        if (isTransitioning) return;
        SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
        SaveLoadManager.Instance.isNewGame = false;
        StartCoroutine(LoadSceneRoutine(gameSceneName));
    }
    public void OnNewGameClicked(int slotIndex)
    {
        if (isTransitioning) return;
        SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
        SaveLoadManager.Instance.isNewGame = true;
        SaveData newGameData = new SaveData { day = 1, money = 1000 };
        SaveLoadManager.Instance.SaveNewGame(slotIndex, newGameData);
        StartCoroutine(LoadSceneRoutine(gameSceneName));
    }
    public void StartOrResumeGameplay()
    {
        if (isTransitioning) return;
        StartCoroutine(StartGameplaySequence());
    }
    private IEnumerator StartGameplaySequence()
    {
        isTransitioning = true;
        Debug.Log("<color=lime>[MainUIManager] Начало последовательности StartGameplaySequence.</color>");
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        StartOfDayPanel sodp = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include); 
        if (sodp != null)
        {
            Debug.Log("<color=lime>[MainUIManager] Прячем StartOfDayPanel...</color>");
            yield return StartCoroutine(sodp.Fade(false, false));
        }

        Debug.Log("<color=lime>[MainUIManager] Снимаем игру с паузы.</color>");
        ResumeGame();
        HiringManager.Instance?.ActivateAllScheduledStaff();
        
        if (MusicPlayer.Instance != null)
        {
            Debug.Log("<color=lime>[MainUIManager] Включаем музыку геймплея.</color>");
            MusicPlayer.Instance.StartGameplayMusic();
        }
        
        isTransitioning = false;
        Debug.Log("<color=lime>[MainUIManager] Последовательность StartGameplaySequence завершена. Игровой день запущен.</color>");
    }
    public void ShowPausePanel(bool show)
    {
        if (isTransitioning) return;

        if (show)
        {
            PauseGame(false); 
            MusicPlayer.Instance?.PauseGameplayMusicForManualPause();
            if (pausePanel != null) pausePanel.SetActive(true);
        }
        else 
        { 
            ResumeGame();
            MusicPlayer.Instance?.ResumeGameplayMusicFromManualPause();
            if (pausePanel != null) pausePanel.SetActive(false);
        }
    }
    public void GoToMainMenu()
    {
        if (isTransitioning) return;
        ResumeGame();
        
        if (SceneManager.GetActiveScene().name == gameSceneName && 
            SaveLoadManager.Instance != null && 
            !SaveLoadManager.Instance.isNewGame) 
        {
            try
            {
                Debug.Log($"[MainUIManager] Автосохранение в слот {SaveLoadManager.Instance.GetCurrentSlot()} перед выходом в меню...");
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.GetCurrentSlot());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MainUIManager] Ошибка автосохранения при выходе в меню: {e.Message}");
            }
        }
        
        StartCoroutine(LoadSceneRoutine(mainMenuSceneName));
    }
    public void TriggerNextDayTransition()
    {
        if (isTransitioning) return;
        StartCoroutine(LoadSceneRoutine(gameSceneName));
    }
    public void PauseGame(bool playMusic = true)
    {
        Time.timeScale = 0f;
        if (playMusic && MusicPlayer.Instance != null) MusicPlayer.Instance.PauseGameplayMusicAndPlayOfficeTheme();
    }
    public void ResumeGame() { Time.timeScale = 1f; }
    #endregion
}