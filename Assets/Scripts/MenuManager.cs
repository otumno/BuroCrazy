// Файл: MenuManager.cs (ФИНАЛЬНАЯ ВЕРСИЯ)
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Панели UI (назначаются в MainMenuScene)")]
    public GameObject mainMenuPanel;
    public GameObject saveSelectionPanel;
    
    [Header("UI для сохранения (назначаются в MainMenuScene)")]
    public SaveSlotUI[] saveSlots;
    public Button continueButton;
    
    [Header("Эффект с листами для смены сцен")]
    public float totalTransitionDuration = 2.5f;
    public GameObject transitionLeafPrefab;
    public AudioClip transitionSound;
    public List<Transform> leafTargetPositions;
    public int minLeavesToAnimate = 25;
    public int maxLeavesToAnimate = 50;
    public float staggerDelay = 0.02f;
    
    [Header("Ссылки на компоненты (должны быть частью этого префаба)")]
    public float uiPanelFadeTime = 0.5f;
    public Image blackoutImage;
    public Transform leafTransitionContainer;
    public AudioSource uiAudioSource;

    [Header("Ссылки на другие менеджеры")]
    public MusicPlayer musicPlayer;
    public SaveLoadManager saveLoadManager;
    public DirectorManager directorManager;

    private GameObject startOfDayPanel, orderSelectionPanel, inGameUIButtons;
    private OrderSelectionUI orderSelectionUI;
    private StartOfDayPanel startOfDayPanelScript;
    private GameObject currentPanel;
    private bool isTransitioning = false;
    private int lastLoadedSlotIndex = -1;
    private bool isPausedForMenu = false;
    private int slotIndexToLoad;
    private bool isNewGameRequest = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (musicPlayer == null) musicPlayer = GetComponentInChildren<MusicPlayer>();
        if (saveLoadManager == null) saveLoadManager = GetComponentInChildren<SaveLoadManager>();
        if (directorManager == null) directorManager = GetComponentInChildren<DirectorManager>();
        
        if (leafTargetPositions.Count == 0 && transform.Find("LeafTransitionContainer/LeafTargets") != null)
        {
            var targetsContainer = transform.Find("LeafTransitionContainer/LeafTargets");
            foreach (Transform child in targetsContainer) { leafTargetPositions.Add(child); }
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene")
        {
            startOfDayPanelScript = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            if (startOfDayPanelScript != null) startOfDayPanel = startOfDayPanelScript.gameObject;
            orderSelectionUI = FindFirstObjectByType<OrderSelectionUI>(FindObjectsInactive.Include);
            if (orderSelectionUI != null) orderSelectionPanel = orderSelectionUI.gameObject;
            inGameUIButtons = GameObject.Find("InGameUIButtons");
            
            if(startOfDayPanel != null) startOfDayPanel.SetActive(false);
            if(orderSelectionPanel != null) orderSelectionPanel.SetActive(false);
            if(inGameUIButtons != null) inGameUIButtons.SetActive(false);
        }
        else if (scene.name == "MainMenuScene")
        {
            mainMenuPanel = GameObject.Find("MainMenuPanel");
            saveSelectionPanel = GameObject.Find("SaveSelectionPanel");
            Start();
        }
    }

    private void Start()
    {
        if(SceneManager.GetActiveScene().name != "MainMenuScene") return;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (saveSelectionPanel != null) saveSelectionPanel.SetActive(false);
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.GetLastSavedSlot(out lastLoadedSlotIndex))
        {
            if (continueButton != null) continueButton.gameObject.SetActive(true);
        }
        else { if (continueButton != null) continueButton.gameObject.SetActive(false); }
        currentPanel = mainMenuPanel;
    }

    public void OnContinueClicked() { if(SaveLoadManager.Instance.GetLastSavedSlot(out lastLoadedSlotIndex)) { OnSaveSlotClicked(lastLoadedSlotIndex); } }
    public void OnStartGameClicked() { if (saveSelectionPanel == null) return; mainMenuPanel.SetActive(false); saveSelectionPanel.SetActive(true); currentPanel = saveSelectionPanel; for (int i = 0; i < saveSlots.Length; i++) { if(saveSlots[i] != null) saveSlots[i].Setup(i, this, SaveLoadManager.Instance); } }
    public void OnSaveSlotClicked(int slotIndex) { slotIndexToLoad = slotIndex; isNewGameRequest = false; StartCoroutine(MasterTransitionRoutine("GameScene")); }
    public void OnNewGameClicked(int slotIndex) { SaveLoadManager.Instance.DeleteSave(slotIndex); slotIndexToLoad = slotIndex; isNewGameRequest = true; StartCoroutine(MasterTransitionRoutine("GameScene")); }
    public void OnBackToMainMenuClicked() { Time.timeScale = 1f; StartCoroutine(MasterTransitionRoutine("MainMenuScene")); }
    
    private IEnumerator MasterTransitionRoutine(string sceneName)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;
        List<GameObject> activeLeaves = new List<GameObject>();
        int leavesCount = Random.Range(minLeavesToAnimate, maxLeavesToAnimate + 1);
        
        yield return StartCoroutine(FadeOutPhase(leavesCount, activeLeaves));
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        yield return new WaitUntil(() => asyncLoad.isDone);
        
        // ИСПРАВЛЕНИЕ: Правильная последовательность
        if (sceneName == "GameScene")
        {
            // Сначала готовим стол "за кулисами"
            yield return StartCoroutine(PostSceneLoadRoutine());
        }
        
        // А теперь рассеиваем переход, чтобы показать готовый стол
        yield return StartCoroutine(FadeInPhase(activeLeaves));

        isTransitioning = false;
    }

    private IEnumerator FadeOutPhase(int leavesCount, List<GameObject> leaves)
    {
        if (uiAudioSource != null && transitionSound != null) uiAudioSource.PlayOneShot(transitionSound);
        StartCoroutine(FadeBlackout(true, totalTransitionDuration));
        leavesCount = Mathf.Min(leavesCount, leafTargetPositions.Count);
        List<Transform> shuffledTargets = leafTargetPositions.OrderBy(t => Random.value).ToList();
        for (int i = 0; i < leavesCount; i++)
        {
            GameObject leaf = Instantiate(transitionLeafPrefab, leafTransitionContainer);
            leaves.Add(leaf);
            Vector3 startPos = GetRandomOffscreenPosition();
            Vector3 targetPos = shuffledTargets[i].position;
            float flightDuration = Random.Range(totalTransitionDuration * 0.6f, totalTransitionDuration);
            var leafScript = leaf.GetComponent<FallingLeaf>();
            StartCoroutine(leafScript.Animate(startPos, targetPos, flightDuration, true, false));
            yield return new WaitForSecondsRealtime(staggerDelay);
        }
        yield return new WaitForSecondsRealtime(totalTransitionDuration);
    }

    private IEnumerator FadeInPhase(List<GameObject> leaves)
    {
        yield return new WaitForSecondsRealtime(0.2f);
        if (uiAudioSource != null && transitionSound != null) uiAudioSource.PlayOneShot(transitionSound);
        StartCoroutine(FadeBlackout(false, totalTransitionDuration));
        foreach (GameObject leaf in leaves)
        {
            if (leaf == null) continue;
            Vector3 startPos = leaf.transform.position;
            Vector3 endPos = GetRandomOffscreenPosition();
            float flightDuration = Random.Range(totalTransitionDuration * 0.6f, totalTransitionDuration);
            var leafScript = leaf.GetComponent<FallingLeaf>();
            if(leafScript != null) StartCoroutine(leafScript.Animate(startPos, endPos, flightDuration, false, true));
            yield return new WaitForSecondsRealtime(staggerDelay);
        }
        yield return new WaitForSecondsRealtime(totalTransitionDuration + 1.0f);
        foreach (GameObject leaf in leaves) { if(leaf != null) Destroy(leaf); }
    }

    private IEnumerator PostSceneLoadRoutine()
    {
        if (isNewGameRequest) { ClientSpawner.Instance?.ResetState(); PlayerWallet.Instance?.ResetState(); ArchiveManager.Instance?.ResetState(); DirectorManager.Instance?.ResetState(); SaveLoadManager.Instance.SaveGame(slotIndexToLoad); }
        else { if (!SaveLoadManager.Instance.LoadGame(slotIndexToLoad)) { yield break; } }
        
        Time.timeScale = 0f;
        MusicPlayer.Instance?.PlayDirectorsOfficeTheme();
        DirectorManager.Instance.PrepareDay();
        if (startOfDayPanel != null)
        {
            startOfDayPanel.SetActive(true);
            currentPanel = startOfDayPanel;
        }
        if (orderSelectionPanel != null)
        {
            orderSelectionPanel.SetActive(true);
            if (orderSelectionUI != null) orderSelectionUI.SetupAndAnimate();
        }
        yield return null;
    }
    
    public IEnumerator StartGameplaySequence()
    {
        isTransitioning = true;
        
        // ИСПРАВЛЕНИЕ: Используем простой и надежный Fade-эффект
        yield return StartCoroutine(FadeBlackout(true, uiPanelFadeTime));

        if(orderSelectionPanel != null) orderSelectionPanel.SetActive(false);
        if(startOfDayPanel != null) startOfDayPanel.SetActive(false);
        currentPanel = null;
        MusicPlayer.Instance?.PlayGameplayMusic();
        if(inGameUIButtons != null) inGameUIButtons.SetActive(true);
        
        // Рассеиваем черноту, чтобы показать игровой мир
        yield return StartCoroutine(FadeBlackout(false, uiPanelFadeTime));
        
        Time.timeScale = 1f;
        isPausedForMenu = false;
        isTransitioning = false;
    }

    public void OnFinalStartDayClicked() { StartCoroutine(StartGameplaySequence()); }
    
    // ... (остальные методы без изменений) ...
    private Vector3 GetRandomOffscreenPosition() { Vector3 position; int side = Random.Range(0, 4); float padding = 100f; RectTransform canvasRect = leafTransitionContainer.root.GetComponent<RectTransform>(); float screenWidth = canvasRect.rect.width; float screenHeight = canvasRect.rect.height; if (side == 0) position = new Vector3(Random.Range(-padding, screenWidth + padding), screenHeight + padding, 0); else if (side == 1) position = new Vector3(Random.Range(-padding, screenWidth + padding), -padding, 0); else if (side == 2) position = new Vector3(-padding, Random.Range(-padding, screenHeight + padding), 0); else position = new Vector3(screenWidth + padding, Random.Range(-padding, screenHeight + padding), 0); return position; }
    public void ToggleSimplePauseMenu() { if (isTransitioning) return; isPausedForMenu = !isPausedForMenu; Time.timeScale = isPausedForMenu ? 0f : 1f; if (isPausedForMenu) { MusicPlayer.Instance?.PlayDirectorsOfficeTheme(); StartCoroutine(SimpleFadeTransition(startOfDayPanel)); } else { MusicPlayer.Instance?.PlayGameplayMusic(); StartCoroutine(SimpleFadeTransition(null)); } }
    public void OnStartDayClicked() { if (isTransitioning) return; if (DirectorManager.Instance != null && (DirectorManager.Instance.activeOrders.Count > 0 || DirectorManager.Instance.activePermanentOrders.Count > 0)) { OnFinalStartDayClicked(); } else { DirectorManager.Instance?.PrepareDay(); if (currentPanel != null) currentPanel.SetActive(false); if (orderSelectionPanel != null) orderSelectionPanel.SetActive(true); if (orderSelectionUI != null) orderSelectionUI.SetupAndAnimate(); currentPanel = orderSelectionPanel; } }
    public void TriggerEndOfDaySequence() { if (isTransitioning) return; Time.timeScale = 0f; MusicPlayer.Instance?.PlayDirectorsOfficeTheme(); if (inGameUIButtons != null) inGameUIButtons.SetActive(false); if (orderSelectionPanel != null) orderSelectionPanel.SetActive(false); StartCoroutine(SimpleFadeTransition(startOfDayPanel)); }
    private IEnumerator SimpleFadeTransition(GameObject panelToShow) { isTransitioning = true; yield return StartCoroutine(FadeBlackout(true, uiPanelFadeTime)); if (currentPanel != null) currentPanel.SetActive(false); if(inGameUIButtons != null) inGameUIButtons.SetActive(false); if (panelToShow != null) { panelToShow.SetActive(true); currentPanel = panelToShow; if (startOfDayPanelScript != null) startOfDayPanelScript.UpdatePanelInfo(); } else { currentPanel = null; if(inGameUIButtons != null) inGameUIButtons.SetActive(true); } yield return StartCoroutine(FadeBlackout(false, uiPanelFadeTime)); isTransitioning = false; }
    private IEnumerator FadeBlackout(bool fadeIn, float duration) { if (blackoutImage == null) { yield break; } blackoutImage.gameObject.SetActive(true); float timer = 0f; float startAlpha = fadeIn ? 0f : 1f; float endAlpha = fadeIn ? 1f : 0f; Color color = blackoutImage.color; while(timer < duration) { float progress = timer / duration; color.a = Mathf.Lerp(startAlpha, endAlpha, progress); blackoutImage.color = color; timer += Time.unscaledDeltaTime; yield return null; } color.a = endAlpha; blackoutImage.color = color; if (!fadeIn) { blackoutImage.gameObject.SetActive(false); } }
    public void OnExitGameClicked() { Application.Quit(); }
}