// Assets/Scripts/Managers/AssignmentManager.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Characters; // Для доступа к StaffController.Role

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
            
            // Если место было занято кем-то другим, снимаем того человека
            if (assignments.ContainsKey(workstation))
            {
                var previousOwner = assignments[workstation];
                if (previousOwner != null)
                {
                    previousOwner.assignedWorkstation = null;
                }
                assignments.Remove(workstation);
            }

            assignments[workstation] = staff;
            staff.assignedWorkstation = workstation;
            Debug.Log($"[AssignmentManager] Сотрудник {staff.characterName} назначен на {workstation.name}");
        }

        // --- НОВЫЙ МЕТОД: Автоматическое назначение ---
        public bool AutoAssignStaff(StaffController staff)
        {
            if (staff == null) return false;

            // Если уже есть место, ничего не делаем
            if (staff.assignedWorkstation != null) return true;

            // Ищем все точки
            var allPoints = ScenePointsRegistry.Instance.allServicePoints;
            
            // Фильтруем точки, подходящие по роли
            var suitablePoints = allPoints.Where(p => IsPointSuitableForRole(p, staff.currentRole)).ToList();

            // Ищем первую свободную
            foreach (var point in suitablePoints)
            {
                if (!assignments.ContainsKey(point) || assignments[point] == null)
                {
                    AssignStaffToWorkstation(staff, point);
                    return true;
                }
            }

            return false;
        }

        private bool IsPointSuitableForRole(ServicePoint point, StaffController.Role role)
        {
            // Хардкод ID столов согласно вашей конфигурации сцены
            switch (role)
            {
                case StaffController.Role.Registrar: 
                    return point.deskId == 0;
                
                case StaffController.Role.Clerk: 
                    return point.deskId == 1 || point.deskId == 2;
                
                case StaffController.Role.Cashier: 
                case StaffController.Role.Accountant: // Бухгалтер тоже может сидеть в кассе
                    return point.deskId == -1 || point.deskId == 4;
                
                case StaffController.Role.Archivist: 
                    return point.deskId == 3;
                
                case StaffController.Role.Guard:
                    // Охранник привязывается к посту, если это ServicePoint
                    return point == ScenePointsRegistry.Instance.guardPostPoint;

                default: 
                    return false;
            }
        }
        // ----------------------------------------------

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
                if(staff != null) staff.assignedWorkstation = null;
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