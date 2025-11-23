// Файл: PayrollManager.cs
using System.Linq;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class PayStaffManager : MonoBehaviour
    {
        public static PayStaffManager Instance { get; set; }

        private CalendarDayPeriodType? _periodType;

        // todo: rewrite to work via subscription
        private void Update()
        {
            if (ClientSpawner.Instance == null)
                return;

            var currentPeriod = ClientSpawner.CurrentPeriodType;
            if (currentPeriod == _periodType)
                return;

            _periodType = currentPeriod;
            PaySalariesForPeriod(_periodType.Value);
        }

        // todo: ask Roman if this is by design. may be you should pay employees once per day???????? not multiple times per day
        private void PaySalariesForPeriod(CalendarDayPeriodType periodName)
        {
            var allStaff = HiringManager.Instance.AllStaff; // Получаем список всех сотрудников
            if (allStaff == null)
                return;

            var totalDebtAccrued = 0;
            foreach (var staff in allStaff)
            {
                // todo: fix nullRef here, don't store 
                if (staff == null)
                {
                    Debug.LogError($"All staff contains null elements!");
                    continue;
                }

                // pay only if person actually worked :D
                if (!staff.WorkingPeriods.Contains(periodName))
                    continue;
                
                staff.unpaidPeriods++;  // todo: this is not right thing to do
                
                // todo: error prone. accumulate debt only if can't pay rn!!!
                totalDebtAccrued += staff.salaryPerPeriod;
            }

            if (totalDebtAccrued > 0)
            {
                // todo: not implement
                Debug.LogError($"[Payroll] Начислен долг по зарплате за период '{periodName}': ${totalDebtAccrued}.");
            }
        }
        
        // todo: this probably should be managed by timeSystem
        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"PatrolManager exists in this scene");
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}