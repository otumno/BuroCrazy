using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DirectorManager : MonoBehaviour
{
    // Сделали сеттер публичным для совместимости с SystemBootstrapper
    public static DirectorManager Instance { get; set; } 

    [Header("Настройки Приказов")]
    [SerializeField] public List<DirectorOrder> allOrders;

    [Header("Настройки Норм Дня")]
    [SerializeField] public List<DailyMandates> allPossibleMandates; // Список всех возможных норм
    public DailyMandates currentMandates { get; private set; } // Нормы на текущий день

    [Header("Состояние Директора")]
    public int currentStrikes = 0;
    
    public List<DirectorOrder> activeOrders = new List<DirectorOrder>();
    public List<DirectorOrder> offeredOrders = new List<DirectorOrder>();
    public List<DirectorOrder> activePermanentOrders = new List<DirectorOrder>();
    public List<DirectorOrder> completedOneTimeOrders = new List<DirectorOrder>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PrepareDay()
    {
        activeOrders.Clear();
        // В начале дня выбираем случайную норму из списка
        if (allPossibleMandates != null && allPossibleMandates.Count > 0)
        {
            currentMandates = allPossibleMandates[Random.Range(0, allPossibleMandates.Count)];
        }
    }

    public void ResetState()
    {
        currentStrikes = 0;
        activeOrders.Clear();
        offeredOrders.Clear();
        activePermanentOrders.Clear();
        completedOneTimeOrders.Clear();
    }
    
    // --- НОВЫЙ МЕТОД ---
    public void AddStrike()
    {
        currentStrikes++;
        Debug.LogWarning($"Директор получил страйк! Всего страйков: {currentStrikes}");
        // TODO: Добавить логику Game Over при currentStrikes >= 3
    }

    // ... (остальные методы GetRandomOrders, SelectOrder) ...
    public List<DirectorOrder> GetRandomOrders(int count)
    {
        offeredOrders.Clear();
        if (allOrders == null || allOrders.Count == 0) return offeredOrders;
        var availableOrders = allOrders.Where(o => !o.isOneTimeOnly || !completedOneTimeOrders.Contains(o)).ToList();
        var randomOrders = availableOrders.OrderBy(x => Random.value * (1f / x.selectionWeight)).Take(count).ToList();
        offeredOrders.AddRange(randomOrders);
        return offeredOrders;
    }
    public void SelectOrder(DirectorOrder selectedOrder)
    {
        if (selectedOrder == null) return;
        Debug.Log($"Выбран приказ: {selectedOrder.orderName}");
        if(selectedOrder.duration == OrderDuration.SingleDay) { activeOrders.Add(selectedOrder); }
        else if (selectedOrder.duration == OrderDuration.Permanent) { if (!activePermanentOrders.Contains(selectedOrder)) { activePermanentOrders.Add(selectedOrder); } }
        if(selectedOrder.isOneTimeOnly) { if (!completedOneTimeOrders.Contains(selectedOrder)) { completedOneTimeOrders.Add(selectedOrder); } }
        if(selectedOrder.oneTimeMoneyBonus != 0) { PlayerWallet.Instance.AddMoney(selectedOrder.oneTimeMoneyBonus, Vector3.zero); }
        if(selectedOrder.removeStrike && currentStrikes > 0) { currentStrikes--; }
        offeredOrders.Clear();
    }
}