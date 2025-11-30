using System.Collections.Generic;
using UnityEngine;

namespace Managers
{
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager Instance { get; private set; }

        [Header("Настройки приказов")]
        [Tooltip("Перетащите сюда ВСЕ ассеты приказов (DirectorOrder).")]
        public List<DirectorOrder> allPossibleOrders;

        [Header("Состояние (Runtime)")]
        public List<DirectorOrder> activeOrders = new List<DirectorOrder>();
        public List<DirectorOrder> activePermanentOrders = new List<DirectorOrder>();
        public List<DirectorOrder> completedOneTimeOrders = new List<DirectorOrder>();
        public List<DirectorOrder> currentMandates = new List<DirectorOrder>();
        public List<DirectorOrder> offeredOrders = new List<DirectorOrder>();

        private void Awake()
        {
            if (Instance == null) { Instance = this; }
            else if (Instance != this) { Destroy(gameObject); }
        }

        public List<DirectorOrder> GetAvailableOrdersForDay()
        {
            if (allPossibleOrders == null || allPossibleOrders.Count == 0)
            {
                Debug.LogError("[OrderManager] Список 'All Possible Orders' пуст!");
                return new List<DirectorOrder>();
            }

            var orderPool = new List<DirectorOrder>(allPossibleOrders);
            // Исключаем уже выполненные одноразовые приказы
            orderPool.RemoveAll(o => completedOneTimeOrders.Contains(o));

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
            if (!activeOrders.Contains(selectedOrder))
            {
                activeOrders.Add(selectedOrder);
            }
            if (selectedOrder.duration == OrderDuration.Permanent && !activePermanentOrders.Contains(selectedOrder))
            {
                activePermanentOrders.Add(selectedOrder);
            }
            if (selectedOrder.isOneTimeOnly && !completedOneTimeOrders.Contains(selectedOrder))
            {
                completedOneTimeOrders.Add(selectedOrder);
            }

            // Применяем мгновенные эффекты
            if (selectedOrder.oneTimeMoneyBonus > 0 && PlayerWallet.Instance != null)
            {
                PlayerWallet.Instance.AddMoney(selectedOrder.oneTimeMoneyBonus, $"Приказ: {selectedOrder.orderName}");
            }
            
            // Сброс списка предложенных
            offeredOrders.Clear();
        }

        public void ResetState()
        {
            activeOrders.Clear();
            activePermanentOrders.Clear();
            completedOneTimeOrders.Clear();
            currentMandates.Clear();
            offeredOrders.Clear();
        }
    }
}