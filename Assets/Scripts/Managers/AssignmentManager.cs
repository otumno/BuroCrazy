using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        if (assignments.ContainsValue(staff))
        {
            var oldWorkstation = assignments.First(kvp => kvp.Value == staff).Key;
            assignments.Remove(oldWorkstation);
        }

        assignments[workstation] = staff;
        staff.assignedWorkstation = workstation;
        Debug.Log($"[AssignmentManager] Сотрудник {staff.characterName} назначен на {workstation.name}");
    }

    // Метод для снятия назначения
    public void UnassignStaff(StaffController staff)
    {
        if (staff == null || !assignments.ContainsValue(staff)) return;

        var workstation = assignments.First(kvp => kvp.Value == staff).Key;
        assignments.Remove(workstation);
        staff.assignedWorkstation = null;
         Debug.Log($"[AssignmentManager] Сотрудник {staff.characterName} снят с рабочего места {workstation.name}");
    }

    // Получить сотрудника, назначенного на конкретное место
    public StaffController GetAssignedStaff(ServicePoint workstation)
    {
        assignments.TryGetValue(workstation, out StaffController staff);
        return staff;
    }
}