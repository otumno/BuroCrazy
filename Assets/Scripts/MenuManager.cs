// Файл: MenuManager.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class MenuManager : MonoBehaviour
{
    [Header("Панели UI")]
    public GameObject mainMenuPanel;
    public GameObject startOfDayPanel;
    public GameObject saveSelectionPanel;
    public GameObject orderSelectionPanel;

    [Header("UI для сохранения")]
    public SaveSlotUI[] saveSlots;
    public Button continueButton;

    [Header("Кнопки в игровом UI")]
    public GameObject inGameUIButtons;
    
    [Header("Настройки перехода")]
    public GameObject paperPrefab;
    public List<Sprite> paperSprites;
    public Image blackoutImage;
    public Transform canvasTransform;
    public List<Transform> paperFallTargets;
    public int numberOfPapers = 7;
    public float transitionTime = 0.6f;
    public AudioClip paperSwooshSound;
    public AudioSource uiAudioSource;

    [Header("Ссылки на другие менеджеры")]
    public MusicPlayer musicPlayer;
    public SaveLoadManager saveLoadManager;
    public OrderSelectionUI orderSelectionUI;
    
    public DirectorManager directorManager;
    public StartOfDayPanel startOfDayPanelScript;

    private GameObject currentPanel;
    private bool isTransitioning = false;
    private int lastLoadedSlotIndex = -1;
    private bool isPausedForMenu = false;

    private void Start()
    {
        mainMenuPanel.SetActive(true);
        saveSelectionPanel.SetActive(false);
        startOfDayPanel.SetActive(false);
        inGameUIButtons.SetActive(false);
        if (orderSelectionPanel != null) orderSelectionPanel.SetActive(false);
        
        if (saveLoadManager.GetLastSavedSlot(out lastLoadedSlotIndex))
        {
            continueButton.gameObject.SetActive(true);
        }
        else
        {
            continueButton.gameObject.SetActive(false);
        }
        
        Time.timeScale = 0f;
        musicPlayer?.PlayMenuTheme();
        currentPanel = mainMenuPanel;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isTransitioning && currentPanel == null && !isPausedForMenu) 
            {
                ToggleSimplePauseMenu();
            }
            else if (!isTransitioning && isPausedForMenu)
            {
                ToggleSimplePauseMenu();
            }
        }
    }
    
    public void ToggleSimplePauseMenu()
    {
        if (isTransitioning) return;

        isPausedForMenu = !isPausedForMenu;
        Time.timeScale = isPausedForMenu ? 0f : 1f;

        if (isPausedForMenu)
        {
            musicPlayer?.PlayDirectorsOfficeTheme();
            StartCoroutine(SimpleFadeTransition(startOfDayPanel));
        }
        else
        {
            musicPlayer?.PlayGameplayMusic();
            StartCoroutine(SimpleFadeTransition(null));
        }
    }

    private IEnumerator SimpleFadeTransition(GameObject panelToShow)
    {
        isTransitioning = true;
        float fadeDuration = 0.3f;

        yield return StartCoroutine(FadeBlackout(true, fadeDuration));

        if (currentPanel != null) currentPanel.SetActive(false);
        inGameUIButtons.SetActive(false);

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
            currentPanel = panelToShow;
            if (startOfDayPanelScript != null) startOfDayPanelScript.UpdatePanelInfo();
        }
        else
        {
            currentPanel = null;
            inGameUIButtons.SetActive(true);
        }

        yield return StartCoroutine(FadeBlackout(false, fadeDuration));
        isTransitioning = false;
    }

    private void HandleGameLoad(int slotIndex)
    {
        if (!saveLoadManager.LoadGame(slotIndex))
        {
            Debug.LogError($"Не удалось загрузить игру из слота {slotIndex}!");
            return;
        }
        musicPlayer?.PlayDirectorsOfficeTheme();
        StartCoroutine(LoadAndShowOrdersRoutine());
    }
    
    private IEnumerator LoadAndShowOrdersRoutine()
    {
        yield return StartCoroutine(TransitionRoutine(startOfDayPanel));

        if (directorManager.activeOrders.Count == 0)
        {
            Debug.Log("День еще не начат, показываю панель приказов поверх стола.");
            directorManager.PrepareDay();
            
            orderSelectionPanel.SetActive(true); 
            orderSelectionUI.SetupAndAnimate();
        }
    }

    public void OnContinueClicked() 
    { 
        if(saveLoadManager.GetLastSavedSlot(out lastLoadedSlotIndex)) 
        {
            HandleGameLoad(lastLoadedSlotIndex);
        } 
    }

    public void OnStartGameClicked() 
    { 
        for (int i = 0; i < saveSlots.Length; i++) { if(saveSlots[i] != null) saveSlots[i].Setup(i, this, saveLoadManager); } 
        TransitionTo(saveSelectionPanel); 
    }

    public void OnSaveSlotClicked(int slotIndex) 
    { 
        HandleGameLoad(slotIndex);
    }
    
    public void OnNewGameClicked(int slotIndex) 
    {
        ClientSpawner.Instance?.ResetState();
        PlayerWallet.Instance?.ResetState();
        ArchiveManager.Instance?.ResetState();
        directorManager?.ResetState();
        
        saveLoadManager.SaveGame(slotIndex);
        musicPlayer?.PlayDirectorsOfficeTheme();
        
        StartCoroutine(LoadAndShowOrdersRoutine());
    }
    
    public void OnStartDayClicked()
    {
        if (directorManager.activeOrders.Count > 0)
        {
            OnFinalStartDayClicked();
        }
        else
        {
            directorManager.PrepareDay();
            orderSelectionPanel.SetActive(true);
            orderSelectionUI.SetupAndAnimate();
        }
    }
    
    public void OnOpenDirectorsOfficeClicked()
    {
        Time.timeScale = 0f;
        musicPlayer?.PlayDirectorsOfficeTheme();
        inGameUIButtons.SetActive(false);
        orderSelectionPanel.SetActive(false);
        TransitionTo(startOfDayPanel);
    }

    public void OnBackToMainMenuClicked() 
    { 
        if(saveLoadManager.GetLastSavedSlot(out lastLoadedSlotIndex)) { saveLoadManager.SaveGame(lastLoadedSlotIndex); } else { saveLoadManager.SaveGame(0); } 
        if (saveLoadManager.GetLastSavedSlot(out int lastSaveSlot)) { lastLoadedSlotIndex = lastSaveSlot; continueButton.gameObject.SetActive(true); } 
        musicPlayer?.PlayMenuTheme(); 
        TransitionTo(mainMenuPanel); 
    }
    
    public void OnExitGameClicked()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    public void OnFinalStartDayClicked()
    {
        // --- ИЗМЕНЕНИЕ: Сбрасываем флаг паузы ---
        isPausedForMenu = false;

        Time.timeScale = 1f;
        musicPlayer?.PlayGameplayMusic();
        TransitionTo(null);
        if (inGameUIButtons != null) { inGameUIButtons.SetActive(true); }
        if (directorManager.activeOrders.Count == 0) { Debug.LogError("КРИТИЧЕСКАЯ ОШИБКА: День начался без выбранного приказа!"); }
        if (saveLoadManager.GetLastSavedSlot(out int lastSlot)) { saveLoadManager.SaveGame(lastSlot); } else { saveLoadManager.SaveGame(0); }
    }

    private void TransitionTo(GameObject panelToShow)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(panelToShow));
    }
    
    private IEnumerator TransitionRoutine(GameObject newPanel)
    {
        isTransitioning = true;
        StartCoroutine(FadeBlackout(true, transitionTime));
        if (uiAudioSource != null && paperSwooshSound != null)
            uiAudioSource.PlayOneShot(paperSwooshSound);

        List<GameObject> papers = new List<GameObject>();
        for (int i = 0; i < numberOfPapers; i++)
        {
            Transform targetPoint = paperFallTargets[Random.Range(0, paperFallTargets.Count)];
            Sprite paperSprite = paperSprites[Random.Range(0, paperSprites.Count)];
            GameObject paper = Instantiate(paperPrefab, canvasTransform);
            paper.GetComponent<Image>().sprite = paperSprite;
            papers.Add(paper);
            StartCoroutine(MovePaperRoutine(paper.transform, targetPoint.position, true));
            yield return new WaitForSecondsRealtime(transitionTime / numberOfPapers);
        }

        yield return new WaitForSecondsRealtime(transitionTime);
        if (currentPanel != null) currentPanel.SetActive(false);
        
        if (newPanel != null) newPanel.SetActive(true);

        currentPanel = newPanel;
        inGameUIButtons.SetActive(currentPanel == null);

        StartCoroutine(FadeBlackout(false, transitionTime));
        if (uiAudioSource != null && paperSwooshSound != null)
            uiAudioSource.PlayOneShot(paperSwooshSound);
        foreach (var paper in papers)
        {
            if (paper != null)
                StartCoroutine(MovePaperRoutine(paper.transform, Vector3.zero, false));
            yield return new WaitForSecondsRealtime(transitionTime / numberOfPapers);
        }

        yield return new WaitForSecondsRealtime(transitionTime);
        foreach (var paper in papers) { if (paper != null) Destroy(paper); }

        isTransitioning = false;
        if (newPanel == startOfDayPanel && startOfDayPanelScript != null)
        {
            startOfDayPanelScript.UpdatePanelInfo();
        }
        
        yield break;
    }
    
    public void StartTransitionTo(GameObject newPanel) { TransitionTo(newPanel); }
    private IEnumerator MovePaperRoutine(Transform paper, Vector3 targetPos, bool isFallingIn) { float timer = 0f; Vector3 startPos; Vector3 endPos; float paperHeightBuffer = paper.GetComponent<RectTransform>().rect.height; if (isFallingIn) { startPos = new Vector3(Random.Range(0, Screen.width), Screen.height + paperHeightBuffer, 0); endPos = targetPos; } else { startPos = paper.position; endPos = new Vector3(startPos.x + Random.Range(-300f, 300f), -paperHeightBuffer, 0); } Quaternion startRot = Quaternion.Euler(0, 0, Random.Range(-60f, 60f)); Quaternion endRot = Quaternion.Euler(0, 0, Random.Range(-15f, 15f)); while (timer < transitionTime) { if (paper == null) yield break; float progress = 1 - Mathf.Pow(1 - (timer / transitionTime), 3); paper.position = Vector3.LerpUnclamped(startPos, endPos, progress); paper.rotation = Quaternion.Slerp(startRot, endRot, progress); timer += Time.unscaledDeltaTime; yield return null; } yield break; }
    private IEnumerator FadeBlackout(bool fadeIn, float duration) { float timer = 0f; float startAlpha = fadeIn ? 0f : 0.8f; float endAlpha = fadeIn ? 0.8f : 0f; Color color = blackoutImage.color; while(timer < duration) { float progress = timer / duration; color.a = Mathf.Lerp(startAlpha, endAlpha, progress); blackoutImage.color = color; timer += Time.unscaledDeltaTime; yield return null; } color.a = endAlpha; blackoutImage.color = color; yield break; }
}