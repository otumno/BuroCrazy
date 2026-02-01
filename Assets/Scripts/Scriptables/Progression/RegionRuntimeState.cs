using UnityEngine;
using System;

namespace Scriptables.Progression
{
    /// <summary>
    /// Runtime состояние региона - отслеживает текущий прогресс региона у игрока.
    /// Создаётся ProgressionManager при старте игры и обновляется при захвате регионов.
    /// </summary>
    [Serializable]
    public class RegionRuntimeState
    {
        [Header("Ссылка на данные")]
        public RegionData data;

        [Header("Состояние владения")]
        public bool isUnlocked;
        public int daysOwned; // Сколько дней регион у игрока

        [Header("Текущий поток")]
        public int currentDailyTarget; // Сколько клиентов регион пришлёт сегодня
        public int previousDayFlow; // Сколько пришло вчера (для статистики)

        [Header("Временные метки")]
        public float unlockTime; // Время когда был захвачен
        public int unlockDay; // День игры когда был захвачен

        public RegionRuntimeState()
        {
            isUnlocked = false;
            daysOwned = 0;
            currentDailyTarget = 0;
            previousDayFlow = 0;
            unlockTime = 0f;
            unlockDay = 0;
        }

        /// <summary>
        /// Создаёт состояние для разблокированного региона
        /// </summary>
        public static RegionRuntimeState CreateUnlocked(RegionData regionData, int currentGameDay)
        {
            var state = new RegionRuntimeState
            {
                data = regionData,
                isUnlocked = true,
                daysOwned = 1, // Первый день - 25%
                currentDailyTarget = CalculateDailyTarget(regionData, 1),
                unlockTime = Time.time,
                unlockDay = currentGameDay
            };

            Debug.Log($"[RegionRuntimeState] Created unlocked state for '{regionData.displayName}': " +
                      $"target={state.currentDailyTarget} (day {state.daysOwned}/{regionData.rampUpDays})");

            return state;
        }

        /// <summary>
        /// Обновляет состояние при смене дня
        /// </summary>
        public void OnNewDay(int totalDaysOwned)
        {
            if (!isUnlocked) return;

            daysOwned = totalDaysOwned;
            int newTarget = CalculateDailyTarget(data, daysOwned);

            Debug.Log($"[RegionRuntimeState] '{data?.displayName ?? "Unknown"}' day update: " +
                      $"{currentDailyTarget} -> {newTarget} (day {daysOwned})");

            previousDayFlow = currentDailyTarget;
            currentDailyTarget = newTarget;
        }

        /// <summary>
        /// Рассчитывает дневной лимит в зависимости от дня владения
        /// </summary>
        public static int CalculateDailyTarget(RegionData data, int daysOwned)
        {
            if (data == null) return 0;

            // rampUpDays = 4: 1=25%, 2=50%, 3=75%, 4+=100%
            float percentage = Mathf.Min(daysOwned, data.rampUpDays) / (float)data.rampUpDays;

            // Базовое значение
            int baseTarget = Mathf.RoundToInt(data.maxDailyFlow * percentage);

            // Применяем variance (разброс +/-)
            float variance = UnityEngine.Random.Range(-data.flowVariance, data.flowVariance);
            int finalTarget = Mathf.RoundToInt(baseTarget * (1f + variance));

            // Гарантируем минимум 1 клиента если регион активен
            return Mathf.Max(1, finalTarget);
        }

        /// <summary>
        /// Получает процент от максимального потока (0.0 - 1.0)
        /// </summary>
        public float GetFlowPercentage()
        {
            if (data == null || data.maxDailyFlow <= 0) return 0f;
            return (float)currentDailyTarget / data.maxDailyFlow;
        }

        /// <summary>
        /// Проверяет достиг ли регион полного потока
        /// </summary>
        public bool IsAtFullCapacity()
        {
            return isUnlocked && daysOwned >= data.rampUpDays;
        }

        /// <summary>
        /// Получает строковое представление прогресса региона
        /// </summary>
        public string GetProgressDescription()
        {
            if (!isUnlocked) return "<color=red>Закрыт</color>";

            float pct = GetFlowPercentage() * 100f;
            int target = currentDailyTarget;

            if (IsAtFullCapacity())
            {
                return $"<color=green>Полный поток: {target} чел/день</color>";
            }
            else
            {
                return $"<color=yellow>Прогрев: {pct:F0}% ({target} чел/день)</color>";
            }
        }
    }
}
