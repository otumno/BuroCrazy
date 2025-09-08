// Файл: MusicPlayer.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [Header("Музыкальные темы")]
    public AudioClip menuTheme;
    public AudioClip directorsOfficeTheme;

    [Header("Игровые плейлисты")]
    public AudioClip[] dayTracks;
    public AudioClip nightTrack;
    public AudioClip radioSwitchSound; 

    [Header("Эффект приглушения")]
    public float muffledFrequency = 1200f;
    [Range(0f, 1f)]
    public float muffledVolume = 0.5f;
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
        if (Time.timeScale == 0f) return;

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

            if (!audioSource.isPlaying && dayTracks.Length > 0)
            {
                PlayRandomDayTrack();
            }
        }
    }
    
    public void RequestNextTrack()
    {
        if (isNightMusic || Time.timeScale == 0f || dayTracks.Length == 0)
        {
            return;
        }

        if (radioSwitchSound != null)
        {
            // --- ИЗМЕНЕНИЕ ЗДЕСЬ ---
            // Проверяем, что главная камера существует, и проигрываем звук на ее позиции
            if (Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(radioSwitchSound, Camera.main.transform.position);
            }
        }
        
        PlayRandomDayTrack();
    }
    
    private void PlayTrack(AudioClip clip, bool loop)
    {
        if (clip == null) return;
        if (audioSource.clip == clip && audioSource.isPlaying) return;
        
        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.Play();
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
        do { newIndex = Random.Range(0, dayTracks.Length); } while (newIndex == lastTrackIndex);
        
        lastTrackIndex = newIndex;
        PlayTrack(dayTracks[lastTrackIndex], false);
    }
    
    public void PlayMenuTheme() { PlayTrack(menuTheme, true); }
    public void PlayDirectorsOfficeTheme() { PlayTrack(directorsOfficeTheme, true); }
    public void PlayGameplayMusic() { audioSource.Stop(); isNightMusic = false; }
    public void SetMuffled(bool isMuffled) { if (effectsCoroutine != null) StopCoroutine(effectsCoroutine); if (isNightMusic) { if (lowPassFilter != null) lowPassFilter.cutoffFrequency = 22000f; audioSource.volume = initialVolume; return; } float targetVolume = isMuffled ? muffledVolume : initialVolume; float targetFrequency = isMuffled ? muffledFrequency : 22000f; effectsCoroutine = StartCoroutine(LerpAudioEffects(targetVolume, targetFrequency)); }
    private IEnumerator LerpAudioEffects(float targetVol, float targetFreq) { float startVol = audioSource.volume; float startFreq = lowPassFilter != null ? lowPassFilter.cutoffFrequency : 22000f; float time = 0; while (time < fadeDuration) { time += Time.unscaledDeltaTime; float progress = time / fadeDuration; audioSource.volume = Mathf.Lerp(startVol, targetVol, progress); if (lowPassFilter != null) { lowPassFilter.cutoffFrequency = Mathf.Lerp(startFreq, targetFreq, progress); } yield return null; } audioSource.volume = targetVol; if (lowPassFilter != null) { lowPassFilter.cutoffFrequency = targetFreq; } effectsCoroutine = null; yield break; }
}