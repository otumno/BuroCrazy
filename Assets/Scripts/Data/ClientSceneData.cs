using System.Collections.Generic;
using UnityEngine;
using Enums;
using Characters;

namespace Data
{
    [System.Serializable]
    public class SceneLine
    {
        [Tooltip("Архетип говорящего (пусто = любой)")]
        public string speakerArchetypeID;

        [Tooltip("Пол говорящего: -1 = любой, 0 = женский, 1 = мужской")]
        public int requiredGender = -1;

        [TextArea(2, 4)]
        public string text;
    }

    [CreateAssetMenu(fileName = "ClientScene", menuName = "Bureau/Client Scene")]
    public class ClientSceneData : ScriptableObject
    {
        [Header("=== ИДЕНТИФИКАЦИЯ ===")]
        public string sceneID;
        public ClientSceneType sceneType = ClientSceneType.None;
        public string sceneName;

        [Header("=== УСЛОВИЯ АКТИВАЦИИ ===")]
        [Tooltip("Минимум клиентов в зоне для активации")]
        public int minClientsInZone = 2;

        [Tooltip("Минимум секунд в зоне до активации")]
        public float minTimeInZone = 15f;

        [Tooltip("Шанс активации (0-1), проверяется периодически")]
        [Range(0f, 1f)] public float triggerChance = 0.1f;

        [Tooltip("Период проверки в секундах")]
        public float checkInterval = 5f;

        [Tooltip("Кулдаун между активациями этой сценки в зоне (секунды)")]
        public float sceneCooldown = 60f;

        [Header("=== ОГРАНИЧЕНИЯ ПО АРХЕТИПАМ ===")]
        [Tooltip("Какие архетипы МОГУТ участвовать (пусто = любые)")]
        public List<string> allowedArchetypes = new List<string>();

        [Tooltip("Какие архетипы НЕ МОГУТ участвовать")]
        public List<string> excludeArchetypes = new List<string>();

        [Header("=== ВРЕМЕННЫЕ ПРЕДПОЧТЕНИЯ ===")]
        [Tooltip("В какое время суток активна сценка (0-1 = ночь-день)")]
        public AnimationCurve timePreference = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 1f)
        );

        [Header("=== КОНТЕНТ ===")]
        [Tooltip("Реплики сценки в порядке воспроизведения")]
        public List<SceneLine> lines = new List<SceneLine>();

        [Tooltip("Пауза между репликами в секундах")]
        public float lineDelay = 1.5f;

        [Tooltip("Длительность показа каждой реплики")]
        public float lineDuration = 3f;

        [Header("=== ЭФФЕКТЫ ===")]
        [Tooltip("Увеличивает ворчание всем в зоне")]
        public bool causeGrumbling = false;

        [Tooltip("Множитель ворчания (1.0 = без изменений)")]
        [Range(0.5f, 2f)] public float grumblingMultiplier = 1.2f;

        [Tooltip("Меняет эмоции участников")]
        public bool affectEmotions = false;

        [Tooltip("Эмоция для участников после сценки")]
        public Emotion postSceneEmotion = Emotion.Neutral;

        [Header("=== ОТЛАДКА ===")]
        public bool debugMode = false;
    }
}
