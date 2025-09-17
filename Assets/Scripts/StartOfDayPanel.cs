using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class StartOfDayPanel : MonoBehaviour
{
    public static StartOfDayPanel Instance { get; private set; }

    [Header("Основные UI Элементы")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private Button startDayButton;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Документы Директора")]
    [SerializeField] private GameObject documentIconPrefab;
    [SerializeField] private Transform documentIconsContainer;
    [SerializeField] private DirectorDocumentReviewPanel reviewPanel; // <-- Новая ссылка

    // Используем словарь для быстрой связи клиента и его иконки
    private Dictionary<ClientPathfinding, DirectorDocumentIcon> waitingDocumentIcons = new Dictionary<ClientPathfinding, DirectorDocumentIcon>();
    
    // ... (остальные поля moneyText, strikesText, activeOrdersText) ...
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI strikesText;
    [SerializeField] private TextMeshProUGUI activeOrdersText;

    private void Awake()
    {
        Instance = this;
    }

    public void UpdatePanelInfo()
    {
        // ... (весь ваш код для обновления текста и кнопок) ...
        if (dayText != null) dayText.text = $"ДЕНЬ {ClientSpawner.Instance.GetCurrentDay() + 1}";
        if (moneyText != null) moneyText.text = $"${PlayerWallet.Instance.GetCurrentMoney()}";
        if (strikesText != null) strikesText.text = $"Страйки: {DirectorManager.Instance.currentStrikes} / 3";
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
            var buttonText = startDayButton.GetComponentInChildren<TextMeshProUGUI>();
            startDayButton.onClick.RemoveAllListeners();
            bool isMidDayPause = Time.timeScale == 0f && ClientSpawner.Instance.GetCurrentPeriod() != null && ClientSpawner.Instance.GetCurrentPeriod().periodName.ToLower() != "ночь";
            if (isMidDayPause) { buttonText.text = "Продолжить день"; startDayButton.interactable = true; startDayButton.onClick.AddListener(() => MainUIManager.Instance.StartOrResumeGameplay()); }
            else if (DirectorManager.Instance.offeredOrders.Count > 0) { buttonText.text = "Выберите приказ"; startDayButton.interactable = false;  }
            else { buttonText.text = "Начать день"; startDayButton.interactable = true; startDayButton.onClick.AddListener(() => MainUIManager.Instance.StartOrResumeGameplay()); }
        }
    }

public IEnumerator Fade(bool fadeIn, bool interactableAfterFade)
{
    if (canvasGroup == null) yield break;

    float startAlpha = fadeIn ? 0f : 1f;
    float endAlpha = fadeIn ? 1f : 0f;
    float fadeDuration = 0.3f; // Можешь вынести в инспектор, если нужно
    
    // Перед началом анимации делаем панель неактивной, чтобы избежать случайных кликов
    canvasGroup.interactable = false;
    canvasGroup.blocksRaycasts = false;

    if (fadeIn)
    {
        gameObject.SetActive(true);
    }

    float timer = 0f;
    while (timer < fadeDuration)
    {
        timer += Time.unscaledDeltaTime;
        canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadeDuration);
        yield return null;
    }

    canvasGroup.alpha = endAlpha;

    // После завершения анимации устанавливаем интерактивность
    canvasGroup.interactable = interactableAfterFade;
    canvasGroup.blocksRaycasts = interactableAfterFade;

    if (!fadeIn)
    {
        gameObject.SetActive(false);
    }
}

    // --- ОБНОВЛЕННЫЕ МЕТОДЫ ---
    public void RegisterDirectorDocument(ClientPathfinding client)
    {
        if (documentIconPrefab == null || documentIconsContainer == null || waitingDocumentIcons.ContainsKey(client)) return;

        GameObject iconGO = Instantiate(documentIconPrefab, documentIconsContainer);
        DirectorDocumentIcon icon = iconGO.GetComponent<DirectorDocumentIcon>();
        if (icon != null)
        {
            // Передаем и клиента, и панель обзора
            icon.Setup(client, reviewPanel); 
            waitingDocumentIcons.Add(client, icon);
        }
    }

    // Теперь метод принимает клиента, а не иконку
    public void RemoveDocumentIcon(ClientPathfinding client)
    {
        if (client != null && waitingDocumentIcons.ContainsKey(client))
        {
            DirectorDocumentIcon iconToRemove = waitingDocumentIcons[client];
            waitingDocumentIcons.Remove(client);
            if(iconToRemove != null) Destroy(iconToRemove.gameObject);
        }
    }

    public int GetWaitingDocumentCount()
    {
        return waitingDocumentIcons.Count;
    }
}