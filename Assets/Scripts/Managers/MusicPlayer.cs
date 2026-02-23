using UnityEngine;
using UnityEngine.SceneManagement;
using Data.Calendar;
using Managers.Teletype;
using Scriptables.Audio;

namespace Managers
{
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        [Header("Музыкальные темы")]
        public AudioClip firstMenuTrack;
        public AudioClip menuTheme;
        public AudioClip directorsOfficeTheme;
        public AudioClip pauseTheme;
        public AudioClip archiveTheme;
        
        [Header("Плейлисты")]
        public AudioClip[] menuTracks;
        public AudioClip[] directorTracks;
        public AudioClip[] archiveTracks;
        
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
        private float _savedTrackTime = 0f;
        private AudioClip _savedTrackClip;

        private bool isMuffled = false;
        
        private bool _hasPlayedGame = false;
        private int _lastMenuTrackIndex = -1;
        private int _lastDirectorTrackIndex = -1;
        private int _lastArchiveTrackIndex = -1;
        private bool _shouldRefreshMenuTrack = false;

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
            if (scene.name == "MainMenuScene") 
            {
                PlayMenuTheme();
            }
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

        public void StopMusic()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopMusic();
            }
        }

        public void PlayMenuTheme()
        {
            isGameplayMusicActive = false;
            
            AudioClip trackToPlay;
            
            if (!_hasPlayedGame && firstMenuTrack != null)
            {
                trackToPlay = firstMenuTrack;
            }
            else if (menuTracks != null && menuTracks.Length > 0)
            {
                trackToPlay = GetRandomTrack(menuTracks, ref _lastMenuTrackIndex);
            }
            else
            {
                trackToPlay = menuTheme;
            }
            
            PlayTrack(trackToPlay);
            SetMuffled(false);
        }
        
        public void OnGameStarted()
        {
            _hasPlayedGame = true;
            _shouldRefreshMenuTrack = true;
        }
        
        public void RefreshMenuTrackIfNeeded()
        {
            if (_shouldRefreshMenuTrack)
            {
                _shouldRefreshMenuTrack = false;
                _lastMenuTrackIndex = -1;
                PlayMenuTheme();
            }
        }

        public void PlayDirectorsOfficeTheme()
        {
            isGameplayMusicActive = false;
            
            AudioClip trackToPlay;
            if (directorTracks != null && directorTracks.Length > 0)
            {
                trackToPlay = GetRandomTrack(directorTracks, ref _lastDirectorTrackIndex);
            }
            else
            {
                trackToPlay = directorsOfficeTheme;
            }
            
            PlayTrack(trackToPlay);
            SetMuffled(true); 
        }

        public void PlayArchiveTheme()
        {
            isGameplayMusicActive = false;
            
            AudioClip trackToPlay;
            if (archiveTracks != null && archiveTracks.Length > 0)
            {
                trackToPlay = GetRandomTrack(archiveTracks, ref _lastArchiveTrackIndex);
            }
            else
            {
                trackToPlay = archiveTheme;
            }
            
            PlayTrack(trackToPlay);
            SetMuffled(false);
        }
        
        private AudioClip GetRandomTrack(AudioClip[] tracks, ref int lastIndex)
        {
            if (tracks == null || tracks.Length == 0) return null;
            
            if (tracks.Length == 1)
            {
                lastIndex = 0;
                return tracks[0];
            }
            
            int newIndex;
            do 
            {
                newIndex = Random.Range(0, tracks.Length);
            } while (newIndex == lastIndex);
            
            lastIndex = newIndex;
            return tracks[newIndex];
        }
        
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
            // 1. Сохраняем текущий трек и время, если это была игровая музыка
            if (isGameplayMusicActive && AudioManager.Instance != null && AudioManager.Instance.musicSource.isPlaying)
            {
                _savedTrackClip = AudioManager.Instance.musicSource.clip;
                _savedTrackTime = AudioManager.Instance.musicSource.time;
            }
            else
            {
                _savedTrackClip = null;
                _savedTrackTime = 0f;
            }

            // 2. Включаем музыку паузы
            PlayTrack(pauseTheme);
            SetMuffled(true);
        }

        public void ResumeGameplayMusicFromManualPause()
        {
            if (isGameplayMusicActive)
            {
                SetMuffled(false);

                // 1. Если у нас есть сохраненный трек — восстанавливаем его
                if (_savedTrackClip != null)
                {
                    PlayTrack(_savedTrackClip);
                    
                    // Важно: устанавливаем время ПОСЛЕ запуска PlayTrack
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.musicSource.time = _savedTrackTime;
                    }
                }
                else
                {
                    // Если нечего восстанавливать — запускаем как обычно
                    PlayCorrectTrackForCurrentTime();
                }
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
            bool night = TimeManager.Instance != null && TimeManager.Instance.GetCurrentPeriodType().IsNight();
            Debug.Log($"[MusicPlayer] Проверка трека. Ночь? {night}. Текущий клип: {AudioManager.Instance?.musicSource?.clip?.name}");

            if (night)
            {
                Debug.Log("[MusicPlayer] Включаю ночной трек.");
                LogTrackChange(nightTrack?.name);
                PlayTrack(nightTrack);
            }
            else
            {
                Debug.Log("[MusicPlayer] Включаю дневной плейлист.");
                // Даже если что-то играет, если это не дневной трек (например, офисная тема), надо сменить
                PlayRandomDayTrack();
            }
        }

        private void LogTrackChange(string trackName)
        {
            if (TeletypeManager.Instance == null || string.IsNullOrEmpty(trackName)) return;
            TeletypeManager.Instance.Log($"♪ {trackName}", false, Managers.Teletype.TeletypeMessageType.Music);
        }

        private void PlayRandomDayTrack()
        {
            if (dayTracks.Length == 0)
            {
                Debug.LogError("[MusicPlayer] ОШИБКА: Список dayTracks пуст!");
                return;
            }

            // ... (старая логика выбора индекса) ...
            int newIndex;
            if (dayTracks.Length == 1) newIndex = 0;
            else
            {
                do { newIndex = Random.Range(0, dayTracks.Length); } while (newIndex == lastTrackIndex);
            }
            lastTrackIndex = newIndex;

            lastPlayedGameplayTrack = dayTracks[lastTrackIndex];
            Debug.Log($"[MusicPlayer] Выбран трек: {lastPlayedGameplayTrack?.name}");
            LogTrackChange(lastPlayedGameplayTrack?.name);
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