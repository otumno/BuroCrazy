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
    public Image clientPortrait;

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

    void Start()
    {
        if (DialogueUIManager.Instance != null)
        {
            DialogueUIManager.Instance.RegisterSceneUI(this);
        }
    }
}