using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class LightManager : MonoBehaviour
    {
        [SerializeField]
        private SingleDaySystem singleDaySystem;

        private void Start()
        {
            singleDaySystem.OnPeriodChanged += OnPeriodChange;
        }

        private void OnDestroy()
        {
            singleDaySystem.OnPeriodChanged += OnPeriodChange;
        }

        private void OnPeriodChange(PeriodSettings periodSettings)
        {
            ToggleStaffLights(!periodSettings.PeriodType.IsNight());
        }
        
        private void ToggleStaffLights(bool enable)
        {
            var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
            foreach(var staff in allStaff)
            {
                if (staff is GuardMovement guard && guard.nightLight != null)
                    guard.nightLight.SetActive(enable);
                else if (staff is ServiceWorkerController worker && worker.nightLight != null)
                    worker.nightLight.SetActive(enable);
            }
        }
    }
}