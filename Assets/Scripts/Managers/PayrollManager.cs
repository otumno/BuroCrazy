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
    var allStaff = HiringManager.Instance.AllStaff; // Получаем список всех сотрудников
    if (allStaff == null) return;

    int totalDebtAccrued = 0;

    foreach (var staff in allStaff)
    {
        if (staff == null) continue;

        // Проверяем, работал ли сотрудник в прошедшем периоде
        if (staff.workPeriods.Any(p => p.Equals(periodName, System.StringComparison.InvariantCultureIgnoreCase)))
        {
            // Вместо списания денег, увеличиваем счетчик долга
            staff.unpaidPeriods++;
            totalDebtAccrued += staff.salaryPerPeriod;
        }
    }

    if (totalDebtAccrued > 0)
    {
        Debug.Log($"[Payroll] Начислен долг по зарплате за период '{periodName}': ${totalDebtAccrued}.");
    }
}
}