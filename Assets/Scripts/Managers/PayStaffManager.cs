using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class PayStaffManager : MonoBehaviour
    {
        public static PayStaffManager Instance { get; set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError($"PayStaffManager (Instance) already exists in this scene. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
        }

        private void Start()
        {
            TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
        }

        private void OnPeriodChanged(PeriodSettings obj)
        {
            PaySalariesForPeriod(obj.PeriodType);
        }

        private void PaySalariesForPeriod(CalendarDayPeriodType periodName)
        {
            if (HiringManager.Instance == null)
                return;

            var allStaff = HiringManager.Instance.AllStaff; 
            if (allStaff == null)
                return;

            var totalDebtAcquired = 0;
            foreach (var staff in allStaff)
            {
                if (staff == null)
                {
                    Debug.LogError($"All staff contains null elements!");
                    continue;
                }

                if (staff.WorkShiftMask.HasFlag(periodName)) 
                {
                    staff.unpaidPeriods++;  
                    totalDebtAcquired += staff.salaryPerPeriod;
                }
            }

            if (totalDebtAcquired > 0)
            {
                Debug.Log($"[Payroll] Начислен долг по зарплате за период '{periodName}': ${totalDebtAcquired}.");
            }
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance)
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
            
            Instance = null;
        }
    }
}