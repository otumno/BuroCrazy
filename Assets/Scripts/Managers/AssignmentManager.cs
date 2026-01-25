using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Managers
{
    public class AssignmentManager : MonoBehaviour
    {
        public static AssignmentManager Instance { get; private set; }

        // Наш главный справочник: "На каком столе -> какой сотрудник назначен"
        private Dictionary<ServicePoint, StaffController> assignments = new Dictionary<ServicePoint, StaffController>();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); } else { Instance = this; }
        }

        // Метод для назначения сотрудника на рабочее место
        public void AssignStaffToWorkstation(StaffController staff, ServicePoint workstation)
        {
            if (staff == null || workstation == null) return;

            // Если сотрудник раньше был на другом месте, освобождаем его
            var oldAssignment = assignments.FirstOrDefault(kvp => kvp.Value == staff);
            if (oldAssignment.Key != null)
            {
                assignments.Remove(oldAssignment.Key);
            }

            assignments[workstation] = staff;
            staff.assignedWorkstation = workstation;
            Debug.Log($"[AssignmentManager] Сотрудник {staff.characterName} назначен на {workstation.name}");
        }

        // Метод для снятия назначения
        public void UnassignStaff(StaffController staff)
        {
            if (staff == null) return;

            var workstationAssignment = assignments.FirstOrDefault(kvp => kvp.Value == staff);
            if (workstationAssignment.Key != null)
            {
                assignments.Remove(workstationAssignment.Key);
                workstationAssignment.Key.ClearAssignedStaff();
                staff.assignedWorkstation = null;
                Debug.Log($"[AssignmentManager] Сотрудник {staff.characterName} снят с рабочего места {workstationAssignment.Key.name}");
            }
        }

        // Метод для снятия назначения рабочего места
        public void UnassignWorkstation(ServicePoint workstation)
        {
            if (workstation == null) return;
            
            if (assignments.TryGetValue(workstation, out var staff))
            {
                assignments.Remove(workstation);
                staff.assignedWorkstation = null;
                Debug.Log($"[AssignmentManager] Рабочее место {workstation.name} освобождено");
            }
        }

        // Получить сотрудника, назначенного на конкретное место
        public StaffController GetAssignedStaff(ServicePoint workstation)
        {
            if (workstation == null) return null;
            assignments.TryGetValue(workstation, out StaffController staff);
            return staff;
        }
    }
}