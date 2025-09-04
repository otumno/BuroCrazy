// Файл: MusicPlayer.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [Header("Музыкальные темы")]
    [Tooltip("Музыка для главного меню и экрана сохранений")]
    public AudioClip menuTheme;
    [Tooltip("Музыка для экрана начала дня (кабинет директора)")]
    public AudioClip directorsOfficeTheme;

    [Header("Игровые плейлисты")]
    [Tooltip("Музыка, которая играет в течение дня")]
    public AudioClip[] dayTracks;
    [Tooltip("Один трек, который будет играть всю ночь (в цикле)")]
    public AudioClip nightTrack;

    [Header("Эффект приглушения")]
    [Tooltip("Частота среза для фильтра. Чем ниже, тем глуше звук. (н-р, 1200)")]
    public float muffledFrequency = 1200f;
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
    private float initialVolume;
    
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        lowPassFilter = GetComponent<AudioLowPassFilter>(); 
        initialVolume = audioSource.volume;
    }

    void Update()
    {
        // Не переключаем треки дня/ночи, если игра на паузе
        if (Time.timeScale == 0f)
        {
            return;
        }

        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
        
        if (period == "ночь")
        {
            if (!isNightMusic)
            {
                isNightMusic = true;
                PlayTrack(nightTrack, true);
            }
        }
        else
        {
            if (isNightMusic)
            {
                isNightMusic = false;
                audioSource.Stop();
            }

            if (!audioSource.isPlaying)
            {
                PlayRandomDayTrack();
            }
        }
    }

    // --- НОВЫЕ МЕТОДЫ, КОТОРЫХ НЕ ХВАТАЕТ ---
    public void PlayMenuTheme()
    {
        PlayTrack(menuTheme, true);
    }

    public void PlayDirectorsOfficeTheme()
    {
        PlayTrack(directorsOfficeTheme, true);
    }
    
    public void PlayGameplayMusic()
    {
        // Просто останавливаем текущий трек. 
        // Update() сам подхватит и включит нужный трек дня/ночи, когда игра снимется с паузы.
        audioSource.Stop();
        isNightMusic = false; // Сбрасываем флаг, чтобы логика в Update сработала корректно
    }

    private void PlayTrack(AudioClip clip, bool loop)
    {
        if (audioSource.clip == clip && audioSource.isPlaying) return;

        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.loop = loop;
            audioSource.Play();
        }
    }
    
    void PlayRandomDayTrack()
    {
        if (dayTracks.Length == 0) return;
        if (dayTracks.Length == 1)
        {
            PlayTrack(dayTracks[0], true);
            return;
        }

        int newIndex;
        do { newIndex = Random.Range(0, dayTracks.Length);
        } while (newIndex == lastTrackIndex);
        
        lastTrackIndex = newIndex;
        PlayTrack(dayTracks[lastTrackIndex], false); // Дневные треки не зациклены
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

        float targetVolume = isMuffled ? muffledVolume : initialVolume;
        float targetFrequency = isMuffled ? muffledFrequency : 22000f;
        
        effectsCoroutine = StartCoroutine(LerpAudioEffects(targetVolume, targetFrequency));
    }
    
    private IEnumerator LerpAudioEffects(float targetVol, float targetFreq)
    {
        float startVol = audioSource.volume;
        float startFreq = lowPassFilter != null ? lowPassFilter.cutoffFrequency : 22000f;
        float time = 0;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float progress = time / fadeDuration;
            
            audioSource.volume = Mathf.Lerp(startVol, targetVol, progress);
            
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
        yield break;
    }
}