using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Managers;

public class DialogueUIConnector : MonoBehaviour
{
    [Header("Панель")]
    public GameObject dialoguePanel;

    [Header("Портреты")]
    public Image directorPortrait;
    public Image directorPortraitFrame;
    public Image clientPortrait;
    public Image clientPortraitFrame;

    [Header("Имена (Под портретами)")]
    public TextMeshProUGUI directorNamePlate; // <--- НОВОЕ
    public TextMeshProUGUI clientNamePlate;   // <--- НОВОЕ

    [Header("Текст Диалога")]
    // speakerNameText больше не нужен для центра, но можно оставить для совместимости или удалить
    // public TextMeshProUGUI speakerNameText; 
    public TextMeshProUGUI dialogueText;

    [Header("Кнопки")]
    public Button nextButton;
    public Transform choiceContainer;

    [Header("Изображение для ноды")]
    [Tooltip("Контейнер с рамкой и изображением")]
    public GameObject nodeImageContainer;
    public Image nodeImageDisplay;
    public Image nodeImageFrame;

    void Start()
    {
        if (DialogueUIManager.Instance != null)
        {
            DialogueUIManager.Instance.RegisterSceneUI(this);
        }
    }
}