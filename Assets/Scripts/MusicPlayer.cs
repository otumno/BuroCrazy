// Файл: MusicPlayer.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [Header("Плейлисты")]
    [Tooltip("Музыка, которая играет в течение дня")]
    public AudioClip[] dayTracks;
    [Tooltip("Один трек, который будет играть всю ночь (в цикле)")]
    public AudioClip nightTrack;

    [Header("Эффект приглушения")]
    [Tooltip("Частота среза для фильтра. Чем ниже, тем глуше звук. (н-р, 1200)")]
    public float muffledFrequency = 1200f;
    
    // --- НОВОЕ: Настройка громкости ---
    [Tooltip("Громкость музыки в приглушенном состоянии (от 0.0 до 1.0)")]
    [Range(0f, 1f)]
    public float muffledVolume = 0.5f;

    [Tooltip("Как быстро (в секундах) звук меняется")]
    public float fadeDuration = 0.5f;

    private AudioSource audioSource;
    private AudioLowPassFilter lowPassFilter;
    private Coroutine effectsCoroutine;
    private int lastTrackIndex = -1;
    private bool isNightMusic = false;
    private float initialVolume; // Запоминаем начальную громкость
    
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        lowPassFilter = GetComponent<AudioLowPassFilter>(); 
        initialVolume = audioSource.volume; // Сохраняем громкость из инспектора
    }

    void Update()
    {
        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
        
        if (period == "ночь")
        {
            if (!isNightMusic)
            {
                isNightMusic = true;
                audioSource.Stop();
                audioSource.clip = nightTrack;
                audioSource.loop = true;
                if(nightTrack != null) audioSource.Play();
                
                // Принудительно отключаем все эффекты для ночной музыки
                if(effectsCoroutine != null) StopCoroutine(effectsCoroutine);
                if (lowPassFilter != null) lowPassFilter.cutoffFrequency = 22000f;
                audioSource.volume = initialVolume;
            }
        }
        else
        {
            if (isNightMusic)
            {
                isNightMusic = false;
                audioSource.Stop();
                audioSource.loop = false;
            }

            if (!audioSource.isPlaying)
            {
                PlayRandomDayTrack();
            }
        }
    }

    void PlayRandomDayTrack()
    {
        if (dayTracks.Length == 0) return;
        if (dayTracks.Length == 1)
        {
            if (audioSource.clip != dayTracks[0])
            {
                audioSource.clip = dayTracks[0];
                audioSource.Play();
            }
            audioSource.loop = true;
            return;
        }

        int newIndex;
        do { newIndex = Random.Range(0, dayTracks.Length);
        } while (newIndex == lastTrackIndex);
        
        lastTrackIndex = newIndex;
        audioSource.clip = dayTracks[lastTrackIndex];
        audioSource.Play();
    }

    public void SetMuffled(bool isMuffled)
    {
        if (effectsCoroutine != null) StopCoroutine(effectsCoroutine);

        if (isNightMusic)
        {
            if (lowPassFilter != null) lowPassFilter.cutoffFrequency = 22000f;
            audioSource.volume = initialVolume;
            return;
        }

        // Определяем целевые значения для громкости и фильтра
        float targetVolume = isMuffled ? muffledVolume : initialVolume;
        float targetFrequency = isMuffled ? muffledFrequency : 22000f;
        
        effectsCoroutine = StartCoroutine(LerpAudioEffects(targetVolume, targetFrequency));
    }

    // --- ИЗМЕНЕНО: Корутина теперь управляет и громкостью, и фильтром ---
    private IEnumerator LerpAudioEffects(float targetVol, float targetFreq)
    {
        float startVol = audioSource.volume;
        float startFreq = lowPassFilter != null ? lowPassFilter.cutoffFrequency : 22000f;
        float time = 0;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float progress = time / fadeDuration;
            
            // Плавно меняем громкость
            audioSource.volume = Mathf.Lerp(startVol, targetVol, progress);
            
            // Плавно меняем частоту фильтра
            if (lowPassFilter != null)
            {
                lowPassFilter.cutoffFrequency = Mathf.Lerp(startFreq, targetFreq, progress);
            }
            
            yield return null;
        }

        audioSource.volume = targetVol;
        if (lowPassFilter != null)
        {
            lowPassFilter.cutoffFrequency = targetFreq;
        }
        effectsCoroutine = null;
    }
}