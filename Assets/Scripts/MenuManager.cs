// Файл: MenuManager.cs - Финальная версия
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Настройки эффектов")]
    public float totalTransitionDuration = 2.0f;
    public GameObject transitionLeafPrefab;
    public AudioClip transitionSound;
    public int minLeavesToAnimate = 25;
    public int maxLeavesToAnimate = 50;
    public float staggerDelay = 0.01f;
    public float uiPanelFadeTime = 0.3f;
	
	[Header("Звуки UI")]
	public AudioClip ordersPanelAppearSound;
    
    [Header("Компоненты префаба")]
    public Image blackoutImage;
    public Transform leafTransitionContainer;
    public AudioSource uiAudioSource;

    private Button continueButton;
    private GameObject mainMenuPanel;
    private GameObject saveSelectionPanel;
    private SaveSlotUI[] saveSlots;
    private DaySplashScreenController daySplashScreenController;
    private StartOfDayPanel startOfDayPanel;
    private OrderSelectionUI orderSelectionUI;
    private GameObject inGameUIButtons;
    private List<Transform> leafTargetPositions = new List<Transform>();

    public bool isTransitioning { get; private set; } = false;
    private int currentSlotIndex;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
		
		HiringManager.Instance = GetComponent<HiringManager>();
		ExperienceManager.Instance = GetComponent<ExperienceManager>();
		PayrollManager.Instance = GetComponent<PayrollManager>();
		DirectorManager.Instance = GetComponent<DirectorManager>();
		SaveLoadManager.Instance = GetComponent<SaveLoadManager>();
		MusicPlayer.Instance = GetComponent<MusicPlayer>();
		
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindSceneReferences();
        if (scene.name == "MainMenuScene")
        {
            UpdateMainMenu();
            Time.timeScale = 1f;
        }
    }

    private void FindSceneReferences()
    {
        leafTargetPositions.Clear();
        if (leafTransitionContainer != null && leafTransitionContainer.Find("LeafTargets") is Transform targetsContainer)
        {
            foreach (Transform child in targetsContainer) { leafTargetPositions.Add(child); }
        }

        if (SceneManager.GetActiveScene().name == "GameScene")
        {
            daySplashScreenController = FindFirstObjectByType<DaySplashScreenController>(FindObjectsInactive.Include);
            startOfDayPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            orderSelectionUI = FindFirstObjectByType<OrderSelectionUI>(FindObjectsInactive.Include);
            inGameUIButtons = GameObject.Find("InGameUIButtons");
        }
        else if (SceneManager.GetActiveScene().name == "MainMenuScene")
        {
            mainMenuPanel = GameObject.Find("MainMenuPanel");
            saveSelectionPanel = GameObject.Find("SaveSelectionPanel");
            if (mainMenuPanel != null)
                continueButton = mainMenuPanel.transform.Find("ContinueButton")?.GetComponent<Button>();
            if (saveSelectionPanel != null)
                saveSlots = saveSelectionPanel.GetComponentsInChildren<SaveSlotUI>(true);
        }
    }
    
    private void UpdateMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (saveSelectionPanel != null) saveSelectionPanel.SetActive(false);
        bool canContinue = SaveLoadManager.Instance != null && SaveLoadManager.Instance.GetLastSavedSlot(out _);
        if (continueButton != null) continueButton.gameObject.SetActive(canContinue);
    }
    
    public void OnContinueClicked() { if(SaveLoadManager.Instance.GetLastSavedSlot(out int slot)) { StartGame(slot, false); } }
    
    public void OnStartGameClicked() 
    { 
        if (saveSelectionPanel == null) return; 
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false); 
        saveSelectionPanel.SetActive(true);
        if (saveSlots == null) return;
        for (int i = 0; i < saveSlots.Length; i++) { if(saveSlots[i] != null) saveSlots[i].Setup(i, this, SaveLoadManager.Instance); }
    }
    
    public void OnSaveSlotClicked(int slotIndex) { StartGame(slotIndex, false); }
    public void OnNewGameClicked(int slotIndex) { StartGame(slotIndex, true); }
    public void OnCabinetButtonClick() { ShowPausePanel(); }
	
    public void StartOrResumeGameplay()
{
    if (isTransitioning) return;
    
    // Мгновенно выключаем панель приказов, чтобы она не "моргнула"
    if (orderSelectionUI != null)
    {
        orderSelectionUI.gameObject.SetActive(false);
    }

    StartCoroutine(StartGameplaySequenceRoutine());
}
    
	public void TriggerNextDayTransition() { if (isTransitioning) return; StartCoroutine(NextDayTransitionRoutine()); }
    
    public void GoToMainMenu()
    {
        if (isTransitioning) return;
        Time.timeScale = 1f;
        StartCoroutine(TransitionToMainMenuRoutine());
    }

    public void ShowOrderSelection()
    {
        // ИСПРАВЛЕНО: Добавлен второй аргумент 'false'
        if (startOfDayPanel != null) StartCoroutine(startOfDayPanel.Fade(false, false));
        if (orderSelectionUI != null)
        {
            orderSelectionUI.Setup();
            StartCoroutine(orderSelectionUI.Fade(true));
        }
    }
    
    private void StartGame(int slotIndex, bool isNewGame)
    {
        if (isTransitioning) return;
        StartCoroutine(StartGameFromMenuRoutine(slotIndex, isNewGame));
    }
    
    public void ShowPausePanel()
    {
        if (isTransitioning) return;
        Time.timeScale = 0f;
        MusicPlayer.Instance?.PauseGameplayMusicAndPlayOfficeTheme();
        if (inGameUIButtons != null) inGameUIButtons.SetActive(false);
        if (startOfDayPanel != null)
        {
            startOfDayPanel.UpdatePanelInfo();
            StartCoroutine(startOfDayPanel.Fade(true, true)); 
        }
    }

    private IEnumerator StartGameFromMenuRoutine(int slotIndex, bool isNewGame)
    {
        isTransitioning = true;
        currentSlotIndex = slotIndex;
        yield return StartCoroutine(FadeOutPhase());
        
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("GameScene");
        yield return new WaitUntil(() => asyncLoad.isDone);
        yield return null;
        
        Time.timeScale = 0f;
        
        FindSceneReferences();
        if (daySplashScreenController == null || startOfDayPanel == null || orderSelectionUI == null)
        {
            isTransitioning = false;
            yield break;
        }

        if (isNewGame)
        {
            SaveLoadManager.Instance.DeleteSave(slotIndex);
            DirectorManager.Instance.ResetState();
            PlayerWallet.Instance.ResetState();
            ClientSpawner.Instance.ResetState();
            ArchiveManager.Instance.ResetState();
        }
        else
        {
            SaveLoadManager.Instance.LoadGame(slotIndex);
        }
        
        DirectorManager.Instance.PrepareDay();
        if (DirectorAvatarController.Instance?.directorChairPoint != null)
        {
            DirectorAvatarController.Instance.transform.position = DirectorAvatarController.Instance.directorChairPoint.position;
            DirectorAvatarController.Instance.ForceSetAtDeskState(true);
        }
        
        StartCoroutine(UnveilSequence(isNewGame));
        
        isTransitioning = false;
    }

