// Assets/Scripts/Scriptables/Progression/RegionData.cs
using UnityEngine;
using System.Collections.Generic;
using Data.Calendar; // Используем существующий namespace календаря

namespace Scriptables.Progression
{
    [CreateAssetMenu(fileName = "Region_New", menuName = "Bureau/Progression/Region Data")]
    public class RegionData : ScriptableObject
    {
        [Header("Идентификация")]
        public string regionID; // Уникальный ID (например, "SLUMS", "DOWNTOWN")
        public string displayName; // "Трущобы"
        [TextArea(2, 4)]
        public string description;

        [Header("Визуал")]
        public Sprite mapVisual; // Иконка на карте (например, домик или силуэт района)
        public Sprite mapBackground; // Фон при просмотре деталей региона

        [Header("Стоимость Открытия")]
        public int unlockCostInfluence = 100; // Новая валюта
        public int unlockCostMoney = 500;

        [Header("Влияние на Геймплей")]
        public List<PeriodBonus> spawnBonuses;

        [Header("Группы архетипов")]
        [Tooltip("Какие группы архетипов приходят из этого региона")]
        public List<string> archetypeGroups = new List<string>();

        [Tooltip("Вес каждой группы (сумма должна быть ~1 или 100)")]
        public List<ArchetypeGroupWeight> groupWeights;

        [Header("Поток клиентов")]
        [Tooltip("Максимальное количество клиентов в день (100% поток)")]
        [Range(1, 200)]
        public int maxDailyFlow = 26;

        [Tooltip("Разброс потока (+/- процент от maxDailyFlow)")]
        [Range(0f, 0.3f)]
        public float flowVariance = 0.1f; // 10%

        [Tooltip("Задержка выхода на полный поток (дней). 1й день = 25%, 2й = 50%, 3й = 75%, 4й = 100%")]
        [Range(1, 10)]
        public int rampUpDays = 4;

        [Header("Телефонные контакты")]
        [Tooltip("Список ID контактов PhoneContact, которые разблокируются при захвате этого региона")]
        public List<string> unlocksContactIDs = new List<string>();
    }

    [System.Serializable]
    public class PeriodBonus
    {
        [Tooltip("В какое время суток работает этот бонус (можно выбрать несколько через Ctrl)")]
        public CalendarDayPeriodType period;
        [Tooltip("Сколько дополнительных клиентов добавляется к базовой волне")]
        public int additionalClients;
    }

    [System.Serializable]
    public class ArchetypeGroupWeight
    {
        [Tooltip("ID группы архетипа (например 'Elderly', 'Business', 'Student')")]
        public string groupID;

        [Tooltip("Вес этой группы при выборе (чем выше, тем чаще появляется)")]
        [Range(0f, 100f)]
        public float weight = 0.25f;
    }
}