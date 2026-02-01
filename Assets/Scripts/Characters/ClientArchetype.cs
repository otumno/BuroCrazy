using System.Collections.Generic;
using UnityEngine;
using Enums;
using Data.Calendar;
using Data.Documents;

namespace Characters
{
    [CreateAssetMenu(fileName = "Archetype_New", menuName = "Bureau/Characters/Client Archetype")]
    public class ClientArchetype : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Группа для спавна: 'Elderly', 'Business', 'Student' и т.д.")]
        public string groupID = "Default";

        [Tooltip("Уникальный ID этого архетипа")]
        public string archetypeID;

        [Tooltip("Отображаемое имя в UI")]
        public string displayName;

        [Header("Визуал - Тело")]
        [Tooltip("Основной спрайт тела (Idle/Walk анимация)")]
        public Sprite bodySprite;

        [Tooltip("Спрайт лица для диалогов")]
        public Sprite portraitSprite;

        [Header("Визуал - Волосы")]
        [Tooltip("Список вариантов причесок (выбирается случайно)")]
        public List<Sprite> hairSprites = new List<Sprite>();

        [Tooltip("Цвета волос для рандомизации")]
        public List<Color> hairColors = new List<Color>
        {
            new Color(0.2f, 0.1f, 0f),    // Чёрный
            new Color(0.4f, 0.25f, 0.1f), // Тёмно-коричневый
            new Color(0.6f, 0.4f, 0.2f),  // Коричневый
            new Color(0.8f, 0.7f, 0.5f),  // Светлый
            new Color(0.9f, 0.9f, 0.85f), // Белый/седой
            new Color(0.5f, 0.3f, 0.2f),  // Рыжий
        };

        [Header("Визуал - Одежда")]
        [Tooltip("Список вариантов одежды (выбирается случайно)")]
        public List<Sprite> outfitSprites = new List<Sprite>();

        [Tooltip("Цвета одежды для рандомизации")]
        public List<Color> outfitColors = new List<Color>
        {
            new Color(0.3f, 0.3f, 0.4f),  // Тёмно-синий
            new Color(0.5f, 0.5f, 0.5f),  // Серый
            new Color(0.6f, 0.5f, 0.4f),  // Коричневый
            new Color(0.2f, 0.3f, 0.2f),  // Тёмно-зелёный
            new Color(0.9f, 0.9f, 0.9f),  // Белый
        };

        [Header("Поведение")]
        [Tooltip("Терпение клиента (в секундах)")]
        public float patience = 30f;

        [Tooltip("Множитель скорости: 1 = норма, 0.5 = медленно, 1.5 = быстро")]
        [Range(0.3f, 2f)]
        public float speedMultiplier = 1f;

        [Tooltip("Вероятность уйти довольным (0-1)")]
        [Range(0f, 1f)]
        public float satisfactionChance = 0.8f;

        [Tooltip("При каком % терпения начинает ворчать")]
        [Range(0.3f, 0.8f)]
        public float grumblingThreshold = 0.5f;

        [Tooltip("Частота ворчания (0-1)")]
        [Range(0f, 1f)]
        public float grumblingFrequency = 0.5f;

        [Header("Цели")]
        [Tooltip("Какие цели может иметь этот архетип")]
        public List<ClientGoal> allowedGoals;

        [Header("Мысли и реплики")]
        [Tooltip("Мысли при появлении")]
        public List<string> thoughtPool;

        [Tooltip("Реплики при ворчании")]
        public List<string> grumblingLines;

        [Tooltip("Реплики при успешном обслуживании")]
        public List<string> happyResponses;

        [Tooltip("Реплики при уходе расстроенным")]
        public List<string> angryResponses;

        [Header("Временные предпочтения спавна")]
        [Tooltip("Кривая предпочтений по времени суток (0-1). 1 = максимальная вероятность появления.")]
        public AnimationCurve timeOfDayPreference = new AnimationCurve(
            new Keyframe(0f, 0f),    // StartNight
            new Keyframe(0.2f, 0.3f), // Evening/LateDay
            new Keyframe(0.4f, 0.6f), // Day
            new Keyframe(0.6f, 0.8f), // Noon
            new Keyframe(0.8f, 1.0f), // EarlyDay
            new Keyframe(0.9f, 1.0f), // Morning - ПИК для пожилых!
            new Keyframe(1f, 0f)      // EndNight
        );

        /// <summary>
        /// Проверяет, насколько архетип предпочитает текущий период для спавна (0-1)
        /// </summary>
        public float GetTimePreferenceScore(CalendarDayPeriodType currentPeriod)
        {
            float normalizedTime = GetNormalizedTimeFromPeriod(currentPeriod);
            return timeOfDayPreference.Evaluate(normalizedTime);
        }

        /// <summary>
        /// Конвертирует период дня в нормализованное время (0-1) для кривой
        /// </summary>
        private float GetNormalizedTimeFromPeriod(CalendarDayPeriodType period)
        {
            if ((period & CalendarDayPeriodType.StartNight) != 0) return 0f;
            if ((period & CalendarDayPeriodType.EndNight) != 0) return 1f;
            if ((period & CalendarDayPeriodType.Evening) != 0) return 0.1f;
            if ((period & CalendarDayPeriodType.LateDay) != 0) return 0.25f;
            if ((period & CalendarDayPeriodType.Day) != 0) return 0.4f;
            if ((period & CalendarDayPeriodType.Noon) != 0) return 0.6f;
            if ((period & CalendarDayPeriodType.EarlyDay) != 0) return 0.8f;
            if ((period & CalendarDayPeriodType.Morning) != 0) return 0.95f;
            return 0.5f; // Default
        }

        /// <summary>
        /// Проверяет, может ли архетип появиться в текущий период (учитывая мин. порог)
        /// </summary>
        public bool CanSpawnInPeriod(CalendarDayPeriodType currentPeriod, float minThreshold = 0.1f)
        {
            if (currentPeriod.IsNight()) return false;
            return GetTimePreferenceScore(currentPeriod) >= minThreshold;
        }

        // Геттеры для визуалов
        public Sprite GetRandomHairSprite()
        {
            if (hairSprites == null || hairSprites.Count == 0) return null;
            return hairSprites[Random.Range(0, hairSprites.Count)];
        }

        public Sprite GetRandomOutfitSprite()
        {
            if (outfitSprites == null || outfitSprites.Count == 0) return null;
            return outfitSprites[Random.Range(0, outfitSprites.Count)];
        }

        public Color GetRandomHairColor()
        {
            if (hairColors == null || hairColors.Count == 0) return Color.black;
            return hairColors[Random.Range(0, hairColors.Count)];
        }

        public Color GetRandomOutfitColor()
        {
            if (outfitColors == null || outfitColors.Count == 0) return Color.white;
            return outfitColors[Random.Range(0, outfitColors.Count)];
        }

        public string GetRandomThought()
        {
            if (thoughtPool == null || thoughtPool.Count == 0) return "...";
            return thoughtPool[Random.Range(0, thoughtPool.Count)];
        }

        public string GetGrumblingLine()
        {
            if (grumblingLines == null || grumblingLines.Count == 0) return "Это несносно...";
            return grumblingLines[Random.Range(0, grumblingLines.Count)];
        }

        public string GetHappyResponse()
        {
            if (happyResponses == null || happyResponses.Count == 0) return "Спасибо!";
            return happyResponses[Random.Range(0, happyResponses.Count)];
        }

        public string GetAngryResponse()
        {
            if (angryResponses == null || angryResponses.Count == 0) return "Это безобразие!";
            return angryResponses[Random.Range(0, angryResponses.Count)];
        }
    }
}
