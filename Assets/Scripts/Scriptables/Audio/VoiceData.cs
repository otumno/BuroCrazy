using System.Collections.Generic;
using UnityEngine;

namespace Scriptables.Audio
{
    [CreateAssetMenu(fileName = "Voice_New", menuName = "Bureau/Audio/Voice Data")]
    public class VoiceData : ScriptableObject
    {
        [Header("Звуки речи")]
        [Tooltip("Список коротких звуков (бипов, хмыканий), из которых собирается речь.")]
        public List<AudioClip> speechClips;

        [Header("Настройки")]
        [Tooltip("Базовая высота голоса (1 = норма, 0.8 = бас, 1.5 = писк).")]
        [Range(0.5f, 2f)] public float basePitch = 1f;
    
        [Tooltip("Насколько сильно скачет голос (интонация).")]
        [Range(0f, 0.5f)] public float pitchDelta = 0.1f;
	
        [Header("Тайминги")]
        [Tooltip("Пауза между звуками ВНУТРИ одного слова (бип-бип). Обычное значение: 0.05 - 0.08")]
        public float delayPerSyllable = 0.07f;

        [Tooltip("Скорость речи: пауза между словами (в секундах).")]
        public float delayPerWord = 0.1f;

        [Tooltip("Громкость голоса")]
        [Range(0f, 1f)] public float volume = 0.8f;
    
        // Вспомогательный метод для получения случайного клипа
        public AudioClip GetRandomClip()
        {
            if (speechClips == null || speechClips.Count == 0) return null;
            return speechClips[Random.Range(0, speechClips.Count)];
        }
    }
}