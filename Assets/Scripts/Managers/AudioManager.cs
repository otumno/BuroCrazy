using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace Managers
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Ссылки")]
        [Tooltip("Перетащи сюда ассет MainAudioLibrary")]
        public AudioLibrary audioLibrary;
        [Tooltip("Перетащи сюда MainMixer")]
        public AudioMixer mainMixer;
        
        [Header("Настройки Пула")]
        [Tooltip("Сколько плееров создать на старте")]
        public int initialPoolSize = 15;
        [Tooltip("Максимум плееров (чтобы не забить память)")]
        public int maxPoolSize = 30;

        [Header("Музыка (Постоянный источник)")]
        [Tooltip("Создай дочерний объект с AudioSource для музыки и назначь сюда")]
        public AudioSource musicSource;

        // Пул источников звука (скрытый список)
        private List<AudioSource> sfxPool;
        private GameObject poolContainer;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializePool();
        }

        private void InitializePool()
        {
            sfxPool = new List<AudioSource>();
            // Создаем пустой объект-папку внутри менеджера, чтобы не захламлять иерархию
            poolContainer = new GameObject("SFX_Pool_Container");
            poolContainer.transform.SetParent(transform);

            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewSource();
            }
        }

        private AudioSource CreateNewSource()
        {
            GameObject go = new GameObject($"SFX_Source_{sfxPool.Count}");
            go.transform.SetParent(poolContainer.transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sfxPool.Add(source);
            return source;
        }

        private AudioSource GetAvailableSource()
        {
            // 1. Ищем свободный источник (который сейчас молчит)
            foreach (var source in sfxPool)
            {
                if (!source.isPlaying) return source;
            }

            // 2. Если все заняты, но лимит не превышен -> создаем новый
            if (sfxPool.Count < maxPoolSize)
            {
                return CreateNewSource();
            }

            // 3. Если лимит превышен -> берем первый попавшийся (перебиваем старый звук)
            // Это защита от утечки памяти
            return sfxPool[0]; 
        }

        // --- ПУБЛИЧНЫЙ API: 2D ЗВУК (UI) ---
        public void PlaySound(SoundID id)
        {
            PlayInternal(id, Vector3.zero, false);
        }

        // --- ПУБЛИЧНЫЙ API: 3D ЗВУК (Мир) ---
        public void PlaySoundAt(SoundID id, Vector3 position)
        {
            PlayInternal(id, position, true);
        }

        // Внутренний метод логики
        private void PlayInternal(SoundID id, Vector3 position, bool is3D)
        {
            if (id == SoundID.None || audioLibrary == null) return;

            SoundData data = audioLibrary.GetSound(id);
            if (data == null)
            {
                // Полезный лог, чтобы понять, какой звук мы забыли добавить в базу
                Debug.LogWarning($"[AudioManager] Звук с ID '{id}' не найден в AudioLibrary!");
                return;
            }

            if (data.clip == null) 
            {
                 Debug.LogWarning($"[AudioManager] У звука '{id}' нет аудиоклипа (AudioClip is null)!");
                 return;
            }

            AudioSource source = GetAvailableSource();

            // --- HARD RESET (Сброс "мусора" от предыдущего использования) ---
            source.Stop(); // Останавливаем, если вдруг играл
            source.time = 0; // Перемотка в начало
            source.mute = false;
            source.loop = false;
            source.bypassEffects = false;
            source.bypassListenerEffects = false;
            source.bypassReverbZones = false;
            source.priority = 128;
            // ---------------------------------------------------------------

            source.transform.position = position;
            source.clip = data.clip;
            source.outputAudioMixerGroup = data.mixerGroup; 
            
            // Защита от "Глитч-Питча"
            float basePitch = Mathf.Clamp(data.pitch, 0.1f, 3f); // Не даем питчу быть нулем
            float variance = Mathf.Clamp(data.pitchVariance, 0f, 0.5f);
            
            float randomPitch = Random.Range(-variance, variance);
            source.pitch = basePitch + randomPitch;
            
            source.volume = Mathf.Clamp01(data.volume);
            source.spatialBlend = is3D ? 1f : 0f; 

            if (is3D)
            {
                source.minDistance = 2f;
                source.maxDistance = 20f;
                source.rolloffMode = AudioRolloffMode.Linear;
            }

            // Debug.Log($"[Audio] Playing {id} (Vol: {source.volume}, Pitch: {source.pitch})");
            source.Play();
        }

        // --- УПРАВЛЕНИЕ МУЗЫКОЙ ---
        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null) return;
            
            // Если этот трек уже играет -> ничего не делаем
            if (musicSource.clip == clip && musicSource.isPlaying) return;

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = true;
            // Группу микшера для музыки лучше назначить один раз в инспекторе префаба [SYSTEMS]
            musicSource.Play();
        }
        
        public void StopMusic()
        {
            if (musicSource != null) musicSource.Stop();
        }

        // --- УПРАВЛЕНИЕ ГРОМКОСТЬЮ (Для меню настроек) ---
        public void SetVolume(string parameterName, float normalizedValue)
        {
            // normalizedValue от 0.0001 до 1.0 (слайдер)
            // Конвертируем в Децибелы (-80db до 0db)
            float db = Mathf.Log10(Mathf.Max(0.0001f, normalizedValue)) * 20;
            mainMixer.SetFloat(parameterName, db);
        }
    }
}