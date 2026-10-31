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

        // Поля для отслеживания времени суток
        [SerializeField] private float dayStartTime = 0f;
        [SerializeField] private float totalSecondsInDay = 0f;

        // События
        public event Action<PeriodSettings> OnPeriodChanged;
        public event Action<int> OnDayChanged;

        /// <summary>
        /// Последний период дня закончился, но день ещё не сменился. Подписчик может задержать смену дня
        /// через <see cref="HoldDayEnd"/> (например, чтобы показать экраны итогов) и отпустить через <see cref="ReleaseDayEnd"/>.
        /// </summary>
        public event Action DayEnding;

        private PeriodSettings currentPeriodSettings;

        // Почему не PushPause: паузу жёстко сбрасывают HardResetPauseRoutine (кнопка «Начать день») и ручная пауза
        // на пробел в PlayerInputController. Свой счётчик держит смену дня независимо от timeScale.
        private int _dayEndHoldCount;
        private bool _isWaitingForDayEnd;

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
            if (Time.timeScale == 0f || currentPeriodSettings == null || _isWaitingForDayEnd) return;

            periodTimer += Time.deltaTime;

            if (periodTimer >= currentPeriodSettings.durationInSeconds)
            {
                RequestNextPeriod();
            }
        }

        /// <summary>
        /// Переход к следующему периоду с учётом удержания конца дня.
        /// Перед сменой дня рассылает <see cref="DayEnding"/>; если кто-то вызвал <see cref="HoldDayEnd"/>,
        /// время встаёт до <see cref="ReleaseDayEnd"/>.
        /// </summary>
        public void RequestNextPeriod()
        {
            var periods = mainCalendarDay.periodSettings;
            if (periods == null || periods.Count == 0 || _isWaitingForDayEnd) return;

            bool isDayEnding = (currentPeriodIndex + 1) % periods.Count == 0;
            if (!isDayEnding)
            {
                GoToNextPeriod();
                return;
            }

            OnDayEnding();
            if (_dayEndHoldCount > 0)
            {
                _isWaitingForDayEnd = true;
                return;
            }

            GoToNextPeriod();
        }

        public void HoldDayEnd() => _dayEndHoldCount++;

        public void ReleaseDayEnd()
        {
            if (_dayEndHoldCount == 0) return;

            _dayEndHoldCount--;
            if (_dayEndHoldCount > 0 || !_isWaitingForDayEnd) return;

            _isWaitingForDayEnd = false;
            GoToNextPeriod();
        }

        private void OnDayEnding() => DayEnding?.Invoke();

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
            dayStartTime = Time.time;

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
            bool isNewDay = (currentPeriodIndex == 0);
            if (isNewDay)
            {
                // Номер дня ведёт CalendarManager: он сохраняется в сейв. Свой счётчик TimeManager обнулялся
                // при каждой загрузке сцены, и день в сейве навсегда оставался первым.
                if (CalendarManager.Instance != null)
                {
                    CalendarManager.Instance.AdvanceDay();
                    dayCounter = CalendarManager.Instance.CurrentDay;
                }
                else
                {
                    dayCounter++;
                }
                dayStartTime = Time.time;
                OnDayChanged?.Invoke(dayCounter);
            }

            LogPeriodStart();
            OnPeriodChanged?.Invoke(currentPeriodSettings);
        }

        // Public API
        public int GetCurrentDay() => CalendarManager.Instance != null ? CalendarManager.Instance.CurrentDay : dayCounter;
        public CalendarDayPeriodType GetCurrentPeriodType() => currentPeriodType;
        public PeriodSettings GetCurrentPeriodSettings() => currentPeriodSettings;
        public float GetPeriodTimer() => periodTimer;
        public bool IsNight() => currentPeriodType.IsNight();

        /// <summary>
        /// Возвращает абсолютное время (Time.time) начала текущего дня
        /// </summary>
        public float GetCurrentDayStartTime() => dayStartTime;

        /// <summary>
        /// Возвращает количество секунд, прошедших с начала текущего дня
        /// </summary>
        public float GetCurrentTimeSinceDayStart() => Time.time - dayStartTime;

        /// <summary>
        /// Возвращает общую длительность дня в секундах
        /// </summary>
        public float GetTotalSecondsInDay()
        {
            if (mainCalendarDay == null || mainCalendarDay.periodSettings == null)
                return 0f;
            float total = 0f;
            foreach (var ps in mainCalendarDay.periodSettings)
                total += ps.durationInSeconds;
            return total;
        }

        private string GetPeriodDisplayName(CalendarDayPeriodType periodType)
        {
            return periodType switch
            {
                CalendarDayPeriodType.Morning => "УТРО",
                CalendarDayPeriodType.EarlyDay => "НАЧАЛО ДНЯ",
                CalendarDayPeriodType.Noon => "ОБЕД",
                CalendarDayPeriodType.Day => "ДЕНЬ",
                CalendarDayPeriodType.LateDay => "КОНЕЦ ДНЯ",
                CalendarDayPeriodType.Evening => "ВЕЧЕР",
                CalendarDayPeriodType.StartNight or CalendarDayPeriodType.EndNight => "НОЧЬ",
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
            DayEnding = null;
            Instance = null;
        }
    }
}