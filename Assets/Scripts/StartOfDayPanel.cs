// Файл: StartOfDayPanel.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StartOfDayPanel : MonoBehaviour
{
    public static StartOfDayPanel Instance { get; private set; }

    [Header("UI Компоненты")]
    public TextMeshProUGUI dayInfoText;
    public Button startDayButton;
    public Button hireButton;
    
    [Header("Документы Директора")]
    public GameObject documentIconPrefab;
    public Transform documentContainer;
    
    private MenuManager menuManager;
    private DirectorManager directorManager;
    private Dictionary<ClientPathfinding, GameObject> activeDocumentIcons = new Dictionary<ClientPathfinding, GameObject>();
    
    void Awake()
    {
        Instance = this;
    }
    
    private void Start()
    {
        menuManager = MenuManager.Instance;
        directorManager = DirectorManager.Instance;
        
        if (startDayButton != null && menuManager != null)
        {
            startDayButton.onClick.RemoveAllListeners();
            startDayButton.onClick.AddListener(OnStartOrContinueClicked);
        }
    }

    private void OnEnable()
    {
        UpdatePanelInfo();
    }

    public void UpdatePanelInfo()
    {
        if (directorManager == null) directorManager = DirectorManager.Instance;
        if (directorManager == null || ClientSpawner.Instance == null) return;
        
        string dayInfo = $"День: {ClientSpawner.Instance.GetCurrentDay()}\n";
        dayInfo += $"Страйки: {directorManager.currentStrikes}/3\n";
        
        if (directorManager.activeOrders.Count > 0)
        {
            DirectorOrder activeOrder = directorManager.activeOrders[0];
            dayInfo += $"\n<b>Активный приказ:</b>\n<color=yellow>{activeOrder.orderName}</color>";
            startDayButton.GetComponentInChildren<TextMeshProUGUI>().text = "Начать день";
        }
        else
        {
            dayInfo += "\n<color=red>Требуется издать приказ на день!</color>";
            startDayButton.GetComponentInChildren<TextMeshProUGUI>().text = "Выберите приказ";
        }

        dayInfoText.text = dayInfo;
        startDayButton.interactable = true;
    }
    
    public void RegisterDirectorDocument(ClientPathfinding client)
    {
        if (documentIconPrefab == null || documentContainer == null) return;
        if (activeDocumentIcons.ContainsKey(client)) return;

        GameObject iconInstance = Instantiate(documentIconPrefab, documentContainer);
        
        RectTransform containerRect = documentContainer.GetComponent<RectTransform>();
        RectTransform iconRect = iconInstance.GetComponent<RectTransform>();
        if (containerRect != null && iconRect != null)
        {
            float randomX = Random.Range(-containerRect.rect.width / 2 + iconRect.rect.width, containerRect.rect.width / 2 - iconRect.rect.width);
            float randomY = Random.Range(-containerRect.rect.height / 2 + iconRect.rect.height, containerRect.rect.height / 2 - iconRect.rect.height);
            iconRect.anchoredPosition = new Vector2(randomX, randomY);
        }

        if (iconInstance.TryGetComponent<DirectorDocumentIcon>(out var docIconScript))
        {
            var reviewPanel = FindFirstObjectByType<DirectorDocumentReviewPanel>(FindObjectsInactive.Include);
            docIconScript.Setup(client, reviewPanel);
        }
        
        activeDocumentIcons.Add(client, iconInstance);
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
        }
    }
    
    private void OnStartOrContinueClicked()
    {
        if (menuManager == null) menuManager = MenuManager.Instance;
        if (menuManager == null) return;

        menuManager.OnStartDayClicked();
    }
}