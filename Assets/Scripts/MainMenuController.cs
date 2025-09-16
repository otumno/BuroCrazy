using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    // Перетащите сюда кнопки из иерархии в инспекторе
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton; // Кнопка "Назад" на панели сохранений

    void Start()
    {
        // Ждем, пока MainUIManager точно будет создан
        if (MainUIManager.Instance != null)
        {
            // Назначаем слушателей из кода - это самый надежный способ
            continueButton.onClick.AddListener(MainUIManager.Instance.OnClick_Continue);
            newGameButton.onClick.AddListener(MainUIManager.Instance.OnClick_NewGame);
            quitButton.onClick.AddListener(MainUIManager.Instance.OnClick_QuitGame);
            backButton.onClick.AddListener(MainUIManager.Instance.OnClick_BackToMainMenu);

            Debug.Log("Кнопки главного меню успешно подключены к MainUIManager.");
        }
        else
      {
            Debug.LogError("MainMenuController не смог найти MainUIManager.Instance!");
        }
    }
 
 
}