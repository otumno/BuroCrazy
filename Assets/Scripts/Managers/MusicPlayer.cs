using UnityEngine;
using UnityEngine.SceneManagement;
using Data.Calendar;
using Managers.Teletype;
using Scriptables.Audio;
using UI;

namespace Managers
{
    public enum MusicState
    {
        None,
        Menu,
        Office,
        Gameplay,
        Archive
    }

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
        
        private MusicState currentState = MusicState.Menu;

        private bool isMuffled = false;
        
        private bool _hasPlayedGame = false;
        private int _lastMenuTrackIndex = -1;
        private int _lastDirectorTrackIndex = -1;
        private int _lastArchiveTrackIndex = -1;
        private bool _shouldRefreshMenuTrack = false;

        [Header("Клоун")]
        public AudioClip clownMusicTrack;
        private bool isClownMusicForced = false;

        [Header("Музыка диалогов")]
        public AudioClip[] phoneDialogTracks;
        public AudioClip[] worldDialogTracks;
        private AudioClip _previousTrack;
        private float _previousTrackTime;

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

        public void SwitchToDialogueMusic(DialogueSystem.Data.DialogueType type)
        {
            var tracks = type == DialogueSystem.Data.DialogueType.Phone ? phoneDialogTracks : worldDialogTracks;
            if (tracks == null || tracks.Length == 0) return;
            
            if (AudioManager.Instance != null && AudioManager.Instance.musicSource != null)
            {
                _previousTrack = AudioManager.Instance.musicSource.clip;
                _previousTrackTime = AudioManager.Instance.musicSource.time;
            }
            
            var clip = tracks[Random.Range(0, tracks.Length)];
            PlayTrack(clip);
        }

        public void RestorePreviousMusic()
        {
            if (_previousTrack != null)
            {
                if (AudioManager.Instance != null && AudioManager.Instance.musicSource != null)
                {
                    AudioManager.Instance.musicSource.time = _previousTrackTime;
                }
                PlayTrack(_previousTrack);
                _previousTrack = null;
            }
        }
        
        // === Новые методы для Cinematic System ===
        
        /// <summary>
        /// Проиграть трек один раз, сохранив текущий для последующего восстановления.
        /// Используется в CinematicSystem для PlayMusic.
        /// </summary>
        public void PlayOneShotTrack(AudioClip clip)
        {
            if (clip == null) return;
            
            // Сохраняем текущий трек и время
            if (AudioManager.Instance != null && AudioManager.Instance.musicSource != null)
            {
                _previousTrack = AudioManager.Instance.musicSource.clip;
                _previousTrackTime = AudioManager.Instance.musicSource.time;
            }
            
            // Проигрываем новый трек
            PlayTrack(clip);
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
            if (!isGameplayMusicActive) return;

            bool isNightNow = settings.PeriodType.IsNight();
            
            var previousSettings = TimeManager.Instance.GetPreviousPeriodSettings();
            bool wasNight = previousSettings != null && previousSettings.PeriodType.IsNight();

            if (wasNight && !isNightNow)
            {
                Debug.Log("<color=orange>[MusicPlayer]</color> СИГНАЛ: Переход НОЧЬ -> ДЕНЬ. Принудительная смена плейлиста.");
                ForceSwitchToDayMusic();
            }
            else if (!wasNight && isNightNow)
            {
                Debug.Log("<color=blue>[MusicPlayer]</color> СИГНАЛ: Переход ДЕНЬ -> НОЧЬ. Включаем ночной трек.");
                ForceSwitchToNightMusic();
            }
        }

        private void ForceSwitchToDayMusic()
        {
            lastPlayedGameplayTrack = null; 
            
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.musicSource.Stop();
                AudioManager.Instance.musicSource.clip = null;
            }

