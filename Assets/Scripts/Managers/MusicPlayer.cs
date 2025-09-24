using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource), typeof(AudioSource), typeof(AudioLowPassFilter))]
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; set; }

    [Header("Ссылки на 'плееры'")]
    [Tooltip("AudioSource для музыки геймплея (дневные/ночные треки)")]
    [SerializeField] private AudioSource gameplaySource;
    [Tooltip("AudioSource для музыки интерфейса (меню, пауза, стол директора)")]
    [SerializeField] private AudioSource uiSource;
    
    [Header("Музыкальные темы")]
    public AudioClip menuTheme;
    public AudioClip directorsOfficeTheme;
    public AudioClip pauseTheme;
    
    [Header("Внутри-игровые плейлисты")]
    public AudioClip[] dayTracks;
    public AudioClip nightTrack;
    public AudioClip radioSwitchSound;

    [Header("Настройки")]
    [Range(0f, 1f)] public float masterVolume = 0.5f;
    [Range(0f, 1f)] public float muffledVolume = 0.25f;
    public float fadeDuration = 0.5f;
    public float muffledFrequency = 1200f;

    private AudioLowPassFilter lowPassFilter;
    private int lastTrackIndex = -1;
    private bool isGameplayMusicActive = false;
    private AudioClip lastPlayedGameplayTrack;
    private Coroutine effectsCoroutine;

    void Awake()
    {
        if (Instance == null) 
        { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
        }
        else if (Instance != this) 
        { 
            Destroy(gameObject);
            return;
        }

        lowPassFilter = GetComponent<AudioLowPassFilter>();
        
        if(gameplaySource) gameplaySource.volume = masterVolume;
        if(uiSource) uiSource.volume = masterVolume;
    }

    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayMenuTheme();
    }

    void Update()
    {
        if (gameplaySource == null || gameplaySource.isPlaying || !isGameplayMusicActive || Time.timeScale == 0f) return;
        PlayCorrectTrackForCurrentTime();
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenuScene")
        {
            PlayMenuTheme();
        }
        else if (scene.name == "GameScene")
        {
            PlayDirectorsOfficeTheme();
        }
    }
    
    public void OnPeriodChanged()
    {
        if (!isGameplayMusicActive || gameplaySource == null || gameplaySource.clip == null) return;
        bool isNightNow = IsNightTime();
        if ((gameplaySource.clip == nightTrack && !isNightNow) || (dayTracks.Contains(gameplaySource.clip) && isNightNow))
        {
            gameplaySource.Stop();
        }
    }

    public void PauseGameplayMusicForManualPause()
    {
        if (!isGameplayMusicActive || gameplaySource == null) return;
        gameplaySource.Pause();
        PlayUiTrack(pauseTheme);
    }

    public void ResumeGameplayMusicFromManualPause()
    {
        if(uiSource) uiSource.Pause();
        if(gameplaySource) gameplaySource.UnPause();
    }
    
    public void PauseGameplayMusicAndPlayOfficeTheme()
    {
        if (!isGameplayMusicActive) return;
        isGameplayMusicActive = false;
        if(gameplaySource) gameplaySource.Stop();
        PlayUiTrack(directorsOfficeTheme);
    }

    public void ResumeGameplayMusic()
    {
        if (isGameplayMusicActive) return;
        isGameplayMusicActive = true;
        if(uiSource) uiSource.Stop();
        if(gameplaySource && lastPlayedGameplayTrack) gameplaySource.Play();
    }
    
    public void StartGameplayMusic() 
    {
        isGameplayMusicActive = true;
        if(uiSource) uiSource.Stop();
        PlayCorrectTrackForCurrentTime();
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

    private void PlayGameplayTrack(AudioClip clip, bool loop)
    {
        if (gameplaySource == null || clip == null) return;
        
        gameplaySource.clip = clip;
        gameplaySource.loop = loop;
        gameplaySource.Play();

        if(clip != nightTrack)
        {
            lastPlayedGameplayTrack = clip;
        }
    }

    private void PlayUiTrack(AudioClip clip)
    {
        if (uiSource == null || clip == null) return;

        if (uiSource.clip == clip && !uiSource.isPlaying)
        {
            uiSource.UnPause();
            return;
        }
        
        if (uiSource.clip == clip && uiSource.isPlaying) return;

        uiSource.clip = clip;
        uiSource.loop = true;
        uiSource.Play();
    }
    
    private void PlayCorrectTrackForCurrentTime()
    {
        if (IsNightTime()) PlayGameplayTrack(nightTrack, true);
        else PlayRandomDayTrack();
    }
    
    private void PlayRandomDayTrack()
    {
        if (dayTracks.Length == 0) return;
        if (dayTracks.Length == 1) { PlayGameplayTrack(dayTracks[0], true); return; }
        int newIndex;
        do { newIndex = Random.Range(0, dayTracks.Length); } while (newIndex == lastTrackIndex && dayTracks.Length > 1);
        lastTrackIndex = newIndex;
        PlayGameplayTrack(dayTracks[lastTrackIndex], false);
    }

    public void PlayMenuTheme() 
    { 
        isGameplayMusicActive = false;
        if(gameplaySource) gameplaySource.Stop();
        PlayUiTrack(menuTheme);
    }
    
    public void PlayDirectorsOfficeTheme() 
    {
        isGameplayMusicActive = false; 
        if(gameplaySource) gameplaySource.Stop();
        PlayUiTrack(directorsOfficeTheme);
    }
    
    public void SetMuffled(bool isMuffled) 
    { 
        if (effectsCoroutine != null) StopCoroutine(effectsCoroutine);
        
        float targetVolume = isMuffled ? muffledVolume : masterVolume;
        float targetFrequency = isMuffled ? muffledFrequency : 22000f;
        effectsCoroutine = StartCoroutine(LerpAudioEffects(targetVolume, targetFrequency));
    }

    private IEnumerator LerpAudioEffects(float targetVol, float targetFreq) 
    { 
        float startVol = gameplaySource.volume;
        float startFreq = lowPassFilter != null ? lowPassFilter.cutoffFrequency : 22000f;
        float time = 0;
        
        while (time < fadeDuration) 
        { 
            time += Time.unscaledDeltaTime;
            float progress = time / fadeDuration; 
            
            float currentVol = Mathf.Lerp(startVol, targetVol, progress);
            if(gameplaySource) gameplaySource.volume = currentVol;
            if(uiSource) uiSource.volume = currentVol;

            if (lowPassFilter != null) 
            {
                lowPassFilter.cutoffFrequency = Mathf.Lerp(startFreq, targetFreq, progress);
            } 
            yield return null;
        }

        if(gameplaySource) gameplaySource.volume = targetVol;
        if(uiSource) uiSource.volume = targetVol;

        if (lowPassFilter != null) 
        {
            lowPassFilter.cutoffFrequency = targetFreq;
        }
        
        effectsCoroutine = null;
    }
    
    private bool IsNightTime()
    {
        if (ClientSpawner.Instance == null || ClientSpawner.Instance.nightPeriodNames == null) return false;
        return ClientSpawner.Instance.nightPeriodNames.Any(p => p.Equals(ClientSpawner.CurrentPeriodName, System.StringComparison.InvariantCultureIgnoreCase));
    }
}