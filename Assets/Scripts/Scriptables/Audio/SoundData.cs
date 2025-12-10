using UnityEngine;
using UnityEngine.Audio;

namespace Scriptables.Audio
{
    [System.Serializable]
    public class SoundData
    {
        public SoundID id;
    
        // можно так-то в массив переделать, чтобы рандомно 1 из проигрывать
        public AudioClip clip;
    
        [Header("Settings")]
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Range(0f, 0.5f)] public float pitchDelta = 0.1f;
    
        [Header("2D / 3D")]
        [Range(0f, 1f)] public float  SpatialBlend = 1.0f;
    
        [Header("3D")]
        public AudioRolloffMode Rolloff  = AudioRolloffMode.Linear;
        [Range(0f, 5f)] public float  DopplerLevel = 1;
        public float DistanceMin  = 0f;
        public float DistanceMax  = 50.0f;
    
        [Header("Routing")]
        [Tooltip("В какую группу микшера отправить? (Music, SFX, UI)")]
        public AudioMixerGroup mixerGroup;
    }
}