using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class SingleDaySystem : MonoBehaviour
    {
        public static bool isPaused => Time.timeScale == 0f;
        
        public event Action<PeriodSettings> OnPeriodChanged;
        public event Action OnDayFinished;
        
        // init
        private List<PeriodSettings> _periods;

        // runtime
        private int _periodIndex;
        private float _periodTime;

        public bool isRunning => _periods is {Count: > 0} && _periodIndex < _periods.Count;

        public IReadOnlyList<PeriodSettings> periods => _periods;

        public float periodDuration => isRunning 
                                       ? _periods[_periodIndex].durationInSeconds
                                       : float.MaxValue;
        public CalendarDayPeriodType periodType => isRunning
                                                   ? _periods[_periodIndex].PeriodType
                                                   : CalendarDayPeriodType.None;

        public void Initialize(List<PeriodSettings> periods)
        {
            _periods = new List<PeriodSettings>(periods);
            _periods = _periods.OrderBy(t => t.PeriodType).ToList();

            if (_periods.Count == 0)
            {
                Debug.LogError("Periods are empty!");
                return;
            }

            // calendarDay must have night
            var nightIndex = _periods.FindIndex(t => t.PeriodType == CalendarDayPeriodType.Night);
            if (nightIndex == -1)
            {
                Debug.LogError($"No Night found");
                throw new InvalidDataException("Missing Night config in CalendarDay!");
            }

            // calendarDay must start from night, so move it in the start of the list
            var nightPeriodSettings = _periods[nightIndex];
            _periods.RemoveAt(nightIndex);
            _periods.Insert(0, nightPeriodSettings);
            
            // start
            _periodTime = 0f;
            _periodIndex = 0;
            
            // throw event
            OnPeriodChanged?.Invoke(_periods[0]);
        }

        public void Update()
        {
            if (_periods == null || isPaused)
                return;

            _periodTime += Time.deltaTime;
            
            if (_periodTime < periodDuration)
                return;

            StartNewPeriod();
        }

        private void StartNewPeriod()
        {
            _periodIndex++;
            _periodTime = 0f;

            if (_periodIndex >= _periods.Count)
                FinishDay();
            else
                OnPeriodChanged?.Invoke(_periods[_periodIndex]);
        }

        private void FinishDay()
        {
            _periodIndex = 0;
            _periodTime = 0f;
            _periods.Clear();
            OnDayFinished?.Invoke();
        }

#if DEBUG_ENABLED
        public void ForceFinishPeriod() => StartNewPeriod();
        
        public void ForceFinishDay() => FinishDay();
#endif

        private void OnDestroy()
        {
            OnPeriodChanged = null;
            OnDayFinished = null;
        }
    }
}