using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[RequireComponent(typeof(CanvasGroup))]
public class StartOfDayPanel : MonoBehaviour
{
    public static StartOfDayPanel Instance { get; private set; }

    [Header("UI Компоненты")]
    public TextMeshProUGUI dayInfoText;
    public Button startDayButton;
    public DirectorDeskButton directorDeskButton;
    
    [Header("Документы Директора")]
    public GameObject documentIconPrefab;
    public Transform documentContainer;
    
    private DirectorManager directorManager;
    private Dictionary<ClientPathfinding, GameObject> activeDocumentIcons = new Dictionary<ClientPathfinding, GameObject>();
    private CanvasGroup canvasGroup;
    
    void Awake()
    {
        Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
    }
    
    // Метод Update() полностью удален, чтобы избежать конфликтов.

    public void UpdatePanelInfo()
    {
        if (directorManager == null) directorManager = DirectorManager.Instance;
        if (directorManager == null || ClientSpawner.Instance == null || startDayButton == null) return;
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"День: {ClientSpawner.Instance.GetCurrentDay()}");
        sb.AppendLine($"Страйки: {directorManager.currentStrikes}/3");

        bool hasActiveOrders = directorManager.activeOrders.Count > 0 || directorManager.activePermanentOrders.Count > 0;
        
        if (hasActiveOrders)
        {
            var orderNames = directorManager.activePermanentOrders.Select(o => o.orderName).ToList();
            if (directorManager.activeOrders.Count > 0) orderNames.Add(directorManager.activeOrders[0].orderName);
            sb.AppendLine($"\n<b>Активные приказы:</b>\n<color=yellow>{string.Join("\n", orderNames)}</color>");
        }
        else
        {
             sb.AppendLine("\n<color=red>Требуется издать приказ на день!</color>");
        }
        dayInfoText.text = sb.ToString();

        var buttonText = startDayButton.GetComponentInChildren<TextMeshProUGUI>();
        startDayButton.onClick.RemoveAllListeners();
        
        bool isMidDayPause = ClientSpawner.Instance.GetPeriodTimer() > 0 && Time.timeScale == 0f;

        if (isMidDayPause)
        {
            buttonText.text = "Продолжить день";
            startDayButton.onClick.AddListener(() => MenuManager.Instance.StartOrResumeGameplay());
        }
        else if (hasActiveOrders)
        {
            buttonText.text = "Начать день";
            startDayButton.onClick.AddListener(() => MenuManager.Instance.StartOrResumeGameplay());
        }
        else
        {
            buttonText.text = "Выберите приказ";
            startDayButton.onClick.AddListener(() => MenuManager.Instance.ShowOrderSelection());
        }
    }
    
    public void ShowPanel()
    {
        StartCoroutine(Fade(true, true));
    }
    
    public IEnumerator Fade(bool fadeIn, bool forceInteractable)
    {
        float fadeTime = 0.3f;
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        
        canvasGroup.blocksRaycasts = true;
        
        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadeTime);
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;
        canvasGroup.interactable = fadeIn || forceInteractable;
        canvasGroup.blocksRaycasts = fadeIn || forceInteractable;
    }

    public void RegisterDirectorDocument(ClientPathfinding client)
    {
        if (documentIconPrefab == null || documentContainer == null) return;
        if (activeDocumentIcons.ContainsKey(client)) return;

        GameObject iconInstance = Instantiate(documentIconPrefab, documentContainer);
        
        if (iconInstance.TryGetComponent<DirectorDocumentIcon>(out var docIconScript))
        {
            var reviewPanel = FindFirstObjectByType<DirectorDocumentReviewPanel>(FindObjectsInactive.Include);
            docIconScript.Setup(client, reviewPanel);
        }
        
        activeDocumentIcons.Add(client, iconInstance);
        if (directorDeskButton != null) directorDeskButton.UpdateAppearance(activeDocumentIcons.Count);
    }

    public void RemoveDocumentIcon(ClientPathfinding client)
    {
        if (client != null && activeDocumentIcons.ContainsKey(client))
        {
            GameObject iconToRemove = activeDocumentIcons[client];
            if (iconToRemove != null)
            {
                Destroy(iconToRemove);
            }
            activeDocumentIcons.Remove(client);
            
            if (directorDeskButton != null) directorDeskButton.UpdateAppearance(activeDocumentIcons.Count);
        }
    }
    
    public int GetWaitingDocumentCount()
    {
        return activeDocumentIcons.Count;
    }
}