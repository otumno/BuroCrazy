using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Панели интерфейса")]
    [Tooltip("Родительский объект для кнопок главного меню")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("Панель с ячейками для сохранения/загрузки")]
    [SerializeField] private GameObject saveLoadPanel;

    [Header("Кнопки главного меню")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;

    void Awake()
    {
        // --- ДИАГНОСТИКА: ПРОВЕРКА ВСЕХ ССЫЛОК ---
        // Эта проверка поможет найти проблему, если вы что-то забыли назначить в инспекторе.
        if (mainMenuPanel == null) Debug.LogError("[MainMenuController] ОШИБКА: Панель 'mainMenuPanel' не назначена в инспекторе!", this);
        if (saveLoadPanel == null) Debug.LogError("[MainMenuController] ОШИБКА: Панель 'saveLoadPanel' не назначена в инспекторе!", this);
        if (continueButton == null) Debug.LogError("[MainMenuController] ОШИБКА: Кнопка 'continueButton' не назначена!", this);
        if (newGameButton == null) Debug.LogError("[MainMenuController] ОШИБКА: Кнопка 'newGameButton' не назначена!", this);
        if (loadGameButton == null) Debug.LogError("[MainMenuController] ОШИБКА: Кнопка 'loadGameButton' не назначена!", this);
        if (quitButton == null) Debug.LogError("[MainMenuController] ОШИБКА: Кнопка 'quitButton' не назначена!", this);
        if (backButton == null) Debug.LogError("[MainMenuController] ОШИБКА: Кнопка 'backButton' не назначена!", this);

        // Назначаем действия на кнопки
        continueButton.onClick.AddListener(OnContinueClick);
        newGameButton.onClick.AddListener(OnNewGameClick);
        loadGameButton.onClick.AddListener(OnLoadClick);
        quitButton.onClick.AddListener(OnQuitClick);
        backButton.onClick.AddListener(OnBackToMainMenuClick);
    }

    void Start()
    {
        // Логика отображения кнопок
        bool hasSaves = SaveLoadManager.Instance != null && SaveLoadManager.Instance.DoesAnySaveExist();
        continueButton.gameObject.SetActive(hasSaves);
        loadGameButton.gameObject.SetActive(hasSaves);
        newGameButton.gameObject.SetActive(!hasSaves);

        // Начальное состояние панелей - показываем главное меню
        ShowPanel(mainMenuPanel);
    }

    // --- МЕТОДЫ-ОБРАБОТЧИКИ НАЖАТИЙ ---

    private void OnContinueClick()
    {
        // Перед тем как передать управление, выключаем панели, чтобы избежать "синего экрана" во время перехода
       // ShowPanel(null); // Эта команда выключит обе панели
        
        int latestSlot = SaveLoadManager.Instance.GetLatestSaveSlotIndex();
        if (latestSlot != -1 && MainUIManager.Instance != null)
        {
            MainUIManager.Instance.OnSaveSlotClicked(latestSlot);
        }
    }

    private void OnNewGameClick()
    {
        ShowPanel(saveLoadPanel);
    }

    private void OnLoadClick()
    {
        ShowPanel(saveLoadPanel);
    }

    private void OnBackToMainMenuClick()
    {
        ShowPanel(mainMenuPanel);
    }

    private void OnQuitClick()
    {
        Application.Quit();
    }

    // --- "ПУЛЕНЕПРОБИВАЕМЫЙ" МЕТОД УПРАВЛЕНИЯ ПАНЕЛЯМИ ---

    private void ShowPanel(GameObject panelToShow)
    {
        // Сначала принудительно выключаем обе панели. Это сбрасывает любое "сломанное" состояние.
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);

        // Если нужно показать какую-то панель, включаем ее.
        // Если panelToShow == null, обе панели останутся выключенными.
        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
    }
}