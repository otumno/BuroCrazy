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
            TimeManager.Instance.DayEnding += TimeManager_DayEnding;
        }

        // Зарплата списывается в конце дня, до экранов итогов: в итогах она видна как расход этого дня.
        // Не на OnDayChanged: он приходит уже после экранов, и списание попало бы в расходы следующего дня.
        private void TimeManager_DayEnding()
        {
            PayAccumulatedSalaries();
        }

        private void PayAccumulatedSalaries()
        {
            if (HiringManager.Instance == null || PlayerWallet.Instance == null)
                return;

            var total = 0;
            foreach (var staff in HiringManager.Instance.AllStaff)
            {
                if (staff == null || staff.unpaidPeriods <= 0)
                    continue;

                total += staff.unpaidPeriods * staff.salaryPerPeriod;
                staff.unpaidPeriods = 0;
            }

            if (total <= 0)
                return;

            // Денег может не хватить — тогда счёт уходит в минус: банкротства в игре пока нет.
            PlayerWallet.Instance.AddMoney(-total, UI.GameText.Get("ledger.staff_wages"));
            Debug.Log($"[Payroll] Выплачена зарплата за день: ${total}. Баланс: ${PlayerWallet.Instance.GetCurrentMoney()}.");
        }

        private void OnPeriodChanged(PeriodSettings obj)
        {
            PaySalariesForPeriod(obj.PeriodType);

            if (obj.PeriodType.IsEndDay())
                DismissTemporaryStaff();
        }

        private void DismissTemporaryStaff()
        {
            if (HiringManager.Instance == null)
                return;

            foreach (var staff in HiringManager.Instance.GetTemporaryStaff())
            {
                // Never dismiss a contract whose configured work day has not ended.
                if (!staff.IsTemporaryWorkDayOver())
                    continue;

                // Смена оплачена вперёд при найме (HiringManager.GetHiringCost), доплачивать нечего.
                Debug.Log($"[Payroll] Временный сотрудник {staff.characterName} отработал смену и уволен.");
                HiringManager.Instance.FireStaff(staff);
            }
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
                    Debug.LogWarning($"[PayStaffManager] Found null staff entry in AllStaff, cleaning up...");
                    continue;
                }

                if (AccruePeriod(staff, TimeManager.Instance.GetCurrentDay(), periodName))
                    totalDebtAcquired += staff.salaryPerPeriod;
            }

            if (totalDebtAcquired > 0)
            {
                Debug.Log($"[Payroll] Начислен долг по зарплате за период '{periodName}': ${totalDebtAcquired}.");
            }
        }

        public static void AccrueCurrentPeriod(StaffController staff)
        {
            var clock = TimeManager.Instance;
            if (clock != null) AccruePeriod(staff, clock.GetCurrentDay(), clock.GetCurrentPeriodType());
        }

        private static bool AccruePeriod(StaffController staff, int day, CalendarDayPeriodType period)
        {
            if (staff.IsTemporary || !staff.HasEmploymentStarted() || (staff.WorkShiftMask & period) == 0 ||
                (staff.lastSalaryDay == day && staff.lastSalaryPeriod == period)) return false;
            staff.lastSalaryDay = day;
            staff.lastSalaryPeriod = period;
            staff.unpaidPeriods++;
            return true;
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance)
            {
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
                TimeManager.Instance.DayEnding -= TimeManager_DayEnding;
            }
            
            Instance = null;
        }
    }
}
