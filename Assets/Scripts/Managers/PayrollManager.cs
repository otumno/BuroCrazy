// Файл: PayrollManager.cs
using UnityEngine;
using System.Linq;

public class PayrollManager : MonoBehaviour
{
    public static PayrollManager Instance { get; set; }

    private string lastCheckedPeriod = "";

    void Update()
    {
        if (ClientSpawner.Instance == null) return;

        string currentPeriod = ClientSpawner.CurrentPeriodName;
        
        // Проверяем, сменился ли период
        if (currentPeriod != lastCheckedPeriod && lastCheckedPeriod != "")
        {
            PaySalariesForPeriod(lastCheckedPeriod);
        }

        lastCheckedPeriod = currentPeriod;
    }

    private void PaySalariesForPeriod(string periodName)
    {
        if (PlayerWallet.Instance == null) return;

        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
        int totalPayroll = 0;

        foreach (var staff in allStaff)
        {
            // Проверяем, работал ли сотрудник в прошедшем периоде
            if (staff.workPeriods.Any(p => p.Equals(periodName, System.StringComparison.InvariantCultureIgnoreCase)))
            {
                PlayerWallet.Instance.AddMoney(-staff.salaryPerPeriod, staff.transform.position);
                totalPayroll += staff.salaryPerPeriod;
            }
        }

        if (totalPayroll > 0)
        {
            Debug.Log($"[Payroll] Выплачена зарплата за период '{periodName}': ${totalPayroll}");
        }
    }
}