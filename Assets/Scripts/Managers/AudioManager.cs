using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using Scriptables.Audio;

namespace Managers
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Ссылки")]
        public SoundLibrary soundLibrary;
        public AudioMixer mainMixer;

        [Header("Микшер Группы")]
        public AudioMixerGroup sfxGroup; 
        
        [Header("Настройки Пула")]
        public int initialPoolSize = 20;
        public int maxPoolSize = 30;

        [Header("Музыка")]
        public AudioSource musicSource;

        private GameObject poolContainer;
        private readonly List<AudioSource> sfxPool = new();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitializePool();
        }

        private void InitializePool()
        {
            poolContainer = new GameObject("SFX_Pool_Container");
            poolContainer.transform.SetParent(transform);

            sfxPool.Capacity = initialPoolSize;
            for (int i = 0; i < initialPoolSize; i++) CreateNewSource();
        }

        private AudioSource CreateNewSource()
        {
            GameObject go = new GameObject($"SFX_Source_{sfxPool.Count}");
            go.transform.SetParent(poolContainer.transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sfxPool.Add(source);
            return source;
        }

        private AudioSource GetAvailableSource()
        {
            foreach (var source in sfxPool)
            {
                if (!source.isPlaying) return source;
            }

            if (sfxPool.Count < maxPoolSize) return CreateNewSource();

            Debug.LogWarning("[AudioManager] Закончились аудиосорсы в пуле!");
            return sfxPool[0]; 
        }

        public void PlaySound(SoundID id) => PlayInternal(id, Vector3.zero, false);
        public void PlaySoundAt(SoundID id, Vector3 position) => PlayInternal(id, position, true);

        // --- ОБНОВЛЕННЫЙ МЕТОД ДЛЯ ГОЛОСОВ ---
        // Используем pitchDelta вместо pitchVariance
        public void PlayVoiceClip(AudioClip clip, Vector3 position, float basePitch, float pitchDelta, float volume)
        {
            if (clip == null) return;

            AudioSource source = GetAvailableSource();
            ResetSource(source);

            source.transform.position = position;
            source.clip = clip;
            if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;

            // Расчет питча через delta
            float randomPitch = Random.Range(-pitchDelta, pitchDelta);
            source.pitch = Mathf.Clamp(basePitch + randomPitch, 0.1f, 3f);
            
            source.volume = Mathf.Clamp01(volume);
            source.spatialBlend = 1f;
            
            source.minDistance = 2f;
            source.maxDistance = 15f;
            source.rolloffMode = AudioRolloffMode.Linear;

            source.Play();
        }

        private void PlayInternal(SoundID id, Vector3 position, bool is3D)
        {
            if (id == SoundID.None || soundLibrary == null) return;

            var soundData = soundLibrary.GetSound(id);
            if (soundData == null || soundData.clip == null) return;

            AudioSource source = GetAvailableSource();
            ResetSource(source);

            source.transform.position = position;
            source.clip = soundData.clip;
            source.outputAudioMixerGroup = soundData.mixerGroup; 
            
            float basePitch = Mathf.Clamp(soundData.pitch, 0.1f, 3f);
            // Используем pitchDelta
            float delta = Mathf.Clamp(soundData.pitchDelta, 0f, 0.5f); 
            
            float randomPitch = Random.Range(-delta, delta);
            source.pitch = basePitch + randomPitch;
            
            source.volume = Mathf.Clamp01(soundData.volume);
            source.spatialBlend = is3D ? 1f : 0f; 

            if (is3D)
            {
                source.minDistance = 2f;
                source.maxDistance = 20f;
                source.rolloffMode = AudioRolloffMode.Linear;
            }

            source.Play();
        }

        private void ResetSource(AudioSource source)
        {
            source.Stop();
            source.mute = false;
            source.loop = false;
            source.priority = 128;
            source.pitch = 1f;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return;

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }
        
        public void StopMusic() => musicSource?.Stop();

        public void SetVolume(string parameterName, float normalizedValue)
        {
            float db = Mathf.Log10(Mathf.Max(0.0001f, normalizedValue)) * 20;
            mainMixer.SetFloat(parameterName, db);
        }
    }
}