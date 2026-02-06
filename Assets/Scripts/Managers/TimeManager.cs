using System;
using System.Collections;
using System.Collections.Generic;
using Data.Calendar;
using Managers.Teletype;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// Главный хронометр игры.
    /// Управляет сменой периодов, дней и рассылает события.
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        [Header("Настройки Календаря")]
        public CalendarDay mainCalendarDay;

        [Header("Отладка (Read Only)")]
        [SerializeField] private int dayCounter = 0;
        [SerializeField] private int currentPeriodIndex = 0;
        [SerializeField] private float periodTimer = 0f;
        [SerializeField] private CalendarDayPeriodType currentPeriodType;

        // События
        public event Action<PeriodSettings> OnPeriodChanged;
        public event Action<int> OnDayChanged;

        private PeriodSettings currentPeriodSettings;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            InitializeTime();
        }

        private void Update()
        {
            if (Time.timeScale == 0f || currentPeriodSettings == null) return;

            periodTimer += Time.deltaTime;

            if (periodTimer >= currentPeriodSettings.durationInSeconds)
            {
                GoToNextPeriod();
            }
        }

        private void InitializeTime()
        {
            if (mainCalendarDay == null || mainCalendarDay.periodSettings.Count == 0)
            {
                Debug.LogError("[TimeManager] Календарь пуст!");
                return;
            }

            dayCounter = 0;

            // --- ОБНОВЛЕНИЕ ПОД НОВЫЙ ENUM ---
            // Ищем конец ночи (EndNight), чтобы игра началась "за секунду до утра"
            int startPeriodIndex = mainCalendarDay.periodSettings.FindIndex(p => p.PeriodType == CalendarDayPeriodType.EndNight);
            
            // Если EndNight нет, ищем хотя бы StartNight
            if (startPeriodIndex == -1)
                startPeriodIndex = mainCalendarDay.periodSettings.FindIndex(p => p.PeriodType == CalendarDayPeriodType.StartNight);
            
            // Если и этого нет, берем последний период в списке
            if (startPeriodIndex == -1)
                startPeriodIndex = mainCalendarDay.periodSettings.Count - 1;

            currentPeriodIndex = startPeriodIndex;
            currentPeriodSettings = mainCalendarDay.periodSettings[currentPeriodIndex];
            currentPeriodType = currentPeriodSettings.PeriodType;
            
            // Ставим таймер так, чтобы до конца периода оставалось 10 секунд
            periodTimer = Mathf.Max(0, currentPeriodSettings.durationInSeconds - 10f);

            LogPeriodStart();
            OnPeriodChanged?.Invoke(currentPeriodSettings);
        }

        public void GoToNextPeriod()
        {
            var periods = mainCalendarDay.periodSettings;
            if (periods == null || periods.Count == 0) return;

            currentPeriodIndex = (currentPeriodIndex + 1) % periods.Count;
            periodTimer = 0f;

            currentPeriodSettings = periods[currentPeriodIndex];
            currentPeriodType = currentPeriodSettings.PeriodType;

            // Если вернулись к началу списка — новый день
            if (currentPeriodIndex == 0)
            {
                dayCounter++;
                OnDayChanged?.Invoke(dayCounter);
            }

            LogPeriodStart();
            OnPeriodChanged?.Invoke(currentPeriodSettings);
        }

        // Public API
        public int GetCurrentDay() => dayCounter;
        public CalendarDayPeriodType GetCurrentPeriodType() => currentPeriodType;
        public PeriodSettings GetCurrentPeriodSettings() => currentPeriodSettings;
        public float GetPeriodTimer() => periodTimer;
        public bool IsNight() => currentPeriodType.IsNight();

        private string GetPeriodDisplayName(CalendarDayPeriodType periodType)
        {
            return periodType switch
            {
                CalendarDayPeriodType.Morning => "УТРО",
                CalendarDayPeriodType.Day => "ДЕНЬ",
                CalendarDayPeriodType.Evening => "ВЕЧЕР",
                CalendarDayPeriodType.StartNight => "НОЧЬ",
                CalendarDayPeriodType.EndNight => "НОЧЬ",
                _ => periodType.ToString().ToUpper()
            };
        }

        private void LogPeriodStart()
        {
            if (TeletypeManager.Instance == null) return;
            string periodName = GetPeriodDisplayName(currentPeriodType);
            TeletypeManager.Instance.Log(periodName, false, Managers.Teletype.TeletypeMessageType.Info);
        }
        
        public PeriodSettings GetPreviousPeriodSettings()
        {
            if (mainCalendarDay == null || mainCalendarDay.periodSettings.Count == 0)
                return null;
            
            var prevIndex = currentPeriodIndex - 1;
            if (prevIndex < 0)
                prevIndex = mainCalendarDay.periodSettings.Count - 1;
            
            return mainCalendarDay.periodSettings[prevIndex];
        }

        private void OnDestroy()
        {
            OnDayChanged = null;
            OnPeriodChanged = null;
            Instance = null;
        }
    }
}