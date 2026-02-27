// Файл: Scripts/UI/MainMenuActions.cs --- ОБНОВЛЕННАЯ ВЕРСИЯ ---

using System.Collections;
using Managers;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UI.Creation;

public class MainMenuActions : MonoBehaviour
{
    [Header("Панели интерфейса")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject saveLoadPanel;
    [SerializeField] private GameObject achievementListPanel;
    [SerializeField] private GameObject directorCreationPanel;

    [Header("Boot Fade Effect")]
    [SerializeField] private BootFadeEffect bootFadeEffect;

    [Header("Director Creation")]
    [SerializeField] private DirectorCreationBookUI directorCreationBook;

	[SerializeField] private Button continueButton;

    [Header("Главная кнопка")]
    [SerializeField] private Button primaryActionButton; 
    private TextMeshProUGUI primaryActionButtonText; 

    private int selectedSlotIndex = -1;
    private bool isDirectorCreationActive = false;

    void Awake()
    {
        if (primaryActionButton != null)
        {
            primaryActionButtonText = primaryActionButton.GetComponentInChildren<TextMeshProUGUI>();
        }
        primaryActionButton.onClick.AddListener(Action_OpenSaveLoadPanel);
        
        if (directorCreationBook != null)
        {
            directorCreationBook.OnBookFinished += OnDirectorCreationFinished;
        }
    }
    
    void Start()
    {
        bool hasSaves = SaveLoadManager.Instance != null && SaveLoadManager.Instance.DoesAnySaveExist();
        if (primaryActionButtonText != null)
        {
            if (hasSaves)
            {
                primaryActionButtonText.text = "Загрузить игру";
            }
            else
            {
                primaryActionButtonText.text = "Новая игра";
            }
        }
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(SaveLoadManager.Instance.DoesAnySaveExist());
        }
        ShowPanel(mainMenuPanel);
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ КНОПОК ---

    public void Action_OpenSaveLoadPanel()
    {
        Debug.Log("<b><color=cyan>[MainMenuActions] ==> Открываю панель выбора слотов...</color></b>");
        ShowPanel(saveLoadPanel);
    }

    public void Action_OpenAchievementList()
    {
        Debug.Log("<b><color=green>[MainMenuActions] ==> Открываю Архив Ачивок...</color></b>");
        ShowPanel(achievementListPanel);
		MusicPlayer.Instance?.PlayArchiveTheme();
    }

    public void Action_Continue()
    {
        int latestSaveSlot = SaveLoadManager.Instance.GetLatestSaveSlotIndex();
        if (latestSaveSlot != -1)
        {
            MainUIManager.Instance.OnSaveSlotClicked(latestSaveSlot);
        }
    }

    public void Action_BackToMainMenu()
    {
        Debug.Log("<b><color=orange>[MainMenuActions] ==> Возвращаюсь в главное меню...</color></b>");
        ShowPanel(mainMenuPanel);
        MusicPlayer.Instance?.PlayMenuTheme();
    }

    public void Action_QuitGame()
    {
        Debug.Log("<b><color=grey>[MainMenuActions] ==> Выход из игры...</color></b>");
        Application.Quit();
    }

    public void Action_StartNewGameWithDirectorCreation(int slotIndex)
    {
        if (isDirectorCreationActive)
        {
            Debug.LogWarning("[MainMenuActions] Director Creation уже запущен, игнорируем повторный вызов");
            return;
        }
        
        isDirectorCreationActive = true;
        selectedSlotIndex = slotIndex;
        StartCoroutine(NewGameDirectorCreationFlow());
    }

    private IEnumerator NewGameDirectorCreationFlow()
    {
        var mascot = FindFirstObjectByType<Utilities.TutorialMascot>();
        if (mascot != null) mascot.gameObject.SetActive(false);
        if (MusicPlayer.Instance != null) MusicPlayer.Instance.StopMusic();

        if (bootFadeEffect != null)
        {
            bootFadeEffect.gameObject.SetActive(true);
            yield return StartCoroutine(PlayBootSequence());
        }
        else
        {
            SwitchToDirectorCreation();
        }
    }

    private IEnumerator PlayBootSequence()
    {
        yield return StartCoroutine(bootFadeEffect.PlayCloseRoutine());

        SwitchToDirectorCreation();

        yield return new WaitForSecondsRealtime(0.1f);

        yield return StartCoroutine(bootFadeEffect.PlayOpenRoutine());
    }

    private void SwitchToDirectorCreation()
    {
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (directorCreationPanel != null) directorCreationPanel.SetActive(true);
        if (directorCreationBook != null) directorCreationBook.OpenBook();
    }

    private void OnDirectorCreationFinished(Data.Creation.DirectorInitialState initialState, string creationCode)
    {
        Debug.Log($"[MainMenuActions] Director Creation finished. Code: {creationCode}");
        
        isDirectorCreationActive = false;

        if (selectedSlotIndex >= 0)
        {
            MainUIManager.Instance.StartNewGameWithDirectorCreation(selectedSlotIndex, initialState, creationCode);
        }
    }
    
    private void ShowPanel(GameObject panelToShow)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
        if (achievementListPanel != null) achievementListPanel.SetActive(false);
        if (directorCreationPanel != null) directorCreationPanel.SetActive(false);

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
    }
}