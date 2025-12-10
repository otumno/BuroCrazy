using UnityEngine;
using UnityEngine.SceneManagement;
using Data.Calendar;
using Scriptables.Audio;

namespace Managers
{
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        [Header("Музыкальные темы")]
        public AudioClip menuTheme;
        public AudioClip directorsOfficeTheme;
        public AudioClip pauseTheme;
        public AudioClip archiveTheme;
        
        [Header("Внутри-игровые плейлисты")]
        public AudioClip[] dayTracks;
        public AudioClip nightTrack;
        
        [Header("SFX переключения")]
        public SoundID radioSwitchSound = SoundID.UI_Click_Default;

        [Header("Настройки")]
        public float muffledVolume = 0.3f;
        
        private int lastTrackIndex = -1;
        private bool isGameplayMusicActive = false;
        private AudioClip lastPlayedGameplayTrack;

        private bool isMuffled = false;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
            }
        }
        
        void OnDestroy()
        {
             SceneManager.sceneLoaded -= OnSceneLoaded;
             if (TimeManager.Instance != null) TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenuScene") PlayMenuTheme();
            else if (scene.name == "GameScene") PlayDirectorsOfficeTheme();
        }
        
        private void OnPeriodChanged(PeriodSettings settings)
        {
            if (isGameplayMusicActive)
            {
                PlayCorrectTrackForCurrentTime();
            }
        }

        // --- ЛОГИКА ВОСПРОИЗВЕДЕНИЯ ---

        private void PlayTrack(AudioClip clip)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(clip);
            }
        }

        public void PlayMenuTheme()
        {
            isGameplayMusicActive = false;
            PlayTrack(menuTheme);
            SetMuffled(false);
        }

        public void PlayDirectorsOfficeTheme()
        {
            isGameplayMusicActive = false;
            PlayTrack(directorsOfficeTheme);
            SetMuffled(true); 
        }

        // --- ДОБАВЛЕНО: Метод для Архива ---
        public void PlayArchiveTheme()
        {
            isGameplayMusicActive = false;
            PlayTrack(archiveTheme);
            SetMuffled(false);
        }
        // ----------------------------------
        
        public void StartGameplayMusic()
        {
            isGameplayMusicActive = true;
            SetMuffled(false);

            if (lastPlayedGameplayTrack != null && !IsNightTime())
            {
                PlayTrack(lastPlayedGameplayTrack);
            }
            else
            {
                PlayCorrectTrackForCurrentTime();
            }
        }
        
        public void PauseGameplayMusicAndPlayOfficeTheme()
        {
            if (!isGameplayMusicActive) return;
            isGameplayMusicActive = false;
            PlayTrack(directorsOfficeTheme);
        }

        public void PauseGameplayMusicForManualPause()
        {
            PlayTrack(pauseTheme);
            SetMuffled(true);
        }

        public void ResumeGameplayMusicFromManualPause()
        {
            if (isGameplayMusicActive)
            {
                SetMuffled(false);
                PlayCorrectTrackForCurrentTime();
            }
            else
            {
                PlayDirectorsOfficeTheme();
            }
        }

        public void RequestNextTrack()
        {
            if (!isGameplayMusicActive || IsNightTime() || Time.timeScale == 0f || dayTracks.Length == 0) return;
            
            AudioManager.Instance?.PlaySound(radioSwitchSound);
            PlayRandomDayTrack();
        }

        private void PlayCorrectTrackForCurrentTime()
        {
            if (IsNightTime())
            {
                PlayTrack(nightTrack);
            }
            else
            {
                if (AudioManager.Instance != null) 
                     PlayRandomDayTrack();
            }
        }

        private void PlayRandomDayTrack()
        {
            if (dayTracks.Length == 0) return;
            if (dayTracks.Length == 1) { PlayTrack(dayTracks[0]); return; }
            
            int newIndex;
            do { newIndex = Random.Range(0, dayTracks.Length); } while (newIndex == lastTrackIndex);
            lastTrackIndex = newIndex;
            
            lastPlayedGameplayTrack = dayTracks[lastTrackIndex];
            PlayTrack(lastPlayedGameplayTrack);
        }

        // --- ЭФФЕКТЫ ---

        public void SetMuffled(bool muffled)
        {
            isMuffled = muffled;
            // TODO: Связать с параметром LowPass в микшере
        }
        
        private static bool IsNightTime() => TimeManager.Instance != null &&
                                             TimeManager.Instance.GetCurrentPeriodType().IsNight();
    }
}