            PlayRandomDayTrack();
        }

        private void ForceSwitchToNightMusic()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.musicSource.Stop();
            }
            
            LogTrackChange(nightTrack?.name);
            PlayTrack(nightTrack);
        }

        private void Update()
        {
            // Если принудительно играет музыка клоуна - не трогаем
            if (isClownMusicForced) return;

            // Проверка каждую секунду (примерно 60 кадров)
            if (Time.frameCount % 60 == 0 && AudioManager.Instance != null && AudioManager.Instance.musicSource != null)
            {
                bool isPlaying = AudioManager.Instance.musicSource.isPlaying;
                
                // Если музыка не играет и мы в нужном состоянии - запускаем следующий трек
                if (!isPlaying)
                {
                    if (currentState == MusicState.Menu)
                    {
                        // Автопереключение для меню
                        if (menuTracks != null && menuTracks.Length > 0)
                        {
                            PlayTrack(GetRandomTrack(menuTracks, ref _lastMenuTrackIndex));
                        }
                        else if (menuTheme != null)
                        {
                            PlayTrack(menuTheme);
                        }
                    }
                    else if (currentState == MusicState.Archive)
                    {
                        // Автопереключение для архива
                        if (archiveTracks != null && archiveTracks.Length > 0)
                        {
                            PlayTrack(GetRandomTrack(archiveTracks, ref _lastArchiveTrackIndex));
                        }
                        else if (archiveTheme != null)
                        {
                            PlayTrack(archiveTheme);
                        }
                    }
                    else if (isGameplayMusicActive && currentState == MusicState.Gameplay)
                    {
                        // Автопереключение для геймплея (день)
                        bool isNightTime = TimeManager.Instance != null && TimeManager.Instance.IsNight();
                        if (!isNightTime && dayTracks != null && dayTracks.Length > 0)
                        {
                            PlayRandomDayTrack();
                        }
                    }
                }
                
                // Дополнительная проверка для геймплея
                if (isGameplayMusicActive)
                {
                    bool isNightTime = TimeManager.Instance.IsNight();
                    bool isPlayingNightClip = AudioManager.Instance.musicSource.clip == nightTrack;

                    if (!isNightTime && isPlayingNightClip)
                    {
                        Debug.LogWarning("[MusicPlayer] Update-контроль: Обнаружен ночной трек ДНЕМ. Исправляю.");
                        ForceSwitchToDayMusic();
                    }
                }
            }
        }

        // --- ЛОГИКА ВОСПРОИЗВЕДЕНИЯ ---

        private void PlayTrack(AudioClip clip)
        {
            if (AudioManager.Instance != null)
            {
                // Выключаем loop, чтобы мы могли отслеживать конец трека для автопереключения
                AudioManager.Instance.musicSource.loop = false;
                AudioManager.Instance.PlayMusic(clip);
            }
        }

        public void StopMusic()
        {
            currentState = MusicState.None; // Останавливает авто-переключение

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopMusic();
            }
        }

        public void PlayMenuTheme()
        {
            isGameplayMusicActive = false;
            currentState = MusicState.Menu;
            
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

        public void PlayThemeWithResume(AudioClip newTheme)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.musicSource != null)
            {
                _savedTrackClip = AudioManager.Instance.musicSource.clip;
                _savedTrackTime = AudioManager.Instance.musicSource.time;
            }
            PlayTrack(newTheme);
        }

        public void ResumeSavedTrack()
        {
            if (_savedTrackClip != null)
            {
                // Восстанавливаем время
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.musicSource.time = _savedTrackTime;
                }
                
                // Восстанавливаем state в зависимости от сцены
                string currentScene = SceneManager.GetActiveScene().name;
                if (currentScene == "GameScene")
                {
                    currentState = MusicState.Office;
                }
                else
                {
                    currentState = MusicState.Menu;
                }
                
                PlayTrack(_savedTrackClip);
            }
            else
            {
                PlayMenuTheme();
            }
        }

        public void OpenArchiveMusic()
        {
            // Запоминаем текущий трек и время
            if (AudioManager.Instance != null && AudioManager.Instance.musicSource != null)
            {
                _savedTrackClip = AudioManager.Instance.musicSource.clip;
                _savedTrackTime = AudioManager.Instance.musicSource.time;
            }
            
            currentState = MusicState.Archive;
            
            // Запускаем случайный трек из архива
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

        public void CloseArchiveMusic()
        {
            ResumeSavedTrack();
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
            PlayCorrectTrackForCurrentTime();
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
            if (RadioMusicNotification.Instance != null && isGameplayMusicActive)
            {
                RadioMusicNotification.Instance.ShowMusicNotification(trackName);
            }
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

        public void PlayClownMusic()
        {
            if (clownMusicTrack == null) return;
            isClownMusicForced = true;
            PlayTrack(clownMusicTrack);
        }

        public void StopClownMusic()
        {
            isClownMusicForced = false;
            // Возвращаемся к обычной музыке
            if (isGameplayMusicActive)
            {
                if (TimeManager.Instance != null && TimeManager.Instance.IsNight())
                    PlayTrack(nightTrack);
                else
                    PlayRandomDayTrack();
            }
            else
            {
                PlayDirectorsOfficeTheme();
            }
        }
        
        private static bool IsNightTime() => TimeManager.Instance != null &&
                                             TimeManager.Instance.GetCurrentPeriodType().IsNight();
    }
}