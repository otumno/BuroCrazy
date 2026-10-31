using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StaffOrders
{
    /// <summary>
    /// Все приказы, которые директор может отдать по ПКМ на сотруднике, и общие настройки силы приказа.
    /// </summary>
    // Один ассет-каталог, а не флаги на каждом StaffAction: список приказов настраивается в одном месте,
    // и одно действие можно отдать разными пунктами с разными ролями и ценой стресса.
    [CreateAssetMenu(fileName = "StaffOrderCatalog", menuName = "Bureau/Staff Order Catalog")]
    public class StaffOrderCatalog : ScriptableObject
    {
        /// <summary>
        /// Бонус к полезности действия в момент приказа. Должен перебивать любые обычные веса utility AI.
        /// </summary>
        [field: SerializeField]
        [field: Tooltip("Бонус к полезности действия в момент приказа. Должен перебивать обычные веса utility AI.")]
        public float InitialPriority { get; private set; } = StaffOrder.DefaultInitialPriority;

        /// <summary>
        /// За сколько секунд игрового времени бонус приказа падает до нуля.
        /// </summary>
        [field: SerializeField]
        [field: Tooltip("За сколько секунд игрового времени бонус приказа падает до нуля.")]
        public float PriorityDecaySeconds { get; private set; } = StaffOrder.DefaultDecaySeconds;

        [field: SerializeField]
        public List<StaffOrderDefinition> Orders { get; private set; } = new List<StaffOrderDefinition>();

        public List<StaffOrderDefinition> GetOrdersFor(StaffController staff)
        {
            return Orders.Where(order => order != null && order.IsAvailableFor(staff)).ToList();
        }

        public StaffOrder CreateOrder(StaffOrderDefinition definition)
        {
            return new StaffOrder(definition.Action, InitialPriority, PriorityDecaySeconds, definition.StressCost);
        }
    }
}
