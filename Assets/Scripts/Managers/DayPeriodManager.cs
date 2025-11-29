using System;
using System.Collections.Generic;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class DayPeriodManager : MonoBehaviour
    {
        public static DayPeriodManager Instance { get; private set; }

        [Header("Настройки")]
        public CalendarDay mainCalendarDay;

        public CalendarDayPeriodType CurrentPeriodType { get; private set; }
        public PeriodSettings CurrentPeriodConfig { get; private set; }
        public PeriodSettings PreviousPeriodConfig { get; private set; }
        
        public float PeriodTimer { get; private set; }
        private int currentPeriodIndex = 0;

        public event Action OnPeriodChanged;
        public event Action OnDayFinished;

        void Awake() { if (Instance != null) Destroy(gameObject); else Instance = this; }

        void Start() { InitializeFirstPeriod(); }

        void Update()
        {
            if (Time.timeScale == 0f || CurrentPeriodConfig == null) return;
            PeriodTimer += Time.deltaTime;
            if (PeriodTimer >= CurrentPeriodConfig.durationInSeconds) SwitchToNextPeriod();
        }

        private void InitializeFirstPeriod()
        {
            if (mainCalendarDay == null || mainCalendarDay.periodSettings.Count == 0) return;
            var nightIndex = mainCalendarDay.periodSettings.FindIndex(p => p.PeriodType == CalendarDayPeriodType.Night);
            if (nightIndex == -1) nightIndex = mainCalendarDay.periodSettings.Count - 1;

            currentPeriodIndex = nightIndex;
            CurrentPeriodConfig = mainCalendarDay.periodSettings[nightIndex];
            PreviousPeriodConfig = CurrentPeriodConfig;
            CurrentPeriodType = CurrentPeriodConfig.PeriodType;
            PeriodTimer = Mathf.Max(0, CurrentPeriodConfig.durationInSeconds - 5f); // Почти конец ночи
            OnPeriodChanged?.Invoke();
        }

        private void SwitchToNextPeriod()
        {
            var periods = mainCalendarDay.periodSettings;
            if (periods == null || periods.Count == 0) return;

            PreviousPeriodConfig = CurrentPeriodConfig;
            currentPeriodIndex = (currentPeriodIndex + 1) % periods.Count;
            
            if (currentPeriodIndex == 0) CompleteDay();

            CurrentPeriodConfig = periods[currentPeriodIndex];
            CurrentPeriodType = CurrentPeriodConfig.PeriodType;
            PeriodTimer = 0f;

            Debug.Log($"<color=cyan>[DayPeriodManager]</color> Смена периода: {CurrentPeriodType}");
            OnPeriodChanged?.Invoke();
        }

        private void CompleteDay()
        {
            OnDayFinished?.Invoke();
            CalendarManager.Instance?.AdvanceDay();
        }

        // --- НОВЫЙ МЕТОД ДЛЯ DEBUG TIMESKIP ---
        public void SkipCurrentPeriod()
        {
            SwitchToNextPeriod();
        }
    }
}