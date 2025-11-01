// Файл: Scripts/UI/MainMenuActions.cs --- ОБНОВЛЕННАЯ ВЕРСИЯ ---
using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class MainMenuActions : MonoBehaviour
{
    [Header("Панели интерфейса")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject saveLoadPanel;
    
    // --- <<< НОВАЯ СТРОКА >>> ---
    [SerializeField] private GameObject achievementListPanel; // Перетащи сюда [UI] AchievementListPanel
    // --- <<< КОНЕЦ НОВОЙ СТРОКИ >>> ---

	[SerializeField] private Button continueButton;

    // ... (остальные поля и Awake() остаются как были) ...
    [Header("Главная кнопка")]
    [SerializeField] private Button primaryActionButton; 
    private TextMeshProUGUI primaryActionButtonText; 

    void Awake()
    {
        if (primaryActionButton != null)
        {
            primaryActionButtonText = primaryActionButton.GetComponentInChildren<TextMeshProUGUI>();
        }
        primaryActionButton.onClick.AddListener(Action_OpenSaveLoadPanel);
    }
    
    // ... (Start() остается как был) ...
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
		MusicPlayer.Instance?.PlayMenuTheme();
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ КНОПОК ---

    public void Action_OpenSaveLoadPanel()
    {
        Debug.Log("<b><color=cyan>[MainMenuActions] ==> Открываю панель выбора слотов...</color></b>");
        ShowPanel(saveLoadPanel);
		MusicPlayer.Instance?.PlayMenuTheme();
    }

    // --- <<< НОВЫЙ МЕТОД >>> ---
    /// <summary>
    /// Вызывается кнопкой "Архив" из главного меню.
    /// </summary>
    public void Action_OpenAchievementList()
    {
        Debug.Log("<b><color=green>[MainMenuActions] ==> Открываю Архив Ачивок...</color></b>");
        ShowPanel(achievementListPanel);
		MusicPlayer.Instance?.PlayArchiveTheme();
    }
    // --- <<< КОНЕЦ НОВОГО МЕТОДА >>> ---

    public void Action_Continue()
    {
        // ... (код без изменений) ...
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
    
    private void ShowPanel(GameObject panelToShow)
    {
        // --- <<< ОБНОВЛЕННЫЙ МЕТОД >>> ---
        // Теперь он знает о трех панелях
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);
        if (achievementListPanel != null) achievementListPanel.SetActive(false); // Прячем и ачивки

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
        // --- <<< КОНЕЦ ОБНОВЛЕНИЯ >>> ---
    }
}