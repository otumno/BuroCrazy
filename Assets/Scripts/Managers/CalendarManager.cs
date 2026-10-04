using UnityEngine;
using System;

namespace Managers
{
    // Хранит номер текущего дня (он же сохраняется в сейв). Двигает его TimeManager при смене дня.
    public class CalendarManager : MonoBehaviour
    {
        public static CalendarManager Instance { get; private set; }

        public int CurrentDay { get; private set; } = 1;

        // Событие, если кому-то важно знать, что наступил новый день (например, для сброса ежедневных квестов)
        public event Action<int> OnNewDayStarted;

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("Multiple instances of Singleton! Check scene creation or something");
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
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