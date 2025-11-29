using UnityEngine;
using System;

namespace Managers
{
    public class CalendarManager : MonoBehaviour
    {
        public static CalendarManager Instance { get; private set; }

        public int CurrentDay { get; private set; } = 1;

        // Событие, если кому-то важно знать, что наступил новый день (например, для сброса ежедневных квестов)
        public event ActionOnDayChange OnNewDayStarted;
        public delegate void ActionOnDayChange(int newDay);

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); } else { Instance = this; }
        }

        public void StartNewGame()
        {
            CurrentDay = 1;
            OnNewDayStarted?.Invoke(CurrentDay);
        }

        public void AdvanceDay()
        {
            CurrentDay++;
            Debug.Log($"<color=green>[CalendarManager]</color> День завершен. Начинается день {CurrentDay}.");
            OnNewDayStarted?.Invoke(CurrentDay);
        }

        // Для загрузки сохранений
        public void SetDay(int day)
        {
            CurrentDay = day;
        }
    }
}