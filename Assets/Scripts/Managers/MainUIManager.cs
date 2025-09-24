using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainUIManager : MonoBehaviour
{
    // ... (Instance, поля, Awake остаются без изменений) ...
    #region Поля и Awake (без изменений)
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
    if (Instance == null) { Instance = this; }
    else if (Instance != this) { Destroy(gameObject); }
}
    #endregion

    /// <summary>
    /// Показывает панель стола директора, ставит игру на паузу и включает музыку кабинета.
    /// </summary>
    public void ShowDirectorDesk()
{
    if (isTransitioning) return;

    // Ищем панель, даже если она неактивна
    StartOfDayPanel deskPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include); // <-- ДОБАВЛЕНА ;

    if (deskPanel != null)
    {
        // Ставим игру на паузу и включаем музыку
        PauseGame(true);
        
        // Запускаем анимацию появления панели
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
        
        // <<< ВОЗВРАЩАЕМ ВЫЗОВ TRANSITION MANAGER >>>
        if (TransitionManager.Instance != null)
        {
            yield return TransitionManager.Instance.TransitionToScene(sceneName);
        }
        else
        {
            Debug.LogWarning("TransitionManager не найден, сцена загружается без перехода.");
            yield return SceneManager.LoadSceneAsync(sceneName);
        }

        // Этот код выполнится ПОСЛЕ того, как переход полностью завершился
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
    
    // ... (UnveilSequence и все остальные методы остаются без изменений) ...
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
        if (orderSelectionUI != null)
        {
            orderSelectionUI.gameObject.SetActive(true);
            orderSelectionUI.Setup();
            var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
            orderCG.alpha = 1f;
            orderCG.interactable = false;
            orderCG.blocksRaycasts = false;
        }
        if (daySplashScreenController != null)
        {
            daySplashScreenController.gameObject.SetActive(true);
            daySplashScreenController.Setup(ClientSpawner.Instance.GetCurrentDay());
            daySplashScreenController.GetComponent<CanvasGroup>().alpha = 1f;
        }
        yield return new WaitForSecondsRealtime(splashScreenDwellTime);
        if (daySplashScreenController != null) { yield return daySplashScreenController.Fade(false); }
        if (orderSelectionUI != null) { var orderCG = orderSelectionUI.GetComponent<CanvasGroup>(); orderCG.interactable = true; orderCG.blocksRaycasts = true; }
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
        SaveData newGameData = new SaveData { day = 1, money = 150 };
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

    // Находим и прячем панель стола директора
    StartOfDayPanel sodp = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include); // <-- ДОБАВЛЕНА ;
    if (sodp != null)
    {
        Debug.Log("<color=lime>[MainUIManager] Прячем StartOfDayPanel...</color>");
        yield return StartCoroutine(sodp.Fade(false, false));
    }

    // Снимаем игру с паузы
    Debug.Log("<color=lime>[MainUIManager] Снимаем игру с паузы.</color>");
    ResumeGame();
    
    // Включаем музыку геймплея
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
        PauseGame(false); // Ставим игру на паузу, но пока не трогаем музыку
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