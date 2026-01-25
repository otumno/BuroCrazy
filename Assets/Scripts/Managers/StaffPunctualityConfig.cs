using System.Collections.Generic;
using UnityEngine;
using Characters;

namespace Managers
{
    [CreateAssetMenu(fileName = "StaffPunctualityConfig", menuName = "Bureau/Config/Punctuality Config")]
    public class StaffPunctualityConfig : ScriptableObject
    {
        [Header("Настройки опозданий")]
        [Tooltip("Глобальный максимум опоздания в секундах")]
        public float globalMaxLateness = 30f;

        [Tooltip("Базовый разброс времени опоздания (секунды)")]
        public float latenessVariance = 5f;

        [Tooltip("Штраф за опоздание к эффективности (0-1)")]
        [Range(0f, 0.5f)]
        public float latenessPenalty = 0.1f;

        [Tooltip("Бонус к эффективности за пунктуальность (0-0.2)")]
        [Range(0f, 0.2f)]
        public float punctualityBonus = 0.05f;

        [Header("Настройки перерывов")]
        [Tooltip("Длительность обеда в секундах")]
        public float lunchBreakDuration = 900f;

        [Tooltip("Время до обеда от начала смены (процент от смены)")]
        [Range(0.3f, 0.7f)]
        public float lunchBreakTimePercent = 0.5f;

        [Tooltip("Кулертайм - минимальное время между использованиями")]
        public float coolerCooldown = 300f;

        [Tooltip("Кулертайм - максимальная продолжительность")]
        public float coolerMaxDuration = 60f;

        [Header("Туалет")]
        [Tooltip("Туалет - минимальное время между использованиями")]
        public float toiletCooldown = 600f;

        [Tooltip("Туалет - максимальная продолжительность")]
        public float toiletMaxDuration = 120f;

        [Header("Вероятности для стажеров (нет обеда)")]
        [Tooltip("Шанс сходить к кулеру за смену")]
        [Range(0f, 1f)]
        public float internCoolerChance = 0.3f;

        [Tooltip("Шанс сходить в туалет за смену")]
        [Range(0f, 1f)]
        public float internToiletChance = 0.2f;

        [Header("Вероятности для обычных работников")]
        [Tooltip("Шанс дополнительного кулертайма (сверх обеда)")]
        [Range(0f, 1f)]
        public float staffCoolerChance = 0.5f;

        [Tooltip("Шанс дополнительного туалета")]
        [Range(0f, 1f)]
        public float staffToiletChance = 0.4f;
    }
}
