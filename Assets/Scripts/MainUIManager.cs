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
    
    private TransitionManager transitionManager;
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
            Debug.LogWarning($"Найден и уничтожен дубликат MainUIManager ({this.GetInstanceID()}).");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Debug.Log($"Главный MainUIManager ({this.GetInstanceID()}) был уничтожен и очистил ссылку на себя.");
        }
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isTransitioning = true;

        if (transitionManager == null)
        {
            transitionManager = FindFirstObjectByType<TransitionManager>();
        }
        
        if (transitionManager != null)
        {
            yield return transitionManager.StartTransition(true);
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        if (sceneName == "GameScene")
        {
            StartOfDayPanel startOfDayPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            OrderSelectionUI orderSelectionUI = FindFirstObjectByType<OrderSelectionUI>(FindObjectsInactive.Include);
            DaySplashScreenController daySplashScreenController = FindFirstObjectByType<DaySplashScreenController>(FindObjectsInactive.Include);
            GameObject inGameUIButtons = FindFirstObjectByType<InGameUI_Actions>(FindObjectsInactive.Include)?.gameObject;

            yield return StartCoroutine(UnveilSequence(startOfDayPanel, orderSelectionUI, daySplashScreenController, inGameUIButtons));
        }

        isTransitioning = false;
    }
    
    private IEnumerator UnveilSequence(StartOfDayPanel startOfDayPanel, OrderSelectionUI orderSelectionUI, DaySplashScreenController daySplashScreenController, GameObject inGameUIButtons)
    {
        PauseGame(true);

        if (SaveLoadManager.Instance.isNewGame)
        {
            DirectorManager.Instance.ResetState();
            PlayerWallet.Instance.ResetState();
            ClientSpawner.Instance.ResetState();
            ArchiveManager.Instance.ResetState();
            HiringManager.Instance.ResetState();
        }
        DirectorManager.Instance.PrepareDay();
        HiringManager.Instance.GenerateNewCandidates();

        if (DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.directorChairPoint != null)
        {
            DirectorAvatarController.Instance.TeleportTo(DirectorAvatarController.Instance.directorChairPoint.position);
            DirectorAvatarController.Instance.ForceSetAtDeskState(true);
        }

        if (inGameUIButtons != null) inGameUIButtons.SetActive(false);
        if (startOfDayPanel != null) startOfDayPanel.GetComponent<CanvasGroup>().alpha = 0;
        if (orderSelectionUI != null) orderSelectionUI.GetComponent<CanvasGroup>().alpha = 0;
        
        if (daySplashScreenController != null)
        {
            daySplashScreenController.Setup(ClientSpawner.Instance.GetCurrentDay() + 1);
            yield return daySplashScreenController.Fade(true);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return daySplashScreenController.Fade(false);
        }

        if (startOfDayPanel != null)
        {
            startOfDayPanel.UpdatePanelInfo();
            StartCoroutine(startOfDayPanel.Fade(true, true));
        }
        if (orderSelectionUI != null)
        {
            orderSelectionUI.Setup();
            StartCoroutine(orderSelectionUI.Fade(true));
        }

        if (transitionManager != null)
        {
            yield return transitionManager.StartTransition(false);
        }
    }
    
    public void OnSaveSlotClicked(int slotIndex)
    {
        if (isTransitioning) return;
        SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
        SaveLoadManager.Instance.isNewGame = false;
        StartCoroutine(LoadSceneRoutine("GameScene"));
    }

    public void OnNewGameClicked(int slotIndex)
    {
        if (isTransitioning) return;
        SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
        SaveLoadManager.Instance.isNewGame = true;
        StartCoroutine(LoadSceneRoutine("GameScene"));
    }

    #region Логика кнопок главного меню
    public void OnClick_Continue()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(true);
        SaveLoadManager.Instance.isNewGame = false;
    }

    public void OnClick_NewGame()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(true);
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
    #endregion
	
	public void TriggerNextDayTransition()
{
    if (isTransitioning) return;
    // Этот метод просто перезапускает игровую сцену.
    // Наша новая логика в LoadSceneRoutine сама подхватит
    // перезагрузку и запустит "церемонию" нового дня.
    StartCoroutine(LoadSceneRoutine("GameScene"));
}
    
	public void GoToMainMenu()
{
    // Этот метод просто вызывает уже существующую у нас логику
    // загрузки главного меню.
    StartCoroutine(LoadSceneRoutine("MainMenuScene"));
}
	
	
    #region Управление UI панелями

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
    
    #region Управление временем и геймплеем
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
    }
    #endregion
}