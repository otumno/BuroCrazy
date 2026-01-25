using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Enums;
using Gameplay;
using Managers;
using Characters;

namespace Managers
{
    public class EquipmentManager : MonoBehaviour
    {
        public static EquipmentManager Instance { get; private set; }

        private List<OfficeEquipment> allEquipment = new List<OfficeEquipment>();
        private Dictionary<EquipmentType, List<OfficeEquipment>> equipmentByType = new Dictionary<EquipmentType, List<OfficeEquipment>>();

        [Header("Настройки")]
        public bool debugMode = false;

        public event System.Action<OfficeEquipment> OnEquipmentBroken;
        public event System.Action<OfficeEquipment> OnEquipmentRepaired;
        public event System.Action<OfficeEquipment> OnEquipmentRegistered;
        public event System.Action<OfficeEquipment> OnEquipmentUnregistered;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void RegisterEquipment(OfficeEquipment equipment)
        {
            if (allEquipment.Contains(equipment)) return;

            allEquipment.Add(equipment);

            if (!equipmentByType.ContainsKey(equipment.equipmentType))
            {
                equipmentByType[equipment.equipmentType] = new List<OfficeEquipment>();
            }
            equipmentByType[equipment.equipmentType].Add(equipment);

            OnEquipmentRegistered?.Invoke(equipment);

            if (debugMode)
            {
                Debug.Log($"[EquipmentManager] Зарегистрировано оборудование: {equipment.name} (Тип: {equipment.equipmentType})");
            }
        }

        public void UnregisterEquipment(OfficeEquipment equipment)
        {
            if (!allEquipment.Contains(equipment)) return;

            allEquipment.Remove(equipment);

            if (equipmentByType.ContainsKey(equipment.equipmentType))
            {
                equipmentByType[equipment.equipmentType].Remove(equipment);
            }

            OnEquipmentUnregistered?.Invoke(equipment);

            if (debugMode)
            {
                Debug.Log($"[EquipmentManager] Оборудование удалено: {equipment.name}");
            }
        }

        public OfficeEquipment GetEquipment(EquipmentType type, Vector3 workerPosition)
        {
            if (!equipmentByType.ContainsKey(type) || equipmentByType[type].Count == 0)
            {
                return null;
            }

            var workingEquipment = equipmentByType[type]
                .Where(e => e.IsWorking())
                .OrderBy(e => Vector3.Distance(workerPosition, e.transform.position))
                .ToList();

            return workingEquipment.FirstOrDefault();
        }

        public OfficeEquipment GetAnyWorkingEquipment(EquipmentType type)
        {
            if (!equipmentByType.ContainsKey(type)) return null;

            return equipmentByType[type].FirstOrDefault(e => e.IsWorking());
        }

        public bool HasWorkingEquipment(EquipmentType type)
        {
            if (!equipmentByType.ContainsKey(type)) return false;

            return equipmentByType[type].Any(e => e.IsWorking());
        }

        public List<OfficeEquipment> GetAllWorkingEquipment()
        {
            return allEquipment.Where(e => e.IsWorking()).ToList();
        }

        public int GetWorkingEquipmentCount(EquipmentType type)
        {
            if (!equipmentByType.ContainsKey(type)) return 0;

            return equipmentByType[type].Count(e => e.IsWorking());
        }

        public OfficeEquipment GetNearestBrokenEquipment(Vector3 workerPosition)
        {
            return allEquipment
                .Where(e => !e.IsWorking())
                .OrderBy(e => Vector3.Distance(workerPosition, e.transform.position))
                .FirstOrDefault();
        }

        public List<OfficeEquipment> GetAllEquipment()
        {
            return new List<OfficeEquipment>(allEquipment);
        }

        public void RepairAllEquipment()
        {
            foreach (var equipment in allEquipment)
            {
                equipment.RepairEquipment();
            }

            Debug.Log($"[EquipmentManager] Всё оборудование отремонтировано. Всего: {allEquipment.Count}");
        }

        public void ReportBrokenEquipment()
        {
            var brokenEquipment = allEquipment.Where(e => !e.IsWorking()).ToList();

            if (brokenEquipment.Count > 0)
            {
                Debug.LogWarning($"[EquipmentManager] Сломанное оборудование ({brokenEquipment.Count}):");
                foreach (var e in brokenEquipment)
                {
                    Debug.LogWarning($"  - {e.name} (Тип: {e.equipmentType})");
                }
            }
        }
    }
}
