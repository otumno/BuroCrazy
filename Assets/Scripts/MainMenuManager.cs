using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Этот скрипт будет управлять кнопками в главном меню
public class MainMenuManager : MonoBehaviour
{
    // Перетащите сюда из иерархии кнопку "Продолжить"
    public Button continueButton;

    // Перетащите сюда кнопку "Новая игра"
    public Button newGameButton;

    // Перетащите сюда кнопку, которую мы назовем "Загрузить"
    public Button loadGameButton; 
    
    // Перетащите сюда панель загрузки/сохранения
    public GameObject saveLoadPanel;

    // Название вашей игровой сцены
    private string gameSceneName = "GameScene"; // <-- УКАЖИТЕ ЗДЕСЬ ПРАВИЛЬНОЕ НАЗВАНИЕ ВАШЕЙ СЦЕНЫ

    void Start()
    {
        // Проверяем, есть ли вообще сохранения
        if (SaveLoadManager.Instance.DoesAnySaveExist()) // <-- ПРЕДПОЛАГАЕМ, ЧТО ТАКОЙ МЕТОД ЕСТЬ
        {
            // Если сохранения есть:
            continueButton.gameObject.SetActive(true); // Показываем кнопку "Продолжить"
            newGameButton.gameObject.SetActive(false); // Прячем кнопку "Новая игра"
        }
        else
        {
            // Если сохранений нет:
            continueButton.gameObject.SetActive(false); // Прячем кнопку "Продолжить"
            newGameButton.gameObject.SetActive(true);  // Показываем кнопку "Новая игра"
        }

        // Убедимся, что панель сохранений по умолчанию скрыта
        saveLoadPanel.SetActive(false);

        // Назначаем функции на нажатия кнопок
        continueButton.onClick.AddListener(OnContinueClick);
        newGameButton.onClick.AddListener(OnNewGameClick);
        loadGameButton.onClick.AddListener(OnLoadClick);
    }

    // Что происходит при нажатии на "Продолжить"
    private void OnContinueClick()
    {
        // Предполагаем, что у вас есть метод для загрузки последнего сохранения
        // Например, он может загружать слот с самым большим номером или последней датой
        int latestSlot = SaveLoadManager.Instance.GetLatestSaveSlotIndex(); // <-- ПРЕДПОЛАГАЕМ, ЧТО ТАКОЙ МЕТОД ЕСТЬ
        SaveLoadManager.Instance.LoadGame(latestSlot);
        SceneManager.LoadScene(gameSceneName);
    }

    // Что происходит при нажатии на "Новая игра"
    private void OnNewGameClick()
    {
        // Когда игрок начинает новую игру, ему нужно выбрать слот.
        // Поэтому мы просто открываем панель сохранений.
        saveLoadPanel.SetActive(true);
    }

    // Что происходит при нажатии на "Загрузить"
    private void OnLoadClick()
    {
        // То же самое - открываем панель, чтобы игрок мог выбрать сохранение.
        saveLoadPanel.SetActive(true);
    }
}