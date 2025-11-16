using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Панели и кнопки (перетащить из иерархии)")]
    public GameObject mainMenuPanel;
    public GameObject saveSelectionPanel;
    public SaveSlotUI[] saveSlots;
    public Button continueButton;
    public Button newGameButton;
    public Button exitButton;

    [Header("Эффекты перехода")]
    public Image blackoutImage;
    public Transform leafTransitionContainer;
    public GameObject transitionLeafPrefab;
    public AudioClip transitionSound;
    public float totalTransitionDuration = 2.0f;
    public List<Transform> leafTargetPositions;
    public int minLeavesToAnimate = 25;
    public int maxLeavesToAnimate = 50;
    public float staggerDelay = 0.01f;

    private AudioSource uiAudioSource;
    private bool isTransitioning = false;

    void Start()
    {
        uiAudioSource = GetComponent<AudioSource>();
        
        // --- АВТОМАТИЧЕСКАЯ НАСТРОЙКА КНОПОК ПРИ ЗАПУСКЕ СЦЕНЫ ---
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);
        }
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(OnStartGameClicked);
        }
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(() => Application.Quit());
        }
        
        UpdateMainMenu();
    }

    private void UpdateMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (saveSelectionPanel != null) saveSelectionPanel.SetActive(false);
        
        // Безопасно обращаемся к "бессмертному" SaveLoadManager.Instance
        bool canContinue = SaveLoadManager.Instance != null && SaveLoadManager.Instance.GetLastSavedSlot(out _);
        if (continueButton != null) continueButton.gameObject.SetActive(canContinue);
    }
    
    // --- Методы для кнопок ---

    public void OnContinueClicked() 
    {
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.GetLastSavedSlot(out int slot))
        {
            StartGame(slot, false);
        }
    }

public void OnBackToMainMenuFromSavesClicked()
{
    if (saveSelectionPanel != null) saveSelectionPanel.SetActive(false);
    if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
}


    public void OnStartGameClicked() 
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (saveSelectionPanel != null) saveSelectionPanel.SetActive(true);

        if (saveSlots == null || SaveLoadManager.Instance == null) return;
        for (int i = 0; i < saveSlots.Length; i++)
        {
            if(saveSlots[i] != null) saveSlots[i].Setup(i, this, SaveLoadManager.Instance);
        }
    }

    public void OnSaveSlotClicked(int slotIndex) { StartGame(slotIndex, false); }
    public void OnNewGameClicked(int slotIndex) { StartGame(slotIndex, true); }

    private void StartGame(int slotIndex, bool isNewGame)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToGameSceneRoutine(slotIndex, isNewGame));
    }

    // --- Логика перехода ---

    private IEnumerator TransitionToGameSceneRoutine(int slotIndex, bool isNewGame)
    {
        isTransitioning = true;
        
        // Вся логика сохранения/загрузки теперь здесь, ПЕРЕД переходом
        if (isNewGame)
        {
            SaveLoadManager.Instance?.DeleteSave(slotIndex);
            DirectorManager.Instance?.ResetState();
            PlayerWallet.Instance?.ResetState();
        }
        else
        {
            SaveLoadManager.Instance?.LoadGame(slotIndex);
        }
        
        yield return StartCoroutine(FadeOutPhase());
        
        SceneManager.LoadScene("GameScene");
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
            yield return new WaitForSeconds(staggerDelay);
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