private IEnumerator UnveilSequence(bool isNewGame)
{
    // 1. Готовим сцену "за кулисами"
    startOfDayPanel.GetComponent<CanvasGroup>().alpha = 0;
    orderSelectionUI.GetComponent<CanvasGroup>().alpha = 0;

    // 2. Убираем черный экран
    yield return StartCoroutine(FadeBlackout(false, 1.0f));

    // 3. Показываем "День N"
    daySplashScreenController.Setup(ClientSpawner.Instance.GetCurrentDay() + 1);
    yield return StartCoroutine(daySplashScreenController.Fade(true));
    yield return new WaitForSecondsRealtime(2.0f);

    // 4. Прячем "День N"
    StartCoroutine(daySplashScreenController.Fade(false));

    // 5. ОДНОВРЕМЕННО показываем стол и приказы (если они нужны)
    startOfDayPanel.UpdatePanelInfo();
    StartCoroutine(startOfDayPanel.Fade(true, true)); // Показываем стол

    bool hasActiveOrders = DirectorManager.Instance.activeOrders.Count > 0 || DirectorManager.Instance.activePermanentOrders.Count > 0;
    if (!hasActiveOrders)
    {
        if (uiAudioSource != null && ordersPanelAppearSound != null)
        {
            uiAudioSource.PlayOneShot(ordersPanelAppearSound);
        }
        orderSelectionUI.Setup();
        yield return StartCoroutine(orderSelectionUI.Fade(true)); // Показываем приказы поверх стола
    }
}

    private IEnumerator StartGameplaySequenceRoutine()
    {
        isTransitioning = true;
        yield return StartCoroutine(FadeBlackout(true, 0.2f));
        if(orderSelectionUI != null) StartCoroutine(orderSelectionUI.Fade(false));
        
        // ИСПРАВЛЕНО: Добавлен второй аргумент 'false'
        if(startOfDayPanel != null) StartCoroutine(startOfDayPanel.Fade(false, false));

        MusicPlayer.Instance?.ResumeGameplayMusic();
        if(inGameUIButtons != null) inGameUIButtons.SetActive(true);
        yield return StartCoroutine(FadeBlackout(false, 0.3f));
        Time.timeScale = 1f;
        isTransitioning = false;
    }

    private IEnumerator NextDayTransitionRoutine()
    {
        isTransitioning = true;
        Time.timeScale = 0f;
        if (inGameUIButtons != null) inGameUIButtons.SetActive(false);
        if (leafTransitionContainer != null) leafTransitionContainer.gameObject.SetActive(true);
        yield return StartCoroutine(FadeOutPhase());
        SaveLoadManager.Instance.SaveGame(currentSlotIndex);
        DirectorManager.Instance.CheckDailyMandates();
        ClientSpawner.Instance.GoToNextPeriod();
        DirectorManager.Instance.PrepareDay();
        if (DirectorAvatarController.Instance?.directorChairPoint != null)
        {
            DirectorAvatarController.Instance.transform.position = DirectorAvatarController.Instance.directorChairPoint.position;
            DirectorAvatarController.Instance.ForceSetAtDeskState(true);
        }
        
        StartCoroutine(UnveilSequence(false));
        
        isTransitioning = false;
    }
    
    private IEnumerator TransitionToMainMenuRoutine()
    {
        isTransitioning = true;
        yield return StartCoroutine(FadeOutPhase());
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("MainMenuScene");
        yield return new WaitUntil(() => asyncLoad.isDone);
        isTransitioning = false;
    }
    
    private IEnumerator FadeOutPhase()
    {
        if (uiAudioSource != null && transitionSound != null) uiAudioSource.PlayOneShot(transitionSound);
        StartCoroutine(FadeBlackout(true, totalTransitionDuration));
        int leavesCount = Random.Range(minLeavesToAnimate, maxLeavesToAnimate);
        for (int i = 0; i < leavesCount; i++)
        {
            if (leafTransitionContainer != null && transitionLeafPrefab != null && leafTargetPositions.Count > 0)
            {
                GameObject leaf = Instantiate(transitionLeafPrefab, leafTransitionContainer);
                Vector3 startPos = GetRandomOffscreenPosition();
                Vector3 targetPos = leafTargetPositions[Random.Range(0, leafTargetPositions.Count)].position;
                float flightDuration = Random.Range(totalTransitionDuration * 0.7f, totalTransitionDuration);
                leaf.GetComponent<FallingLeaf>()?.StartCoroutine(leaf.GetComponent<FallingLeaf>().Animate(startPos, targetPos, flightDuration, true, false));
            }
            yield return new WaitForSecondsRealtime(staggerDelay);
        }
        yield return new WaitForSecondsRealtime(totalTransitionDuration);
    }

    private IEnumerator FadeBlackout(bool fadeIn, float duration)
    {
        if (blackoutImage == null) { yield break; }
        blackoutImage.gameObject.SetActive(true);
        float timer = 0f;
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        Color color = blackoutImage.color;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            color.a = Mathf.Lerp(startAlpha, endAlpha, progress);
            blackoutImage.color = color;
            yield return null;
        }
        color.a = endAlpha;
        blackoutImage.color = color;
        if (!fadeIn) { blackoutImage.gameObject.SetActive(false); }
    }

    private Vector3 GetRandomOffscreenPosition()
    {
        Vector3 position;
        int side = Random.Range(0, 4);
        float padding = 200f;
        if (leafTransitionContainer?.root == null) return Vector3.zero;
        RectTransform canvasRect = leafTransitionContainer.root.GetComponent<RectTransform>();
        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;
        if (side == 0) position = new Vector3(Random.Range(0, screenWidth), screenHeight + padding, 0);
        else if (side == 1) position = new Vector3(Random.Range(0, screenWidth), -padding, 0);
        else if (side == 2) position = new Vector3(-padding, Random.Range(0, screenHeight), 0);
        else position = new Vector3(screenWidth + padding, Random.Range(0, screenHeight), 0);
        return position;
    }
}