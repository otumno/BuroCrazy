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
        // Список бонусов к спавну клиентов в разные периоды дня.
        // WaveManager будет читать это через ProgressionManager.
        public List<PeriodBonus> spawnBonuses;

        // public List<ArchetypeData> allowedArchetypes; // Заготовка под будущие архетипы
    }

    [System.Serializable]
    public class PeriodBonus
    {
        [Tooltip("В какое время суток работает этот бонус (можно выбрать несколько через Ctrl)")]
        public CalendarDayPeriodType period;
        [Tooltip("Сколько дополнительных клиентов добавляется к базовой волне")]
        public int additionalClients;
    }
}