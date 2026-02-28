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
        GameObject[] allPanels = new GameObject[] { mainMenuPanel, saveLoadPanel, achievementListPanel, directorCreationPanel };
        
        if (directorCreationPanel != null)
        {
            var animator = directorCreationPanel.GetComponent<Managers.UIWindowAnimator>();
            if (animator != null)
            {
                animator.Open();
            }
            else
            {
                directorCreationPanel.SetActive(true);
                var canvasGroup = directorCreationPanel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
        }
        
        foreach (var panel in allPanels)
        {
            if (panel == null || panel == directorCreationPanel) continue;
            
            bool isVisible = panel.activeInHierarchy;
            if (!isVisible)
            {
                var cg = panel.GetComponent<CanvasGroup>();
                if (cg != null) isVisible = cg.alpha > 0.01f;
            }
            
            if (isVisible)
            {
                var animator = panel.GetComponent<Managers.UIWindowAnimator>();
                if (animator != null)
                {
                    animator.Close();
                }
                else
                {
                    panel.SetActive(false);
                }
            }
        }
        
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
        GameObject[] allPanels = new GameObject[] { mainMenuPanel, saveLoadPanel, achievementListPanel, directorCreationPanel };

        // 1. ГАРАНТИРОВАННО Включаем целевую панель
        if (panelToShow != null)
        {
            panelToShow.SetActive(true); // Страховка, если объект был выключен в Инспекторе
            var animator = panelToShow.GetComponent<UIWindowAnimator>();
            if (animator != null) 
            {
                animator.Open();
            }
            else
            {
                var cg = panelToShow.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }
            }
        }

        // 2. Красиво прячем все остальные
        foreach (var panel in allPanels)
        {
            if (panel == null || panel == panelToShow) continue;

            var cg = panel.GetComponent<CanvasGroup>();
            // Считаем видимым, если есть альфа ИЛИ объект активен
            bool isVisible = (cg != null && cg.alpha > 0.01f) || panel.activeSelf;

            if (isVisible)
            {
                var animator = panel.GetComponent<UIWindowAnimator>();
                if (animator != null)
                {
                    animator.Close();
                }
                else if (cg != null)
                {
                    // Мгновенное скрытие без SetActive(false)
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
            }
        }
    }
}