using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace Scriptables.Audio
{
    [System.Serializable]
    public class SoundData
    {
        public SoundID id;
    
        [Header("Звук рандомно 1 из:")]
        public AudioClip[] clips;

        [Header("Settings")]
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Range(0f, 0.5f)] public float pitchDelta = 0.1f;
        public int Priority = 128;
        public bool Loop = false;
    
        [Header("2D / 3D")]
        [Range(0f, 1f)] public float  SpatialBlend = 1.0f;
    
        [Header("3D")]
        public AudioRolloffMode Rolloff  = AudioRolloffMode.Linear;
        [Range(0f, 5f)] public float  DopplerLevel = 1;
        public float DistanceMin  = 0f;
        public float DistanceMax  = 50.0f;
        
        [Header("Fade In")]
        public float FadeInDuration = 0f;
        public Ease  FadeInEase     = Ease.Linear;
    
        [Header("Fade Out")]
        public float FadeOutDuration = 0f;
        public Ease  FadeOutEase     = Ease.Linear;
    
        [Header("Routing")]
        [Tooltip("В какую группу микшера отправить? (Music, SFX, UI)")]
        public AudioMixerGroup mixerGroup;

        public bool Validate(int k)
        {
            if (id == SoundID.None)
            {
                Debug.LogError($"Invalid SoundId.None, dataIndex: {k}");
                return false;
            }

            if (clips == null || clips.Length == 0)
            {
                Debug.LogError($"sound clips are null or empty, dataIndex: {k}");
                return false;
            }

            volume = Mathf.Clamp01(volume);
            pitch = Mathf.Clamp(pitch, 0.1f, 0.3f);
            pitchDelta = Mathf.Clamp(pitchDelta, 0f, 0.5f);
            SpatialBlend = Mathf.Clamp(SpatialBlend, 0f, 1f);
            DopplerLevel = Mathf.Clamp(DopplerLevel, 0f, 5f);

            return true;
        }
    }
}