// Assets/Scripts/Managers/DurabilityManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Gameplay;

namespace Managers
{
    public class DurabilityManager : MonoBehaviour
    {
        public static DurabilityManager Instance { get; private set; }

        // Список всех объектов с прочностью на сцене
        private List<OfficeObjectDurability> allDurabilityObjects = new List<OfficeObjectDurability>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterObject(OfficeObjectDurability obj)
        {
            if (!allDurabilityObjects.Contains(obj))
            {
                allDurabilityObjects.Add(obj);
            }
        }

        public void UnregisterObject(OfficeObjectDurability obj)
        {
            if (allDurabilityObjects.Contains(obj))
            {
                allDurabilityObjects.Remove(obj);
            }
        }

        /// <summary>
        /// Возвращает true, если есть хоть один объект, требующий ремонта (здоровье < 100%).
        /// </summary>
        public bool HasObjectsToRepair()
        {
            // Сначала проверяем сломанные (критично), потом просто поврежденные
            return allDurabilityObjects.Any(d => d != null && d.currentHealth < d.maxHealth);
        }

        /// <summary>
        /// Находит приоритетную цель для ремонта относительно позиции уборщика.
        /// Приоритет: 1. Сломанные полностью (ближайший). 2. Поврежденные (ближайший).
        /// </summary>
        public OfficeObjectDurability GetPriorityRepairTarget(Vector3 workerPosition)
        {
            // 1. Сначала ищем полностью сломанные (IsUsable == false)
            var brokenObject = allDurabilityObjects
                .Where(d => d != null && !d.IsUsable())
                .OrderBy(d => Vector3.Distance(workerPosition, d.transform.position))
                .FirstOrDefault();

            if (brokenObject != null) return brokenObject;

            // 2. Если сломанных нет, ищем просто поврежденные
            var damagedObject = allDurabilityObjects
                .Where(d => d != null && d.currentHealth < d.maxHealth)
                .OrderBy(d => Vector3.Distance(workerPosition, d.transform.position))
                .FirstOrDefault();

            return damagedObject;
        }
    }
}