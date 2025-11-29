using System.Collections.Generic;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class CalendarSystem : MonoBehaviour
    {
        [SerializeField]
        private List<CalendarDay> _days = new();
        
        [SerializeField]
        private CalendarDay _dayTemplate;

        [SerializeField]
        private SpecialEventConfig _specialEventConfig;

        [SerializeField]
        private SingleDaySystem _daySystem;
        
        // runtime
        private int _dayIndex;

        public void Initialize(int dayIndex)
        {
            _dayIndex = dayIndex;
        }

        public void StartDay()
        {
            var defaultPeriods = _dayIndex < _days.Count ? _days[_dayIndex].periodSettings : _dayTemplate.periodSettings;
            var specialEventPeriods = _specialEventConfig?.GetSpecialEvent().Settings;
            var periods = specialEventPeriods ?? defaultPeriods;
        
            _daySystem.Initialize(periods);
        }

        private void OnDayFinished()
        {
            _dayIndex++;
            StartDay();
        }
        
        private void Start()
        {
            _daySystem.OnDayFinished += OnDayFinished;
        }

        private void OnDestroy()
        {
            _daySystem.OnDayFinished -= OnDayFinished;
        }
    }
}