// Файл: Scripts/UI/MainMenuActions.cs --- ОБНОВЛЕННАЯ УПРОЩЕННАЯ ВЕРСИЯ ---
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Убедись, что эта строка есть для работы с TextMeshPro

public class MainMenuActions : MonoBehaviour
{
    [Header("Панели интерфейса")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject saveLoadPanel;
	[SerializeField] private Button continueButton;

    // --- <<< ИЗМЕНЕНИЕ: Теперь у нас только ОДНА кнопка >>> ---
    [Header("Главная кнопка")]
    [SerializeField] private Button primaryActionButton; 
    
    private TextMeshProUGUI primaryActionButtonText; // Ссылка на текст внутри кнопки

    void Awake()
    {
        // Находим компонент текста на кнопке один раз при старте
        if (primaryActionButton != null)
        {
            primaryActionButtonText = primaryActionButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        // Назначаем действие: любая из вариаций кнопки будет открывать панель слотов
        primaryActionButton.onClick.AddListener(Action_OpenSaveLoadPanel);
    }

    void Start()
    {
        // Проверяем, есть ли сохранения
        bool hasSaves = SaveLoadManager.Instance != null && SaveLoadManager.Instance.DoesAnySaveExist();

        // --- <<< ИЗМЕНЕНИЕ: Вместо вкл/выкл кнопок, меняем текст >>> ---
        if (primaryActionButtonText != null)
        {
            if (hasSaves)
            {
                // Если сохранения есть, кнопка предлагает их загрузить
                primaryActionButtonText.text = "Загрузить игру";
            }
            else
            {
                // Если сохранений нет, кнопка предлагает начать новую игру
                primaryActionButtonText.text = "Новая игра";
            }
        }
        if (continueButton != null)
    {
        continueButton.gameObject.SetActive(SaveLoadManager.Instance.DoesAnySaveExist());
    }
        // Показываем главное меню при старте
        ShowPanel(mainMenuPanel);
    }
    
    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ КНОПОК ---

    public void Action_OpenSaveLoadPanel()
    {
        // Этот метод теперь вызывается в обоих случаях
        Debug.Log("<b><color=cyan>[MainMenuActions] ==> Открываю панель выбора слотов...</color></b>");
        ShowPanel(saveLoadPanel);
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
    
    private void ShowPanel(GameObject panelToShow)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(false);

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
    }
}