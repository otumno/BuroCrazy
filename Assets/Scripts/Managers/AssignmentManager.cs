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
            if (GetAssignedStaff(workstation) == staff && staff.assignedWorkstation == workstation) return;

            // Если сотрудник раньше был на другом месте, освобождаем его
            var oldAssignment = assignments.FirstOrDefault(kvp => kvp.Value == staff);
            if (oldAssignment.Key != null)
            {
                if (staff is IServiceProvider provider) ClientSpawner.UnassignServiceProviderFromDesk(oldAssignment.Key, provider);
                oldAssignment.Key.ClearAssignedStaff();
                assignments.Remove(oldAssignment.Key);
            }
            
            // Если место было занято кем-то другим, снимаем того человека
            if (assignments.ContainsKey(workstation))
            {
                var previousOwner = assignments[workstation];
                if (previousOwner != null)
                {
                    if (previousOwner is IServiceProvider displaced) ClientSpawner.UnassignServiceProviderFromDesk(workstation, displaced);
                    previousOwner.assignedWorkstation = null;
                }
                assignments.Remove(workstation);
            }

            assignments[workstation] = staff;
            staff.assignedWorkstation = workstation;
            workstation.SetAssignedStaff(staff);
            Debug.Log($"[AssignmentManager] Сотрудник {staff.characterName} назначен на {workstation.name}");
        }

        // --- НОВЫЙ МЕТОД: Автоматическое назначение ---
        public bool AutoAssignStaff(StaffController staff)
        {
            if (staff == null) return false;

            // Если уже есть место, ничего не делаем
            if (staff.assignedWorkstation != null) return true;

            // Ищем все точки
            var allPoints = ScenePointsRegistry.Instance?.allServicePoints;
            if (allPoints == null) return false;
            
            // Фильтруем точки, подходящие по роли
            var suitablePoints = allPoints.Where(p => CanAssignTo(staff, p)).ToList();

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

        public static bool CanAssignTo(StaffController staff, ServicePoint point)
        {
            if (staff == null || point == null) return false;
            switch (staff.currentRole)
            {
                case StaffController.Role.Registrar: 
                    return point.deskId == 0 && point.clientStandPoint != null;
                
                case StaffController.Role.Clerk: 
                    var skills = staff.currentRank?.unlockedActions;
                    return point.deskId == 1 && (skills == null || skills.Any(a => a is ProcessDocumentCat1Action)) ||
                           point.deskId == 2 && (skills == null || skills.Any(a => a is ProcessDocumentCat2Action));
                
                case StaffController.Role.Cashier: 
                    return point.deskId == -1;
                
                case StaffController.Role.Accountant: 
                    return point.deskId == 4;
                
                case StaffController.Role.Archivist: 
                    return point.deskId == 3;

                // Офис-менеджер, Уборщик, Охранник, Стажер - НЕ имеют постоянного стола
                // Они используют патрульные точки или зоны
                case StaffController.Role.OfficeManager:
                case StaffController.Role.Janitor:
                case StaffController.Role.Guard:
                case StaffController.Role.Intern:
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
                if (staff is IServiceProvider provider) ClientSpawner.UnassignServiceProviderFromDesk(workstationAssignment.Key, provider);
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
                if (staff is IServiceProvider provider) ClientSpawner.UnassignServiceProviderFromDesk(workstation, provider);
                workstation.ClearAssignedStaff();
                if (staff != null) staff.assignedWorkstation = null;
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
