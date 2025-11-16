using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class GameSceneUIManager : MonoBehaviour
{
    public static GameSceneUIManager Instance { get; set; }

    [Header("Настройки эффектов")]
    public float totalTransitionDuration = 2.0f;
    public GameObject transitionLeafPrefab;
    public AudioClip transitionSound;
    public int minLeavesToAnimate = 25;
    public int maxLeavesToAnimate = 50;
    public float staggerDelay = 0.01f;
    public float uiPanelFadeTime = 0.3f;
    
    [Header("Компоненты префаба")]
    public Image blackoutImage;
    public Transform leafTransitionContainer;
    public AudioSource uiAudioSource;
    public List<Transform> leafTargetPositions;

    // Ссылки на объекты сцены, которые найдутся автоматически
    private DaySplashScreenController daySplashScreenController;
    private StartOfDayPanel startOfDayPanel;
    private OrderSelectionUI orderSelectionUI;
    private GameObject inGameUIButtons;

    public bool isTransitioning { get; private set; } = false;
    private int currentSlotIndex; // Может понадобиться для сохранения

    void Start()
    {
        FindSceneReferences();
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.GetLastSavedSlot(out currentSlotIndex))
        {
            // Определяем, была ли это новая игра (в SaveLoadManager можно добавить флаг)
            bool isNewGame = false; // Упрощенно, нужно будет передавать это состояние из MenuManager
            StartCoroutine(UnveilSequence(isNewGame));
        }
    }
    
    private void FindSceneReferences()
    {
        daySplashScreenController = FindFirstObjectByType<DaySplashScreenController>(FindObjectsInactive.Include);
        startOfDayPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
        orderSelectionUI = FindFirstObjectByType<OrderSelectionUI>(FindObjectsInactive.Include);
        inGameUIButtons = GameObject.Find("InGameUIButtons");
    }
    
    public void GoToMainMenu()
    {
        if (isTransitioning) return;
        Time.timeScale = 1f;
        StartCoroutine(TransitionToMainMenuRoutine());
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

    public void StartOrResumeGameplay() 
    { 
        if (isTransitioning) return; 
        StartCoroutine(StartGameplaySequenceRoutine()); 
    }

    public void TriggerNextDayTransition() 
    { 
        if (isTransitioning) return; 
        StartCoroutine(NextDayTransitionRoutine()); 
    }
    
    public void ShowOrderSelection()
    {
        if (startOfDayPanel != null) StartCoroutine(startOfDayPanel.Fade(false, false));
        if (orderSelectionUI != null)
        {
            orderSelectionUI.Setup();
            StartCoroutine(orderSelectionUI.Fade(true));
        }
    }

    private IEnumerator UnveilSequence(bool isNewGame)
    {
        isTransitioning = true;
        
        startOfDayPanel.GetComponent<CanvasGroup>().alpha = 0;
        orderSelectionUI.GetComponent<CanvasGroup>().alpha = 0;

        daySplashScreenController.Setup(ClientSpawner.Instance.GetCurrentDay() + (isNewGame ? 1 : 0));
        daySplashScreenController.GetComponent<CanvasGroup>().alpha = 1f;

        yield return StartCoroutine(FadeBlackout(false, 1.0f));
        
        yield return new WaitForSecondsRealtime(2.0f);
        
        startOfDayPanel.UpdatePanelInfo();
        StartCoroutine(startOfDayPanel.Fade(true, true));
        
        yield return StartCoroutine(daySplashScreenController.Fade(false));
        
        bool hasActiveOrders = DirectorManager.Instance.activeOrders.Count > 0 || DirectorManager.Instance.activePermanentOrders.Count > 0;
        if (!hasActiveOrders)
        {
            orderSelectionUI.Setup();
            yield return StartCoroutine(orderSelectionUI.Fade(true));
        }

        isTransitioning = false;
    }

    private IEnumerator StartGameplaySequenceRoutine()
    {
        isTransitioning = true;
        yield return StartCoroutine(FadeBlackout(true, 0.2f));
        if(orderSelectionUI != null) StartCoroutine(orderSelectionUI.Fade(false));
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
        yield return StartCoroutine(FadeBlackout(true, 0.5f));
        
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