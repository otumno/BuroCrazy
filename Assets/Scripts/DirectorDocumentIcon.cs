using UnityEngine;
using UnityEngine.UI;

public class DirectorDocumentIcon : MonoBehaviour
{
    // Ссылка на клиента, который "принес" этот документ
    public ClientPathfinding ownerClient;
    // Ссылка на главную панель для проверки документов
    private DirectorDocumentReviewPanel reviewPanel;
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnIconClicked);
        }
    }

    // Метод для настройки иконки при ее создании
    public void Setup(ClientPathfinding client, DirectorDocumentReviewPanel panel)
    {
        this.ownerClient = client;
        this.reviewPanel = panel;
    }

    private void OnIconClicked()
    {
        if (reviewPanel != null)
        {
            // Вызываем метод на панели, передавая информацию о владельце документа
            reviewPanel.ShowDocument(ownerClient);
        }
        else
        {
            Debug.LogError("Панель для проверки документов не назначена иконке!");
        }
    }
}