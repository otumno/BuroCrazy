using UnityEngine;
using UnityEngine.UI;

public class MainMenuActions : MonoBehaviour
{
    [Header("Панели интерфейса")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject saveLoadPanel;

    [Header("Кнопки для управления видимостью")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;

    void Awake()
    {
        // Проверяем, что все ссылки установлены
        if (mainMenuPanel == null) Debug.LogError("<b><color=red>[MainMenuActions] ОШИБКА: Панель 'mainMenuPanel' НЕ НАЗНАЧЕНА!</color></b>", this);
        if (saveLoadPanel == null) Debug.LogError("<b><color=red>[MainMenuActions] ОШИБКА: Панель 'saveLoadPanel' НЕ НАЗНАЧЕНА!</color></b>", this);
        // ... (добавьте проверки для кнопок, если хотите быть на 100% уверены)
    }

    void Start()
    {
        Debug.Log("<color=yellow>[MainMenuActions] Скрипт запущен (Start).</color>");
        
        bool hasSaves = SaveLoadManager.Instance != null && SaveLoadManager.Instance.DoesAnySaveExist();
        Debug.Log($"<color=yellow>[MainMenuActions] Проверка сохранений: hasSaves = {hasSaves}</color>");

        continueButton.gameObject.SetActive(hasSaves);
        loadGameButton.gameObject.SetActive(hasSaves);
        newGameButton.gameObject.SetActive(!hasSaves);

        Debug.Log("<color=yellow>[MainMenuActions] Начальное состояние: Показываю MainMenuPanel.</color>");
        ShowPanel(mainMenuPanel);
    }
    
    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ КНОПОК ---

    public void Action_Continue()
    {
        Debug.Log("<b><color=green>[MainMenuActions] ==> Вызван метод Action_Continue().</color></b>");
        //ShowPanel(null); // Прячем панели перед переходом
        
        int latestSlot = SaveLoadManager.Instance.GetLatestSaveSlotIndex();
        if (latestSlot != -1 && MainUIManager.Instance != null)
        {
            Debug.Log($"<color=green>[MainMenuActions] Передаю управление MainUIManager для загрузки слота #{latestSlot}.</color>");
            MainUIManager.Instance.OnSaveSlotClicked(latestSlot);
        }
        else
        {
            Debug.LogError("<b><color=red>[MainMenuActions] Action_Continue() НЕ СМОГ загрузить игру! latestSlot или MainUIManager не найдены.</color></b>");
        }
    }

    public void Action_OpenSaveLoadPanel()
    {
        Debug.Log("<b><color=cyan>[MainMenuActions] ==> Вызван метод Action_OpenSaveLoadPanel().</color></b>");
        ShowPanel(saveLoadPanel);
    }

    public void Action_BackToMainMenu()
    {
        Debug.Log("<b><color=orange>[MainMenuActions] ==> Вызван метод Action_BackToMainMenu().</color></b>");
        ShowPanel(mainMenuPanel);
    }

    public void Action_QuitGame()
    {
        Debug.Log("<b><color=grey>[MainMenuActions] ==> Вызван метод Action_QuitGame().</color></b>");
        Application.Quit();
    }
    
    private void ShowPanel(GameObject panelToShow)
    {
        string panelName = (panelToShow == null) ? "НИКАКУЮ (все выключены)" : panelToShow.name;
        Debug.Log($"<color=yellow>[MainMenuActions] Вызвана команда ShowPanel для панели: {panelName}</color>");

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
    }
}