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
    
    [Header("Слои портрета (Для личного приема)")]
    public GameObject photoContainer; // Контейнер для рамки и маски
    public Image portraitBody;
    public Image portraitOutfit;
    public Image portraitHair;
    public Image portraitFace;
    public Image uniquePortraitDisplay; // Новый объект для цельного фото
    public Material grayscaleMaterial;
    public TMPro.TextMeshProUGUI visitorNameText; // Текст для имени на фото/папке
    public TMPro.TextMeshProUGUI statusText; // Текст "На рассмотрении" / "Ожидает приема"

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

        if (statusText != null)
        {
            if (ownerClient.mainGoal == ClientGoal.DirectorAudience)
                statusText.text = "Ожидает приема";
            else
                statusText.text = "На рассмотрении";
        }

        // Сброс состояния: папка видна, фото и имя скрыты
        if (photoContainer != null) photoContainer.SetActive(false);
        if (visitorNameText != null) visitorNameText.gameObject.SetActive(false);
        iconImage.enabled = true;

        if (ownerClient.IsRemote)
        {
            if (ownerClient.iconOverride != null) iconImage.sprite = ownerClient.iconOverride;
        }
        else if (ownerClient.mainGoal == ClientGoal.DirectorAudience)
        {
            var visuals = ownerClient.GetVisuals();
            if (visuals != null)
            {
                iconImage.enabled = false;
                if (photoContainer != null) photoContainer.SetActive(true);
                
                // Форматирование имени: "Посетитель №..." или уникальное имя
                if (visitorNameText != null)
                {
                    visitorNameText.gameObject.SetActive(true);
                    string rawName = ownerClient.gameObject.name.Replace("(Clone)", "").Trim();
                    if (rawName.Contains("#"))
                    {
                        string[] parts = rawName.Split('#');
                        string number = parts.Length > 1 ? parts[1].Trim() : "";
                        visitorNameText.text = $"Посетитель №{number}";
                    }
                    else
                    {
                        visitorNameText.text = rawName;
                    }
                }

                Sprite uniqueSpr = visuals.UniquePortrait;
                if (uniqueSpr != null)
                {
                    if (uniquePortraitDisplay != null)
                    {
                        uniquePortraitDisplay.gameObject.SetActive(true);
                        uniquePortraitDisplay.sprite = uniqueSpr;
                        if (grayscaleMaterial != null) uniquePortraitDisplay.material = grayscaleMaterial;
                    }
                    if (portraitBody != null) portraitBody.gameObject.SetActive(false);
                    if (portraitOutfit != null) portraitOutfit.gameObject.SetActive(false);
                    if (portraitHair != null) portraitHair.gameObject.SetActive(false);
                    if (portraitFace != null) portraitFace.gameObject.SetActive(false);
                }
                else
                {
                    if (uniquePortraitDisplay != null) uniquePortraitDisplay.gameObject.SetActive(false);
                    if (portraitBody != null) {
                        portraitBody.gameObject.SetActive(true);
                        SetPortraitLayer(portraitBody, visuals.GetBodyRenderer());
                    }
                    if (portraitOutfit != null) {
                        portraitOutfit.gameObject.SetActive(true);
                        SetPortraitLayer(portraitOutfit, visuals.GetOutfitRenderer());
                    }
                    if (portraitHair != null) {
                        portraitHair.gameObject.SetActive(true);
                        SetPortraitLayer(portraitHair, visuals.GetHairRenderer());
                    }
                    if (portraitFace != null && visuals.currentSpriteCollection != null) {
                        portraitFace.gameObject.SetActive(true);
                        portraitFace.sprite = visuals.currentSpriteCollection.GetFaceSprite(Emotion.Neutral, ownerClient.gender);
                        if (grayscaleMaterial != null) portraitFace.material = grayscaleMaterial;
                    }
                }
            }
        }
    }

    private void SetPortraitLayer(Image uiImage, SpriteRenderer sourceRenderer)
    {
        if (uiImage == null) return;
        if (sourceRenderer != null && sourceRenderer.sprite != null) {
            uiImage.sprite = sourceRenderer.sprite;
            uiImage.color = sourceRenderer.color;
            uiImage.enabled = true;
            if (grayscaleMaterial != null) uiImage.material = grayscaleMaterial;
        } else {
            uiImage.enabled = false;
        }
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
                Debug.Log($"[Icon] Диалог завершен. Обрабатываем {clientRef?.name}");
                if (dialogueRef != null && PhoneManager.Instance != null) {
                    PhoneManager.Instance.RemoveCall(dialogueRef);
                }
                if (StartOfDayPanel.Instance != null) {
                    StartOfDayPanel.Instance.RemoveDocumentIcon(clientRef);
                }
                if (clientRef != null && destroyObject) {
                    Destroy(clientRef.gameObject);
                }
                // Мы больше не заставляем клиента уходить довольным здесь. Это решает DialogueUIManager.
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