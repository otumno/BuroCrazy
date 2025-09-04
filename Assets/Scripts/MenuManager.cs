using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MenuManager : MonoBehaviour
{
    [Header("Панели UI")]
    public GameObject mainMenuPanel;
    public GameObject startOfDayPanel;
    public GameObject saveSelectionPanel;

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

    private GameObject currentPanel;
    private bool isTransitioning = false;
    private int lastLoadedSlotIndex = -1;

    private void Start()
    {
        mainMenuPanel.SetActive(false);
        saveSelectionPanel.SetActive(false);
        startOfDayPanel.SetActive(false);
        inGameUIButtons.SetActive(false);
        
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
        currentPanel.SetActive(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isTransitioning && currentPanel == null) OnOpenDirectorsOfficeClicked();
            else if (!isTransitioning && currentPanel == startOfDayPanel) OnStartDayClicked();
        }
    }
    
    // ... (Методы TransitionRoutine, MovePaperRoutine, FadeBlackout остаются без изменений) ...
    private void TransitionTo(GameObject panelToShow) { /* ... */ }
    private IEnumerator TransitionRoutine(GameObject newPanel) { /* ... */ }
    private IEnumerator MovePaperRoutine(Transform paper, Vector3 targetPos, bool isFallingIn) { /* ... */ }
    private IEnumerator FadeBlackout(bool fadeIn, float duration) { /* ... */ }


    public void OnContinueClicked() 
    { 
        if(saveLoadManager.GetLastSavedSlot(out lastLoadedSlotIndex)) 
        { 
            saveLoadManager.LoadGame(lastLoadedSlotIndex); 
            musicPlayer?.PlayDirectorsOfficeTheme(); 
            TransitionTo(startOfDayPanel); 
        } 
    }
    public void OnStartGameClicked() 
    { 
        for (int i = 0; i < saveSlots.Length; i++) 
        { 
            if(saveSlots[i] != null) 
                saveSlots[i].Setup(i, this, saveLoadManager); 
        } 
        TransitionTo(saveSelectionPanel); 
    }
    public void OnSaveSlotClicked(int slotIndex) 
    { 
        saveLoadManager.LoadGame(slotIndex);
        musicPlayer?.PlayDirectorsOfficeTheme(); 
        TransitionTo(startOfDayPanel); 
    }
    public void OnNewGameClicked(int slotIndex) 
    { 
        ClientSpawner.Instance?.ResetState(); 
        PlayerWallet.Instance?.ResetState(); 
        ArchiveManager.Instance?.ResetState(); 
        saveLoadManager.SaveGame(slotIndex); 
        OnSaveSlotClicked(slotIndex); 
    }

    // --- ИЗМЕНЕНИЕ ЗДЕСЬ ---
    public void OnStartDayClicked() 
    { 
        // 1. Сохраняем игру перед началом дня
        if(saveLoadManager.GetLastSavedSlot(out int lastSlot))
        {
            saveLoadManager.SaveGame(lastSlot);
        }

        // 2. Начинаем день
        Time.timeScale = 1f; 
        musicPlayer?.PlayGameplayMusic(); 
        TransitionTo(null); 
    }
    
    public void OnOpenDirectorsOfficeClicked() 
    { 
        Time.timeScale = 0f; 
        musicPlayer?.PlayDirectorsOfficeTheme(); 
        TransitionTo(startOfDayPanel); 
    }

    // --- ИЗМЕНЕНИЕ ЗДЕСЬ ---
    public void OnBackToMainMenuClicked() 
    {
        // Больше не сохраняем при выходе в меню
        if (saveLoadManager.GetLastSavedSlot(out lastLoadedSlotIndex))
        {
            continueButton.gameObject.SetActive(true);
        }
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
}