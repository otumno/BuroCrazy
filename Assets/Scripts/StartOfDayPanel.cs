using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Data.Calendar;
using DG.Tweening;
using Managers;

public class StartOfDayPanel : MonoBehaviour
{
    public static StartOfDayPanel Instance { get; private set; }

    [Header("Основные UI Элементы")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private Button startDayButton;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI errorRateText;
    
    // --- НОВЫЕ ПОЛЯ ДЛЯ ФОНА ---
    [Header("Атмосферный Фон")]
    [Tooltip("Ссылка на Image объекта 'Back'")]
    [SerializeField] private Image backgroundImage; 
    
    [System.Serializable]
    public class PeriodBackground
    {
        [Tooltip("Выберите периоды, для которых подходит этот фон")]
        public CalendarDayPeriodType periodMask;
        public Sprite backgroundSprite;
    }
    
    [Tooltip("Список фонов для разного времени суток")]
    public List<PeriodBackground> backgroundVisuals;
    // ---------------------------

    [Header("Документы Директора")]
    [SerializeField] private DirectorDeskButton directorDeskButton;
    [SerializeField] private GameObject documentIconPrefab;
    [SerializeField] private Transform documentIconsContainer;
    [SerializeField] private DirectorDocumentReviewPanel reviewPanel;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI strikesText;
    [SerializeField] private TextMeshProUGUI activeOrdersText;
	
	[Header("Проектные Документы")]
    [SerializeField] private GameObject projectDocIconPrefab; // Префаб иконки (скрипт ProjectDocumentIconUI)
    [SerializeField] private UI.ProjectReviewPanelUI projectReviewPanel; // Ссылка на панель просмотра
    
	
	private Dictionary<Data.Documents.ProjectDocumentDefinition, GameObject> activeProjectIcons = new Dictionary<Data.Documents.ProjectDocumentDefinition, GameObject>();
    private Dictionary<ClientPathfinding, DirectorDocumentIcon> waitingDocumentIcons = new Dictionary<ClientPathfinding, DirectorDocumentIcon>();

    [Header("Animation Settings")]
    [SerializeField] private float _fadeDuration;
    private Sequence _sequence;
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void OnEnable()
    {
        Debug.Log($"<color=orange>[StartOfDayPanel] OnEnable called. _pauseCount before: {MainUIManager.Instance?.pauseCount ?? -1}</color>");
        UpdatePanelInfo();
        UpdateBackground(); // Обновляем фон при открытии
    }
    
    // --- МЕТОД ОБНОВЛЕНИЯ ФОНА ---
    private void UpdateBackground()
    {
        if (backgroundImage == null || TimeManager.Instance == null || backgroundVisuals == null) return;

        var currentPeriod = TimeManager.Instance.GetCurrentPeriodType();

        // Ищем подходящий фон (проверка битовой маски)
        foreach (var bg in backgroundVisuals)
        {
            if ((bg.periodMask & currentPeriod) != 0)
            {
                if (bg.backgroundSprite != null)
                {
                    backgroundImage.sprite = bg.backgroundSprite;
                }
                return;
            }
        }
    }

    public void UpdatePanelInfo()
    {
        if (dayText != null && CalendarManager.Instance != null) 
            dayText.text = $"ДЕНЬ {CalendarManager.Instance.CurrentDay}";
        
        if (moneyText != null && PlayerWallet.Instance != null) 
            moneyText.text = $"${PlayerWallet.Instance.GetCurrentMoney()}";
        
        if (strikesText != null && DirectorManager.Instance != null) 
            strikesText.text = $"Ошибки: {DirectorManager.Instance.currentStrikes} / 3";

        // Обновление ошибок
        if (errorRateText != null && DocumentQualityManager.Instance != null && OrderManager.Instance != null)
        {
            float averageError = DocumentQualityManager.Instance.GetCurrentAverageErrorRate();
            float allowedError = OrderManager.Instance.currentMandates.Any()
                ? OrderManager.Instance.currentMandates[0].allowedDirectorErrorRate
                : 1f;

            errorRateText.text = $"Ошибки: {averageError:P0} / Норма: {allowedError:P0}";
            errorRateText.color = (averageError > allowedError) ? Color.red : Color.white;
            errorRateText.gameObject.SetActive(true);
        }

        // Обновление приказов
        if (activeOrdersText != null && OrderManager.Instance != null)
        {
            var dailyOrders = OrderManager.Instance.activeOrders.Select(o => o.orderName);
            var permanentOrders = OrderManager.Instance.activePermanentOrders.Select(o => o.orderName + " (Пост.)");
            var allActiveOrders = dailyOrders.Concat(permanentOrders);
            var activeOrders = allActiveOrders as string[] ?? allActiveOrders.ToArray();
            
            if (activeOrders.Any())
            {
                activeOrdersText.text = "<b>Активные приказы:</b>\n" + string.Join("\n", activeOrders);
                activeOrdersText.gameObject.SetActive(true);
            }
            else
            {
                activeOrdersText.gameObject.SetActive(false);
            }
        }

        // Кнопка начала дня
        if (startDayButton != null && TimeManager.Instance != null)
        {
            startDayButton.interactable = true;
            var buttonText = startDayButton.GetComponentInChildren<TextMeshProUGUI>();

            var currentPeriodPlan = TimeManager.Instance.GetCurrentPeriodSettings();
            
            // Здесь была ошибка с лишними символами, исправлено:
            var isMidDayPause = Time.timeScale == 0f &&
                                currentPeriodPlan != null &&
                                !currentPeriodPlan.PeriodType.IsNight(); 

            buttonText.text = isMidDayPause ? "Продолжить день" : "Начать день";

            startDayButton.onClick.RemoveAllListeners();
            startDayButton.onClick.AddListener(() => {
                MainUIManager.Instance.StartOrResumeGameplay();
            });
        }

        if (directorDeskButton != null)
        {
            directorDeskButton.UpdateAppearance(GetWaitingDocumentCount());
        }
    }

    public void CreateDocumentIcon(ClientPathfinding client)
    {
        if (documentIconPrefab == null || documentIconsContainer == null || waitingDocumentIcons.ContainsKey(client)) return;
        
        GameObject iconGO = Instantiate(documentIconPrefab, documentIconsContainer);
        DirectorDocumentIcon icon = iconGO.GetComponent<DirectorDocumentIcon>();
        
        if (icon != null) { 
            icon.Setup(client, reviewPanel); 
            waitingDocumentIcons.Add(client, icon); 

            if(directorDeskButton != null)
            {
                directorDeskButton.UpdateAppearance(GetWaitingDocumentCount());
            }
        }
    }
    
    public void RegisterDirectorDocument(ClientPathfinding client)
    {
        CreateDocumentIcon(client);
    }
	
	public void RegisterProjectDocument(Data.Documents.ProjectDocumentDefinition docData, System.Action onSignedWorldCallback)
    {
        if (docData == null || activeProjectIcons.ContainsKey(docData)) return;

        // Создаем иконку в DocumentContainer (туда же, где клиентские документы)
        // documentIconsContainer - это существующее поле в вашем скрипте (надеюсь, оно public или SerializeField)
        if (documentIconsContainer == null) return;

        GameObject iconGO = Instantiate(projectDocIconPrefab, documentIconsContainer);
        var iconUI = iconGO.GetComponent<UI.ProjectDocumentIconUI>();
        
        if (iconUI != null)
        {
            iconUI.Setup(docData, (doc) => 
            {
                // При клике на иконку открываем панель просмотра
                projectReviewPanel.Show(doc, () => 
                {
                    // Когда подписали в панели:
                    
                    // 1. Убираем иконку из UI
                    RemoveProjectDocumentIcon(doc);
                    
                    // 2. Сообщаем физическому миру (Callback в ProjectDocumentObject)
                    onSignedWorldCallback?.Invoke();
                    
                    // 3. Обновляем счетчик на кнопке стола
                    if(directorDeskButton != null) directorDeskButton.UpdateAppearance(GetWaitingDocumentCount());
                });
            });
        }

        activeProjectIcons.Add(docData, iconGO);
        
        // Обновляем кнопку стола (красный кружочек с цифрой)
        if(directorDeskButton != null) directorDeskButton.UpdateAppearance(GetWaitingDocumentCount());
    }

    public void RemoveDocumentIcon(ClientPathfinding client)
    {
        if (client != null && waitingDocumentIcons.ContainsKey(client))
        {
            DirectorDocumentIcon iconToRemove = waitingDocumentIcons[client];
            waitingDocumentIcons.Remove(client);
            if(iconToRemove != null) Destroy(iconToRemove.gameObject);

            if(directorDeskButton != null)
            {
                directorDeskButton.UpdateAppearance(GetWaitingDocumentCount());
            }
        }
    }
	
	public void RemoveProjectDocumentIcon(Data.Documents.ProjectDocumentDefinition docData)
    {
        if (activeProjectIcons.ContainsKey(docData))
        {
            Destroy(activeProjectIcons[docData]);
            activeProjectIcons.Remove(docData);
        }
    }

    public int GetWaitingDocumentCount() { return waitingDocumentIcons.Count + activeProjectIcons.Count; }
}