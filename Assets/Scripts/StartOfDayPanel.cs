// Файл: StartOfDayPanel.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class StartOfDayPanel : MonoBehaviour
{
    public static StartOfDayPanel Instance { get; private set; }

    [Header("Основные UI Элементы")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private Button startDayButton;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Документы Директора")]
    // --- ДОБАВЛЕНО: Ссылка на кнопку счетчика ---
    [SerializeField] private DirectorDeskButton directorDeskButton;
    [SerializeField] private GameObject documentIconPrefab;
    [SerializeField] private Transform documentIconsContainer;
    [SerializeField] private DirectorDocumentReviewPanel reviewPanel;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI strikesText;
    [SerializeField] private TextMeshProUGUI activeOrdersText;
    private Dictionary<ClientPathfinding, DirectorDocumentIcon> waitingDocumentIcons = new Dictionary<ClientPathfinding, DirectorDocumentIcon>();
    
    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else if (Instance != this) { Destroy(gameObject); }
    }
    
    private void OnEnable()
    {
        UpdatePanelInfo();
    }

    public void UpdatePanelInfo()
    {
        if (dayText != null) dayText.text = $"ДЕНЬ {ClientSpawner.Instance.GetCurrentDay()}";
        if (moneyText != null) moneyText.text = $"${PlayerWallet.Instance.GetCurrentMoney()}";
        if (strikesText != null) strikesText.text = $"Ошибки: {DirectorManager.Instance.currentStrikes} / 3";
        if (activeOrdersText != null)
        {
            var dailyOrders = DirectorManager.Instance.activeOrders.Select(o => o.orderName);
            var permanentOrders = DirectorManager.Instance.activePermanentOrders.Select(o => o.orderName + " (Пост.)");
            var allActiveOrders = dailyOrders.Concat(permanentOrders);
            if (allActiveOrders.Any()) { activeOrdersText.text = "<b>Активные приказы:</b>\n" + string.Join("\n", allActiveOrders); activeOrdersText.gameObject.SetActive(true); }
            else { activeOrdersText.gameObject.SetActive(false); }
        }
        
        if (startDayButton != null)
        {
            startDayButton.interactable = true;
            var buttonText = startDayButton.GetComponentInChildren<TextMeshProUGUI>();
            bool isMidDayPause = Time.timeScale == 0f && ClientSpawner.Instance.GetCurrentPeriod() != null && ClientSpawner.Instance.GetCurrentPeriod().periodName.ToLower() != "ночь";
            if (isMidDayPause) 
            { 
                buttonText.text = "Продолжить день";
            }
            else 
            { 
                buttonText.text = "Начать день";
            }
            startDayButton.onClick.RemoveAllListeners();
            startDayButton.onClick.AddListener(() => MainUIManager.Instance.StartOrResumeGameplay()); 
        }

        // --- ДОБАВЛЕНО: Обновляем кнопку при обновлении панели ---
        if(directorDeskButton != null)
        {
            directorDeskButton.UpdateAppearance(GetWaitingDocumentCount());
        }
    }

    public IEnumerator Fade(bool fadeIn, bool interactableAfterFade)
    {
        if (canvasGroup == null) yield break;
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        float fadeDuration = 0.3f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (fadeIn) { gameObject.SetActive(true); }
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = endAlpha;
        canvasGroup.interactable = interactableAfterFade;
        canvasGroup.blocksRaycasts = interactableAfterFade;
        if (!fadeIn) { gameObject.SetActive(false); }
    }

    public void RegisterDirectorDocument(ClientPathfinding client)
    {
        if (documentIconPrefab == null || documentIconsContainer == null || waitingDocumentIcons.ContainsKey(client)) return;
        
        GameObject iconGO = Instantiate(documentIconPrefab, documentIconsContainer);
        DirectorDocumentIcon icon = iconGO.GetComponent<DirectorDocumentIcon>();
        if (icon != null) { 
            icon.Setup(client, reviewPanel); 
            waitingDocumentIcons.Add(client, icon); 

            // --- ДОБАВЛЕНО: Уведомляем кнопку об изменении ---
            if(directorDeskButton != null)
            {
                directorDeskButton.UpdateAppearance(GetWaitingDocumentCount());
            }
        }
    }

    public void RemoveDocumentIcon(ClientPathfinding client)
    {
        if (client != null && waitingDocumentIcons.ContainsKey(client))
        {
            DirectorDocumentIcon iconToRemove = waitingDocumentIcons[client];
            waitingDocumentIcons.Remove(client);
            if(iconToRemove != null) Destroy(iconToRemove.gameObject);

            // --- ДОБАВЛЕНО: Уведомляем кнопку об изменении ---
            if(directorDeskButton != null)
            {
                directorDeskButton.UpdateAppearance(GetWaitingDocumentCount());
            }
        }
    }

    public int GetWaitingDocumentCount() { return waitingDocumentIcons.Count; }
}