// File: MusicPlayer.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; set; }

 void Awake()
{
    // Управление синглтоном удалено. Этим теперь занимается MenuManager.
    
    // Оставляем только настройку компонентов.
    audioSource = GetComponent<AudioSource>();
    lowPassFilter = GetComponent<AudioLowPassFilter>();
    initialVolume = audioSource.volume;
}

    [Header("Musical Themes")]
    public AudioClip menuTheme;
    public AudioClip directorsOfficeTheme;
    
    [Header("In-Game Playlists")]
    public AudioClip[] dayTracks;
    public AudioClip nightTrack;
    public AudioClip radioSwitchSound;
    
    [Header("Muffling Effect")]
    public float muffledFrequency = 1200f;
    [Range(0f, 1f)]
    public float muffledVolume = 0.5f;
    public float fadeDuration = 0.5f;

    private AudioSource audioSource;
    private AudioLowPassFilter lowPassFilter;
    private Coroutine effectsCoroutine;
    private int lastTrackIndex = -1;
    private bool isGameplayMusicActive = false;
    private float initialVolume;
    private AudioClip lastPlayedGameplayTrack;
    private float lastTrackTime = 0f;

    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayMenuTheme();
    }

    void Update()
    {
        if (audioSource.isPlaying || !isGameplayMusicActive || Time.timeScale == 0f)
        {
            return;
        }
        PlayCorrectTrackForCurrentTime();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    if (scene.name == "MainMenuScene")
    {
        PlayMenuTheme();
    }
    // <<< ДОБАВЬТЕ ЭТИ СТРОКИ >>>
    else if (scene.name == "GameScene") // <-- Укажите здесь правильное имя вашей игровой сцены
    {
        // Когда загружается игровая сцена, мы еще не начали день.
        // Поэтому включаем спокойную музыку кабинета директора.
        PlayDirectorsOfficeTheme();
    }
}
    
    public void OnPeriodChanged()
    {
        if (!isGameplayMusicActive) return;
        bool isNightNow = IsNightTime();
        if ((audioSource.clip == nightTrack && !isNightNow) || (dayTracks.Contains(audioSource.clip) && isNightNow))
        {
            audioSource.Stop();
        }
    }
    
    public void PauseGameplayMusicAndPlayOfficeTheme()
    {
        if (!isGameplayMusicActive || audioSource.clip == directorsOfficeTheme) return;
        lastTrackTime = audioSource.time;
        isGameplayMusicActive = false;
        PlayTrack(directorsOfficeTheme, true);
    }

    public void ResumeGameplayMusic()
    {
        if (isGameplayMusicActive) return;
        isGameplayMusicActive = true;
        PlayTrack(lastPlayedGameplayTrack, dayTracks.Length <= 1);
        if (audioSource != null)
        {
            audioSource.time = lastTrackTime;
        }
    }

    public void RequestNextTrack()
    {
        if (!isGameplayMusicActive || IsNightTime() || Time.timeScale == 0f || dayTracks.Length == 0) return;
        if (radioSwitchSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(radioSwitchSound, Camera.main.transform.position);
        }
        PlayRandomDayTrack();
    }
    
    public void StartGameplayMusic() 
    {
        isGameplayMusicActive = true;
        audioSource.Stop();
        PlayCorrectTrackForCurrentTime();
    }

    private void PlayCorrectTrackForCurrentTime()
    {
        if (IsNightTime())
        {
            PlayTrack(nightTrack, true);
        }
        else
        {
            PlayRandomDayTrack();
        }
    }

    private bool IsNightTime()
    {
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.nightPeriodNames == null) return false;
        return ClientSpawner.Instance.nightPeriodNames.Any(p => p.Equals(ClientSpawner.CurrentPeriodName, System.StringComparison.InvariantCultureIgnoreCase));
    }
    
    private void PlayTrack(AudioClip clip, bool loop)
    {
        if (clip == null) { audioSource.Stop(); return; };
        if (audioSource.clip == clip && audioSource.isPlaying) return;
        
        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.Play();

        if(isGameplayMusicActive && clip != nightTrack && clip != directorsOfficeTheme && clip != menuTheme)
        {
            lastPlayedGameplayTrack = clip;
        }
    }
    
    private void PlayRandomDayTrack()
    {
        if (dayTracks.Length == 0) return;
        if (dayTracks.Length == 1) { PlayTrack(dayTracks[0], true); return; }
        int newIndex;
        do { newIndex = Random.Range(0, dayTracks.Length); } while (newIndex == lastTrackIndex);
        lastTrackIndex = newIndex;
        PlayTrack(dayTracks[lastTrackIndex], false);
    }
    
    public void PlayMenuTheme() { isGameplayMusicActive = false; lastPlayedGameplayTrack = null; PlayTrack(menuTheme, true); }
    public void PlayDirectorsOfficeTheme() { isGameplayMusicActive = false; lastPlayedGameplayTrack = null; PlayTrack(directorsOfficeTheme, true); }
    
    public void SetMuffled(bool isMuffled) 
    { 
        if (effectsCoroutine != null) StopCoroutine(effectsCoroutine);
        
        bool isNight = IsNightTime();
        if (isNight) 
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
            time += Time.unscaledDeltaTime;
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
        yield break; // This line fixes the error
    }
}