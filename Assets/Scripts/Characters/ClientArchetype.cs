using System.Collections.Generic;
using UnityEngine;
using Enums;
using Data.Documents;

namespace Characters
{
    [CreateAssetMenu(fileName = "Archetype_New", menuName = "Bureau/Characters/Client Archetype")]
    public class ClientArchetype : ScriptableObject
    {
        [Header("Идентификация")]
        public string archetypeID;
        public string displayName;

        [Header("Поведенческие характеристики")]
        [Tooltip("Терпение клиента (в секундах)")]
        public float patience = 30f;

        [Tooltip("Сопротивляемость стрессу (0-1, где 1 = очень стрессоустойчивый)")]
        [Range(0f, 1f)]
        public float stressResistance = 0.5f;

        [Tooltip("Склонность к агрессии (0-1)")]
        [Range(0f, 1f)]
        public float aggressionTendency = 0.2f;

        [Tooltip("Вероятность уйти довольным при успешном обслуживании")]
        [Range(0f, 1f)]
        public float satisfactionChance = 0.8f;

        [Header("Доступные цели")]
        [Tooltip("Какие цели может иметь клиент этого архетипа")]
        public List<ClientGoal> allowedGoals;

        [Header("Мысли и реплики")]
        [Tooltip("Список мыслей, которые может показывать клиент")]
        public List<string> thoughtPool;

        [Tooltip("Реплики при неудаче")]
        public List<string> angryResponses;

        [Tooltip("Реплики при успехе")]
        public List<string> happyResponses;

        [Header("Визуальные ограничения")]
        [Tooltip("Доступные цвета одежды (RGB)")]
        public List<Color> allowedClothingColors;

        [Tooltip("Разрешенные типы одежды")]
        public List<OutfitType> allowedOutfitTypes;

        [Tooltip("Типичные фразы при разговоре")]
        public List<string> greetingLines;

        [Header("Бонусы/Штрафы")]
        [Tooltip("Множитель чаевых (0-2)")]
        [Range(0f, 2f)]
        public float tipMultiplier = 1f;

        [Tooltip("Множитель времени обслуживания")]
        [Range(0.5f, 2f)]
        public float serviceTimeMultiplier = 1f;

        [Header("Особые свойства")]
        [Tooltip("Может ли этот архетип быть бездомным")]
        public bool canBeHomeless = false;

        [Tooltip("Требует ли особого обращения (например, элита)")]
        public bool requiresSpecialTreatment = false;

        public string GetRandomThought()
        {
            if (thoughtPool == null || thoughtPool.Count == 0) return "...";
            return thoughtPool[Random.Range(0, thoughtPool.Count)];
        }

        public string GetRandomGreeting()
        {
            if (greetingLines == null || greetingLines.Count == 0) return "Здравствуйте.";
            return greetingLines[Random.Range(0, greetingLines.Count)];
        }

        public string GetAngryResponse()
        {
            if (angryResponses == null || angryResponses.Count == 0) return "Это безобразие!";
            return angryResponses[Random.Range(0, angryResponses.Count)];
        }

        public string GetHappyResponse()
        {
            if (happyResponses == null || happyResponses.Count == 0) return "Спасибо!";
            return happyResponses[Random.Range(0, happyResponses.Count)];
        }
    }

    public enum OutfitType
    {
        Casual,
        Formal,
        Workwear,
        Rags,
        Uniform,
        Suit
    }
}
