using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Managers;

public class PhonePanelUI : MonoBehaviour
{
    [Header("Контейнеры")]
    [Tooltip("Куда добавлять кнопки входящих")]
    public Transform incomingContainer;
    [Tooltip("Куда добавлять кнопки исходящих")]
    public Transform outgoingContainer;

    [Header("Префабы")]
    [Tooltip("Префаб кнопки (должен иметь Button и TextMeshProUGUI)")]
    public GameObject buttonPrefab;
    
    [Header("Кнопки управления")]
    public Button closeButton;

    private void Start()
    {
        closeButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        MainUIManager.Instance?.PushPause();
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        MainUIManager.Instance?.PopPause();
    }

    public void Refresh()
    {
        ClearContainer(incomingContainer);
        ClearContainer(outgoingContainer);

        if (PhoneManager.Instance == null) return;

        // 1. Рисуем Входящие
        var calls = PhoneManager.Instance.GetActiveCalls();
        if (calls.Count == 0)
        {
            CreateLabel(incomingContainer, "Нет входящих вызовов");
        }
        else
        {
            for (int i = 0; i < calls.Count; i++)
            {
                int index = i; // Замыкание
                string label = $"Линия {index + 1}"; // Или имя из диалога, если есть метаданные
                CreateButton(incomingContainer, label, () => PhoneManager.Instance.AnswerCall(calls[index]), Color.green);
            }
        }

        // 2. Рисуем Исходящие
        bool anyOutgoing = false;
        foreach (var contact in PhoneManager.Instance.outgoingContacts)
        {
            if (contact.isUnlocked)
            {
                CreateButton(outgoingContainer, contact.displayName, () => PhoneManager.Instance.CallContact(contact), Color.white);
                anyOutgoing = true;
            }
        }
        
        if (!anyOutgoing)
        {
            CreateLabel(outgoingContainer, "Нет доступных контактов");
        }
    }

    private void CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction action, Color color)
    {
        GameObject btnObj = Instantiate(buttonPrefab, parent);
        var btn = btnObj.GetComponent<Button>();
        var txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        
        txt.text = text;
        btn.image.color = color; // Пример цветовой кодировки
        btn.onClick.AddListener(action);
    }

    private void CreateLabel(Transform parent, string text)
    {
        GameObject btnObj = Instantiate(buttonPrefab, parent);
        var btn = btnObj.GetComponent<Button>();
        var txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        
        txt.text = text;
        btn.interactable = false; // Делаем неактивным, так как это просто текст
        
        // Делаем прозрачным или серым
        var cg = btnObj.AddComponent<CanvasGroup>();
        cg.alpha = 0.5f;
    }

    private void ClearContainer(Transform container)
    {
        foreach (Transform child in container) Destroy(child.gameObject);
    }
}