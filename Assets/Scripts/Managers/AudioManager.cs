using System;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using Audio;
using GameInitialization;
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

        private readonly List<AudioInstance> _audioInstances = new();
        private readonly List<AudioInstance> _pausedInstances = new();
        private readonly HashSet<SoundID>    _missingSounds = new();

        private const string SFX_GROUP_NAME = "Sfx";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            InitializePool();
        }

        private void Start()
        {
            // Применяем сохранённую громкость в Start, а не в Awake: SetFloat молча игнорируется,
            // пока микшер ещё не полностью загружен на старте сцены.
            AudioVolumeSettings.Apply(this);
        }

        private void InitializePool()
        {
            _audioInstances.Capacity = initialPoolSize;
            for (int i = 0; i < initialPoolSize; i++)
                CreateAudioInstance();
        }

        private AudioInstance GetAvailableInstance()
        {
            foreach (var source in _audioInstances)
            {
                if (!source.isBusy)
                    return source;
            }

            if (_audioInstances.Count < maxPoolSize)
            {
                var audioInstance = CreateAudioInstance();
                return audioInstance;
            }

            Debug.LogWarning("[AudioManager] Закончились аудиосорсы в пуле!");
            return _audioInstances[0]; 
        }

        // play 2D sound (2D sounds don't depend on position)
        public AudioInstance PlaySound(SoundID id) => PlayInternal(id, null, null);
        // play 3D sound at position
        public AudioInstance PlaySound(SoundID id, Vector3 position) => PlayInternal(id, null, position);
        // play 3D sound at position and link audioSource to gameObject while clip is playing
        public AudioInstance PlaySound(SoundID id, Transform linkTransform) => PlayInternal(id, linkTransform, null);

        public AudioInstance PlayAudioClip2D(AudioClip clip)
        {
            var source = GetAvailableInstance();
            source.ResetSource();
            source.PlayAudioClip2D(clip, sfxGroup);
            return source;
        }
        
        public AudioInstance PlayVoiceClip(AudioClip clip,
                                  Vector3 position,
                                  float basePitch,
                                  float pitchDelta,
                                  float volume)
        {
            var source = GetAvailableInstance();
            source.ResetSource();
            source.PlayVoiceClip(clip, position, basePitch, pitchDelta, volume, sfxGroup);
            return source;
        }

        private AudioInstance PlayInternal(SoundID id, Transform linkTransform, Vector3? position)
        {
            if (id == SoundID.None)
                return null;

            var soundData = soundLibrary.GetSound(id);
            if (soundData == null)
            {
                if (!_missingSounds.Contains(id))
                    Debug.LogError($"Sound not found: {id.ToString()}");
                
                // чтобы не дублировать лог по многу раз
                _missingSounds.Add(id);
                return null;
            }

            // Debug.Log($"[AudioManager] Playing: {id} | clips={soundData.clips?.Length} | volume={soundData.volume} | mixer={soundData.mixerGroup?.name}");

            var audioInstance = GetAvailableInstance();
            audioInstance.ResetSource();
            audioInstance.Play(soundData, linkTransform, position);
            return audioInstance;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null)
                return;
            
            if (musicSource.clip == clip && musicSource.isPlaying)
                return;

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }
        
        public void StopMusic() => musicSource.Stop();

        public void SetVolume(string parameterName, float normalizedValue)
        {
            float db = Mathf.Log10(Mathf.Max(0.0001f, normalizedValue)) * 20;
            mainMixer.SetFloat(parameterName, db);
        }

        private void OnIsPausedChange(bool isPaused)
        {
            if (isPaused)
            {
                foreach (var audioInstance in _audioInstances)
                {
                    if (!audioInstance.isPlaying ||
                        string.Equals(audioInstance.audioGroup.name, SFX_GROUP_NAME, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    audioInstance.Pause();
                    _pausedInstances.Add(audioInstance);
                }
            }
            else
            {
                foreach (var audioInstance in _pausedInstances)
                    audioInstance.Continue();
                
                _pausedInstances.Clear();
            }
        }
        
        private AudioInstance CreateAudioInstance()
        {
            var go = new GameObject($"SFX_Source_{_audioInstances.Count}");
            go.transform.SetParent(transform);
            
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.Stop();

            var audioInstance = go.AddComponent<AudioInstance>();
            audioInstance.Init(audioSource);
            _audioInstances.Add(audioInstance);
            return audioInstance;
        }
    }
}