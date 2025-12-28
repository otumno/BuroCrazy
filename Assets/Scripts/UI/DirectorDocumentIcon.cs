using UnityEngine;
using UnityEngine.UI;
using Managers;

[RequireComponent(typeof(Image))]
public class DirectorDocumentIcon : MonoBehaviour
{
    [Header("UI")]
    public Image iconImage;
    [Header("Спрайты")]
    public Sprite defaultVisitorSprite; 

    private ClientPathfinding ownerClient;
    private DirectorDocumentReviewPanel reviewPanel;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if(iconImage == null) iconImage = GetComponent<Image>();
        button.onClick.AddListener(OnIconClicked);
    }

    public void Setup(ClientPathfinding client, DirectorDocumentReviewPanel panel)
    {
        this.ownerClient = client;
        this.reviewPanel = panel;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (ownerClient == null) return;

        // 1. ЗВОНОК (Удаленный клиент)
        if (ownerClient.IsRemote)
        {
            if (ownerClient.iconOverride != null) 
                iconImage.sprite = ownerClient.iconOverride;
        }
        // 2. ПОСЕТИТЕЛЬ (Прием)
        else if (ownerClient.mainGoal == ClientGoal.DirectorAudience)
        {
            var visuals = ownerClient.GetVisuals();
            Sprite portrait = visuals != null ? visuals.GetPortraitSprite() : null;
            iconImage.sprite = portrait != null ? portrait : defaultVisitorSprite;
        }
        // 3. ДОКУМЕНТ (Обычный) - оставляем спрайт префаба
    }

    private void OnIconClicked()
    {
        if (ownerClient == null) { Destroy(gameObject); return; }

        // --- ВАРИАНТ 1: ЗВОНОК (ТЕЛЕФОН) ---
        if (ownerClient.IsRemote)
        {
            Debug.Log($"[Icon] Ответ на звонок: {ownerClient.name}");
            // true = уничтожить объект клиента после диалога (т.к. это просто звонок)
            StartDialogueAndCleanup(true);
        }
        // --- ВАРИАНТ 2: ПОСЕТИТЕЛЬ (ЛИЧНЫЙ ПРИЕМ) ---
        else if (ownerClient.mainGoal == ClientGoal.DirectorAudience)
        {
            Debug.Log($"[Icon] Приглашаю посетителя {ownerClient.name}");
            // false = не уничтожать, а отправить к выходу (физический персонаж)
            StartDialogueAndCleanup(false);
        }
        // --- ВАРИАНТ 3: ДОКУМЕНТ (МИНИ-ИГРА) ---
        else
        {
            if (reviewPanel != null) reviewPanel.ShowDocument(ownerClient);
        }
    }

    // --- ВОТ МЕТОД, КОТОРОГО НЕ ХВАТАЛО ---
    private void StartDialogueAndCleanup(bool destroyObject)
    {
        if (ownerClient.specificDialogue == null)
        {
            Debug.LogWarning("Нет диалога! Завершаем сразу.");
            FinalizeClientInteraction(destroyObject);
            return;
        }

        if (DialogueUIManager.Instance != null)
        {
            // 1. СОХРАНЯЕМ ССЫЛКИ ПЕРЕД ЗАПУСКОМ ДИАЛОГА
            var clientRef = ownerClient;
            var dialogueRef = ownerClient.specificDialogue; // <--- ВОТ ЭТА СТРОКА ДОЛЖНА БЫТЬ ЗДЕСЬ
            
            // 2. ЗАПУСКАЕМ ДИАЛОГ
            DialogueUIManager.Instance.StartDialogue(ownerClient.specificDialogue, ownerClient, () => 
            {
                // 3. ЭТОТ КОД ВЫПОЛНИТСЯ ПОСЛЕ ДИАЛОГА
                Debug.Log($"[Icon] Диалог завершен. Удаляю {clientRef?.name}");

                // Теперь dialogueRef доступен, так как мы объявили его выше
                if (dialogueRef != null && PhoneManager.Instance != null)
                {
                    PhoneManager.Instance.RemoveCall(dialogueRef);
                }
                
                if (StartOfDayPanel.Instance != null)
                    StartOfDayPanel.Instance.RemoveDocumentIcon(clientRef);

                if (clientRef != null)
                {
                    clientRef.isLeavingSuccessfully = true;
                    clientRef.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;

                    if (destroyObject)
                    {
                        Destroy(clientRef.gameObject);
                    }
                    else
                    {
                        clientRef.ForceLeave(ClientPathfinding.LeaveReason.Processed);
                    }
                }
            });
        }
    }

    private void FinalizeClientInteraction(bool destroyObject)
    {
        if (StartOfDayPanel.Instance != null) StartOfDayPanel.Instance.RemoveDocumentIcon(ownerClient);
        if (ownerClient != null)
        {
            if (destroyObject) Destroy(ownerClient.gameObject);
            else ownerClient.ForceLeave(ClientPathfinding.LeaveReason.Processed);
        }
    }
}