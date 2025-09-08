// Файл: DirectorManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DirectorManager : MonoBehaviour
{
    public static DirectorManager Instance { get; private set; }

    [Header("Настройки")]
    [Tooltip("Список ВСЕХ доступных приказов директора в игре.")]
    public List<DirectorOrder> allOrders;
    public List<DailyMandates> allMandates;
    
    [Header("Текущее состояние")]
    public DailyMandates currentMandates;
    public int currentStrikes = 0;
    
    public List<DirectorOrder> activeOrders = new List<DirectorOrder>();
    public List<DirectorOrder> activePermanentOrders = new List<DirectorOrder>();
    public List<DirectorOrder> offeredOrders = new List<DirectorOrder>();
    public List<DirectorOrder> completedOneTimeOrders = new List<DirectorOrder>();

    [Header("Документы Директора")]
    public float directorDocumentInterval = 120f;
    public GameObject directorDocumentPrefab;
    private List<DirectorDocument> directorDocumentsInPlay = new List<DirectorDocument>();
    private float directorDocumentTimer = 0f;

    [Header("Ссылки на менеджеры")]
    public MenuManager menuManager;
    private bool isEndOfDaySequenceInitiated = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        
        if (directorDocumentPrefab != null)
        {
            directorDocumentTimer += Time.deltaTime;
            if (directorDocumentTimer >= directorDocumentInterval)
            {
                directorDocumentTimer = 0f;
                SpawnDirectorDocument();
            }
        }

        if (ClientSpawner.Instance != null && ClientSpawner.Instance.GetCurrentPeriod() != null && !isEndOfDaySequenceInitiated)
        {
            string currentPeriodName = ClientSpawner.CurrentPeriodName;
            if (currentPeriodName != null && currentPeriodName.ToLower().Trim() == "вечер")
            {
                float timeLeft = ClientSpawner.Instance.GetCurrentPeriod().durationInSeconds - ClientSpawner.Instance.GetPeriodTimer();
                if (timeLeft <= 10f)
                {
                    isEndOfDaySequenceInitiated = true;
                    activeOrders.Clear();
                    menuManager?.OnOpenDirectorsOfficeClicked();
                }
            }
        }
    }

    public void PrepareDay()
    {
        // --- ДИАГНОСТИКА НАЧАЛАСЬ ---
        Debug.Log("--- DirectorManager: Начинаю подготовку нового дня (PrepareDay) ---");

        isEndOfDaySequenceInitiated = false;
        activeOrders.Clear();
        ApplyAllActiveEffects();

        int dailyIncome = 0;
        foreach (var permanentOrder in activePermanentOrders)
        {
            dailyIncome += permanentOrder.permanentDailyIncome;
        }
        if (dailyIncome > 0 && PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.AddMoney(dailyIncome, Vector3.zero);
            Debug.Log($"Получен ежедневный доход от приказов: ${dailyIncome}");
        }
        if (allMandates != null && allMandates.Count > 0)
        {
            currentMandates = allMandates[Random.Range(0, allMandates.Count)];
        }

        offeredOrders.Clear();
        
        Debug.Log($"Всего приказов в игре (allOrders): {allOrders.Count}");
        Debug.Log($"Уже выполненных уникальных приказов (completedOneTimeOrders): {completedOneTimeOrders.Count}");

        var availableOrders = allOrders.Except(completedOneTimeOrders).ToList();
        Debug.Log($"Приказов доступно для выбора (availableOrders): {availableOrders.Count}");

        for (int i = 0; i < 3; i++)
        {
            Debug.Log($"--> Итерация цикла выбора приказа #{i}");
            if (availableOrders.Count == 0)
            {
                Debug.LogWarning("Доступные приказы закончились. Прерываю цикл.");
                break;
            }

            float totalWeight = availableOrders.Sum(order => order.selectionWeight);
            float randomWeight = Random.Range(0, totalWeight);
            
            DirectorOrder selectedOrder = null;
            foreach (var order in availableOrders)
            {
                randomWeight -= order.selectionWeight;
                if (randomWeight <= 0)
                {
                    selectedOrder = order;
                    break;
                }
            }
            
            if (selectedOrder != null)
            {
                Debug.Log($"Выбран приказ '{selectedOrder.name}'. Добавляю в список предлагаемых.");
                offeredOrders.Add(selectedOrder);
                availableOrders.Remove(selectedOrder);
            }
            else
            {
                 Debug.LogError("Не удалось выбрать приказ по весу! Этого не должно было случиться.");
            }
        }
        Debug.Log($"--- DirectorManager: Подготовка завершена. Итого предложено приказов: {offeredOrders.Count} ---");
        // --- ДИАГНОСТИКА ОКОНЧЕНА ---
    }
    
    public void SelectOrder(DirectorOrder order)
    {
        if (order == null) return;
        Debug.Log($"Выбран приказ: {order.orderName} (Длительность: {order.duration})");

        if (order.oneTimeMoneyBonus != 0 && PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.AddMoney(order.oneTimeMoneyBonus, Vector3.zero);
            Debug.Log($"Получен разовый бонус: ${order.oneTimeMoneyBonus}");
        }
        if (order.removeStrike)
        {
            currentStrikes = Mathf.Max(0, currentStrikes - 1);
            Debug.Log("Один страйк снят!");
        }

        if (order.duration == OrderDuration.Permanent)
        {
            if (!activePermanentOrders.Contains(order))
                activePermanentOrders.Add(order);
        }
        else
        {
            activeOrders.Add(order);
        }
        
        if (order.isOneTimeOnly)
        {
            if (!completedOneTimeOrders.Contains(order))
                completedOneTimeOrders.Add(order);
        }

        ApplyAllActiveEffects();
        offeredOrders.Clear();
    }
    
    public void ApplyAllActiveEffects()
    {
        DirectorOrder combinedEffects = ScriptableObject.CreateInstance<DirectorOrder>();
        var allActiveOrders = activePermanentOrders.Concat(activeOrders);

        foreach (var order in allActiveOrders)
        {
            combinedEffects.staffMoveSpeedMultiplier *= order.staffMoveSpeedMultiplier;
            combinedEffects.clientSpawnRateMultiplier *= order.clientSpawnRateMultiplier;
            combinedEffects.clerkStressGainMultiplier *= order.clerkStressGainMultiplier;
            combinedEffects.clientPatienceMultiplier *= order.clientPatienceMultiplier;
            combinedEffects.messGenerationMultiplier *= order.messGenerationMultiplier;
            combinedEffects.staffStressReliefMultiplier *= order.staffStressReliefMultiplier;
            if (order.disableGuards) combinedEffects.disableGuards = true;
        }

        if (ClientSpawner.Instance != null)
        {
            ClientSpawner.Instance.ApplyOrderEffects(combinedEffects);
        }
        
        StaffController[] allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
        foreach (var staff in allStaff)
        {
            AgentMover mover = staff.GetComponent<AgentMover>();
            mover?.ApplySpeedMultiplier(combinedEffects.staffMoveSpeedMultiplier);
        }
    }
    
    public void ResetState()
    {
        currentStrikes = 0;
        activeOrders.Clear();
        activePermanentOrders.Clear();
        completedOneTimeOrders.Clear();
        offeredOrders.Clear();
        currentMandates = null;
        foreach(var doc in directorDocumentsInPlay)
        {
            if (doc != null) Destroy(doc.gameObject);
        }
        directorDocumentsInPlay.Clear();
        directorDocumentTimer = 0f;
        isEndOfDaySequenceInitiated = false;
    }

    public void AddStrike()
    {
        currentStrikes++;
        Debug.Log($"Директор получил страйк! Текущее количество: {currentStrikes}");
        if (currentStrikes >= 3)
        {
            Debug.Log("GAME OVER: Внеплановая проверка!");
        }
    }
    
    public void CheckDailyMandates()
    {
        if (currentMandates == null)
        {
            Debug.LogWarning("Нормы дня не установлены!");
            return;
        }

        bool failedMandate = false;
        if (ArchiveManager.Instance != null && ArchiveManager.Instance.GetCurrentDocumentCount() > currentMandates.maxArchiveDocumentCount)
        {
            Debug.Log($"Норма дня не выполнена: слишком много документов в архиве ({ArchiveManager.Instance.GetCurrentDocumentCount()}/{currentMandates.maxArchiveDocumentCount})");
            AddStrike();
            failedMandate = true;
        }

        DocumentStack[] allStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
        foreach (var stack in allStacks)
        {
            if (ArchiveManager.Instance != null && stack == ArchiveManager.Instance.mainDocumentStack) continue;
            if (stack.CurrentSize > currentMandates.maxDeskDocumentCount)
            {
                Debug.Log($"Норма дня не выполнена: слишком много документов на столе {stack.name} ({stack.CurrentSize}/{currentMandates.maxDeskDocumentCount})");
                AddStrike();
                failedMandate = true;
                break;
            }
        }
        
        if (ClientPathfinding.clientsExitedProcessed < currentMandates.minProcessedClients)
        {
            Debug.Log($"Норма дня не выполнена: обслужено слишком мало клиентов ({ClientPathfinding.clientsExitedProcessed}/{currentMandates.minProcessedClients})");
            AddStrike();
            failedMandate = true;
        }
        
        if (ClientPathfinding.clientsExitedAngry > currentMandates.maxUpsetClients)
        {
            Debug.Log($"Норма дня не выполнена: слишком много недовольных клиентов ({ClientPathfinding.clientsExitedAngry}/{currentMandates.maxUpsetClients})");
            AddStrike();
            failedMandate = true;
        }

        if (!failedMandate)
        {
            Debug.Log("Все нормы дня выполнены! Отличная работа, Директор!");
        }
    }
    
    public void EndDayAndStartNew()
    {
        CheckDailyMandates();
        ClientSpawner.Instance.GoToNextPeriod();
    }
    
    public void SpawnDirectorDocument()
    {
        if (directorDocumentPrefab == null) return;
        Vector3 spawnPosition = Vector3.zero;
        GameObject docGO = Instantiate(directorDocumentPrefab, spawnPosition, Quaternion.identity);
        
        DirectorDocument newDoc = docGO.GetComponent<DirectorDocument>();
        if (newDoc != null)
        {
            newDoc.hiddenErrorsCount = Random.Range(0, 11);
            if (currentMandates != null)
            {
                newDoc.allowedErrorRate = currentMandates.allowedDirectorErrorRate;
            }
            newDoc.isCorrupted = (Random.value < 0.2f);
            if (newDoc.isCorrupted)
            {
                newDoc.bribeAmount = Random.Range(50, 201);
            }
            directorDocumentsInPlay.Add(newDoc);
        }
    }
}