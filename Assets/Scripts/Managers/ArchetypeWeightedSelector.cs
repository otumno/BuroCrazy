using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Scriptables.Progression;
using Characters;
using Data;

namespace Managers
{
    /// <summary>
    /// Утилитарный класс для выбора архетипов с учётом весов регионов.
    /// Используется WaveManager для определения какой тип клиента спавнить.
    /// </summary>
    public static class ArchetypeWeightedSelector
    {
        /// <summary>
        /// Выбирает группу архетипов с учётом весов всех активных регионов.
        /// Возвращает groupID выбранной группы.
        /// </summary>
        public static string SelectGroupFromAllRegions(List<RegionRuntimeState> activeRegions)
        {
            if (activeRegions == null || activeRegions.Count == 0)
            {
                Debug.LogWarning("[ArchetypeWeightedSelector] Нет активных регионов!");
                return "Default";
            }

            // Собираем все веса групп из всех регионов
            var combinedWeights = new Dictionary<string, float>();
            float totalWeight = 0f;

            foreach (var regionState in activeRegions)
            {
                if (regionState.data == null || regionState.data.groupWeights == null)
                    continue;

                foreach (var weight in regionState.data.groupWeights)
                {
                    if (string.IsNullOrEmpty(weight.groupID))
                        continue;

                    // Учитываем текущий поток региона в весе
                    float regionMultiplier = regionState.currentDailyTarget;

                    if (combinedWeights.ContainsKey(weight.groupID))
                    {
                        combinedWeights[weight.groupID] += weight.weight * regionMultiplier;
                    }
                    else
                    {
                        combinedWeights[weight.groupID] = weight.weight * regionMultiplier;
                    }
                    totalWeight += weight.weight * regionMultiplier;
                }
            }

            if (combinedWeights.Count == 0)
            {
                Debug.LogWarning("[ArchetypeWeightedSelector] Нет весов групп в активных регионах!");
                return "Default";
            }

            // Weighted random selection
            float randomPoint = Random.Range(0, totalWeight);
            float currentWeight = 0f;

            foreach (var kvp in combinedWeights)
            {
                currentWeight += kvp.Value;
                if (randomPoint <= currentWeight)
                {
                    return kvp.Key;
                }
            }

            return combinedWeights.Keys.Last();
        }

        /// <summary>
        /// Выбирает конкретный архетип из базы по группе.
        /// </summary>
        public static ClientArchetype SelectArchetype(string groupID, ArchetypeDatabase database)
        {
            if (database == null)
            {
                Debug.LogWarning("[ArchetypeWeightedSelector] ArchetypeDatabase is null!");
                return null;
            }

            var archetype = database.GetRandomByGroup(groupID);
            if (archetype == null)
            {
                Debug.LogWarning($"[ArchetypeWeightedSelector] Не найден архетип для группы '{groupID}'!");
                return null;
            }

            return archetype;
        }

        /// <summary>
        /// Выбирает следующего клиента для спавна на основе текущего состояния игры.
        /// </summary>
        public static ClientArchetype SelectNextClient(
            List<RegionRuntimeState> activeRegions,
            ArchetypeDatabase database,
            Data.Calendar.CalendarDayPeriodType currentPeriod)
        {
            if (activeRegions == null || activeRegions.Count == 0)
            {
                Debug.LogWarning("[ArchetypeWeightedSelector] Нет активных регионов!");
                return null;
            }

            // 1. Выбираем группу с учётом весов
            string groupID = SelectGroupFromAllRegions(activeRegions);

            // 2. Получаем архетип из группы
            var archetype = SelectArchetype(groupID, database);
            if (archetype == null)
            {
                // Fallback: пробуем случайный архетип
                archetype = database.GetRandomArchetype();
            }

            // 3. Проверяем временные предпочтения
            if (archetype != null && !archetype.CanSpawnInPeriod(currentPeriod, 0.05f))
            {
                // Если архетип не хочет появляться в это время, пробуем другую группу
                // Пытаемся до 3 раз
                for (int i = 0; i < 3; i++)
                {
                    groupID = SelectGroupFromAllRegions(activeRegions);
                    archetype = SelectArchetype(groupID, database);

                    if (archetype != null && archetype.CanSpawnInPeriod(currentPeriod, 0.05f))
                    {
                        break;
                    }
                }

                // Если всё ещё не подходит, используем любой доступный
                if (archetype == null || !archetype.CanSpawnInPeriod(currentPeriod, 0.05f))
                {
                    archetype = database.GetRandomArchetype();
                }
            }

            return archetype;
        }

        /// <summary>
        /// Генерирует список архетипов для спавна на основе дневного плана.
        /// </summary>
        public static List<ClientArchetype> GenerateSpawnList(
            int totalClients,
            List<RegionRuntimeState> activeRegions,
            ArchetypeDatabase database)
        {
            var spawnList = new List<ClientArchetype>();

            for (int i = 0; i < totalClients; i++)
            {
                var archetype = SelectNextClient(activeRegions, database, Data.Calendar.CalendarDayPeriodType.Day);
                if (archetype != null)
                {
                    spawnList.Add(archetype);
                }
            }

            return spawnList;
        }

        /// <summary>
        /// Получает распределение клиентов по группам для отладки.
        /// </summary>
        public static Dictionary<string, int> GetGroupDistribution(int sampleSize, List<RegionRuntimeState> activeRegions)
        {
            var distribution = new Dictionary<string, int>();

            for (int i = 0; i < sampleSize; i++)
            {
                string groupID = SelectGroupFromAllRegions(activeRegions);

                if (distribution.ContainsKey(groupID))
                {
                    distribution[groupID]++;
                }
                else
                {
                    distribution[groupID] = 1;
                }
            }

            return distribution;
        }
    }
}
