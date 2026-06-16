using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Characters;
using Data.Calendar;

namespace Managers
{
    /// <summary>
    /// Класс для распределения клиентов по периодам дня с учётом предпочтений архетипов.
    /// Используется WaveManager для создания дневного плана спавна.
    /// </summary>
    public static class TimeDistributionCalculator
    {
        /// <summary>
        /// Структура для хранения распределения по периодам
        /// </summary>
        public struct PeriodDistribution
        {
            public CalendarDayPeriodType period;
            public int clientCount;
            public List<ClientArchetype> archetypes;
            public float totalPreferenceScore;
        }

        /// <summary>
        /// Распределяет общее количество клиентов по периодам дня с учётом предпочтений.
        /// </summary>
        /// <param name="totalDailyFlow">Общее количество клиентов в день</param>
        /// <param name="availableArchetypes">Доступные архетипы</param>
        /// <param name="periods">Периоды дня для распределения</param>
        /// <returns>Словарь: период -> распределение</returns>
        public static Dictionary<CalendarDayPeriodType, PeriodDistribution> CalculateDistribution(
            int totalDailyFlow,
            List<ClientArchetype> availableArchetypes,
            CalendarDayPeriodType[] periods)
        {
            var result = new Dictionary<CalendarDayPeriodType, PeriodDistribution>();

            if (totalDailyFlow <= 0)
            {
                Debug.LogWarning("[TimeDistributionCalculator] totalDailyFlow <= 0!");
                return result;
            }

            // 1. Равномерное распределение по дневным периодам (временно отключаем учёт timeOfDayPreference)
            var periodScores = new Dictionary<CalendarDayPeriodType, float>();
            float totalScore = 0f;
            int activePeriodsCount = 0;

            foreach (var period in periods)
            {
                if (period.IsNight())
                {
                    periodScores[period] = 0f;
                    continue;
                }

                periodScores[period] = 1f;
                totalScore += 1f;
                activePeriodsCount++;
            }

            // 2. Распределяем клиентов пропорционально score
            foreach (var period in periods)
            {
                float score = periodScores[period];
                float normalizedScore = totalScore > 0 ? score / totalScore : 0f;

                int clientCount = Mathf.RoundToInt(totalDailyFlow * normalizedScore);
                clientCount = Mathf.Max(0, clientCount); // Гарантируем неотрицательность

                result[period] = new PeriodDistribution
                {
                    period = period,
                    clientCount = clientCount,
                    archetypes = new List<ClientArchetype>(),
                    totalPreferenceScore = score
                };
            }

            // 3. Распределяем конкретных клиентов по периодам
            AssignArchetypesToPeriods(result, availableArchetypes, totalDailyFlow);

            Debug.Log($"[TimeDistributionCalculator] Распределение на {totalDailyFlow} клиентов:");
            foreach (var kvp in result)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value.clientCount} клиентов");
            }

            return result;
        }

        /// <summary>
        /// Распределяет конкретные архетипы по периодам с учётом их предпочтений.
        /// </summary>
        private static void AssignArchetypesToPeriods(
            Dictionary<CalendarDayPeriodType, PeriodDistribution> distribution,
            List<ClientArchetype> availableArchetypes,
            int totalClients)
        {
            if (availableArchetypes == null || availableArchetypes.Count == 0)
                return;

            int assigned = 0;

            // Для каждого периода
            foreach (var kvp in distribution)
            {
                var period = kvp.Key;
                var dist = kvp.Value;

                if (dist.clientCount <= 0 || period.IsNight())
                    continue;

                // Равномерный выбор архетипа (без учёта timeOfDayPreference — он временно отключён)
                for (int i = 0; i < dist.clientCount; i++)
                {
                    if (availableArchetypes == null || availableArchetypes.Count == 0) break;

                    var archetype = availableArchetypes[Random.Range(0, availableArchetypes.Count)];
                    if (archetype != null)
                    {
                        distribution[period].archetypes.Add(archetype);
                        assigned++;
                    }
                }
            }

            // Если что-то не распределилось (все периоды ночные?), добавляем в "Day"
            if (assigned < totalClients && distribution.ContainsKey(CalendarDayPeriodType.Day))
            {
                for (int i = assigned; i < totalClients; i++)
                {
                    var randomArchetype = availableArchetypes[Random.Range(0, availableArchetypes.Count)];
                    distribution[CalendarDayPeriodType.Day].archetypes.Add(randomArchetype);
                }
            }
        }

        /// <summary>
        /// Вспомогательная структура для взвешенного выбора
        /// </summary>
        private struct WeightedArchetype
        {
            public ClientArchetype archetype;
            public float weight;
        }

        /// <summary>
        /// Выбирает элемент с учётом веса
        /// </summary>
        private static WeightedArchetype SelectWeighted(List<WeightedArchetype> items)
        {
            float totalWeight = items.Sum(x => x.weight);
            float randomPoint = Random.Range(0, totalWeight);

            float currentWeight = 0f;
            foreach (var item in items)
            {
                currentWeight += item.weight;
                if (randomPoint <= currentWeight)
                {
                    return item;
                }
            }

            return items.Last();
        }

        /// <summary>
        /// Получает дефолтный score предпочтения для периода (если архетипы не заданы).
        /// </summary>
        private static float GetDefaultPreferenceScore(CalendarDayPeriodType period)
        {
            // Дефолтные предпочтения: утро и день наиболее активны
            if ((period & CalendarDayPeriodType.Morning) != 0) return 1.0f;
            if ((period & CalendarDayPeriodType.EarlyDay) != 0) return 0.9f;
            if ((period & CalendarDayPeriodType.Noon) != 0) return 0.8f;
            if ((period & CalendarDayPeriodType.Day) != 0) return 0.7f;
            if ((period & CalendarDayPeriodType.LateDay) != 0) return 0.5f;
            if ((period & CalendarDayPeriodType.Evening) != 0) return 0.3f;

            return 0f; // Ночь
        }

        /// <summary>
        /// Получает все периоды дня (без ночи) из конфигурации.
        /// </summary>
        public static CalendarDayPeriodType[] GetActivePeriods()
        {
            return new CalendarDayPeriodType[]
            {
                CalendarDayPeriodType.Morning,
                CalendarDayPeriodType.EarlyDay,
                CalendarDayPeriodType.Noon,
                CalendarDayPeriodType.Day,
                CalendarDayPeriodType.LateDay,
                CalendarDayPeriodType.Evening
            };
        }

        /// <summary>
        /// Проверяет что распределение корректно (все клиенты распределены).
        /// </summary>
        public static bool ValidateDistribution(Dictionary<CalendarDayPeriodType, PeriodDistribution> distribution, int expectedTotal)
        {
            int actualTotal = distribution.Values.Sum(d => d.archetypes.Count);
            return actualTotal == expectedTotal;
        }
    }
}
