using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DirectorManager : MonoBehaviour
{
    public static DirectorManager Instance { get; set; }

    [Header("Настройки приказов")]
    [Tooltip("Перетащите сюда ВСЕ ассеты приказов (DirectorOrder), которые существуют в игре.")]
    public List<DirectorOrder> allPossibleOrders;

    [Header("Состояние игры")]
    public List<DirectorOrder> activeOrders = new List<DirectorOrder>();
    public List<DirectorOrder> activePermanentOrders = new List<DirectorOrder>();
    public List<DirectorOrder> completedOneTimeOrders = new List<DirectorOrder>();
    public List<DirectorOrder> currentMandates = new List<DirectorOrder>();
    public List<DirectorOrder> offeredOrders = new List<DirectorOrder>();
    public int currentStrikes = 0;

    private void Awake()
    {
        // --- Наш отладочный хук ---
        Debug.Log($"<b><color=purple>[DirectorManager AWAKE] на объекте '{this.gameObject.name}'. ID: {this.gameObject.GetInstanceID()}</color></b>");

        if (Instance == null)
        {
            Debug.Log($"<color=purple>[DirectorManager] Instance был пуст. Теперь я ({this.gameObject.GetInstanceID()}) - главный.</color>");
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.Log($"<color=purple>[DirectorManager] Instance уже занят ({Instance.gameObject.GetInstanceID()}). Я ({this.gameObject.GetInstanceID()}) самоуничтожаюсь.</color>");
            Destroy(gameObject);
        }
    }

    public List<DirectorOrder> GetAvailableOrdersForDay()
    {
        // --- Наш отладочный хук ---
        Debug.Log($"<color=purple>[DirectorManager GET ORDERS] Я ({this.gameObject.GetInstanceID()}) генерирую приказы.</color>");
        
        if (allPossibleOrders == null || allPossibleOrders.Count == 0)
        {
            Debug.LogError("<b>[DirectorManager]</b> Список 'All Possible Orders' пуст! Не могу выбрать приказы на день. Проверьте инспектор.", this);
            return new List<DirectorOrder>();
        }
        
        var orderPool = new List<DirectorOrder>(allPossibleOrders);
        offeredOrders.Clear();
        int numberOfChoices = 3;
        for (int i = 0; i < numberOfChoices && orderPool.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, orderPool.Count);
            offeredOrders.Add(orderPool[randomIndex]);
            orderPool.RemoveAt(randomIndex);
        }
        return offeredOrders;
    }

    public void SelectOrder(DirectorOrder selectedOrder)
    {
        // --- Наш отладочный хук ---
        Debug.Log($"<b><color=purple>[DirectorManager SELECT ORDER] Я ({this.gameObject.GetInstanceID()}) очищаю список offeredOrders.</color></b>");
        
        if (!activeOrders.Contains(selectedOrder))
        {
            activeOrders.Add(selectedOrder);
        }
        if (!activePermanentOrders.Contains(selectedOrder))
        {
             activePermanentOrders.Add(selectedOrder);
        }
        
        offeredOrders.Clear();
    }
    
    public void AddStrike()
    {
        currentStrikes++;
        Debug.Log($"Получена ошибка! Всего ошибок: {currentStrikes}");
    }
    
    public void PrepareDay()
    {
        // Логика подготовки к новому дню
    }

    public void ResetState()
    {
        activeOrders.Clear();
        activePermanentOrders.Clear();
        completedOneTimeOrders.Clear();
        currentMandates.Clear();
        offeredOrders.Clear();
        currentStrikes = 0;
    }
	
	public void EvaluateEndOfDayStrikes()
{
    if (DocumentQualityManager.Instance == null) return;

    float averageError = DocumentQualityManager.Instance.GetCurrentAverageErrorRate();
    float allowedError = 1.0f; // 100% по умолчанию

    // Берем норму из активного приказа, если он есть
    if (currentMandates.Any())
    {
        allowedError = currentMandates[0].allowedDirectorErrorRate;
    }

    Debug.Log($"[End of Day] Проверка ошибок. Среднее: {averageError:P1}, Норма: {allowedError:P1}");
    if (averageError > allowedError)
    {
        AddStrike();
        Debug.LogWarning($"[End of Day] СТРАЙК! Среднее количество ошибок превысило норму.");
    }

    // Сбрасываем счетчик ошибок для следующего дня
    DocumentQualityManager.Instance.ResetDay();
}
	
}