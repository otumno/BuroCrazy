using Data.Calendar;
using UnityEngine;

namespace Managers
{
    // todo: this must not be monoBehaviour, just simple class inside some central OfficeSystem, which will aggregate time, schedule, calendar, etc.
    public class PayStaffManager : MonoBehaviour
    {
        public static PayStaffManager Instance { get; set; }

        private CalendarDayPeriodType? _periodType;

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

        private void Update()
		{
			// ИСПРАВЛЕНО: Смотрим в DayPeriodManager
			if (DayPeriodManager.Instance == null)
				return;

			var currentPeriod = DayPeriodManager.Instance.CurrentPeriodType;
			if (currentPeriod == _periodType)
				return;

			_periodType = currentPeriod;
			PaySalariesForPeriod(_periodType.Value);
		}

        private void PaySalariesForPeriod(CalendarDayPeriodType periodName)
        {
            if (HiringManager.Instance == null) return;

            var allStaff = HiringManager.Instance.AllStaff; 
            if (allStaff == null) return;

            var totalDebtAccrued = 0;
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
                    totalDebtAccrued += staff.salaryPerPeriod;
                }
            }

            if (totalDebtAccrued > 0)
            {
                Debug.Log($"[Payroll] Начислен долг по зарплате за период '{periodName}': ${totalDebtAccrued}.");
            }
        } 

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}