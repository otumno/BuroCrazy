// Assets/Scripts/Managers/MainUIManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers
{
    public class MainUIManager : MonoBehaviour
    {
        public static MainUIManager Instance { get; set; }
        
        [Header("Ссылки на компоненты UI")]
        public AudioSource uiAudioSource;
        [SerializeField] private GameObject pausePanel;
        
        [Header("Настройки")]
        public float splashScreenDwellTime = 2.0f;
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";
        
        private int _pauseCount = 0;
        public int pauseCount => _pauseCount;
        public bool isTransitioning { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null); 
                DontDestroyOnLoad(gameObject); 
            }
            else if (Instance != this) { Destroy(gameObject); }
        }

        // --- ЛОГИКА ПАУЗЫ С ЗАЩИТОЙ РЕДАКТОРА ---

        public void PauseGame(bool playMusic = true)
        {
            _pauseCount++;
            if (_pauseCount == 1)
            {
                StartCoroutine(SafePauseRoutine(playMusic));
            }
        }

        private IEnumerator SafePauseRoutine(bool playMusic)
        {
            yield return null; // Пропускаем один кадр инициализации для стабильности Play Mode
            Time.timeScale = 0f;
            if (playMusic && MusicPlayer.Instance != null) 
                MusicPlayer.Instance.PauseGameplayMusicAndPlayOfficeTheme();
            
            Debug.Log("<color=yellow>[MainUIManager] Safe Pause Applied</color>");
        }

        public void ResumeGame()
        {
            if (_pauseCount > 0) _pauseCount--;
            if (_pauseCount == 0)
            {
                StopAllCoroutines(); 
                Time.timeScale = 1f;
            }
        }

        public void PushPause() => PauseGame(false);
        public void PopPause() => ResumeGame();

        // --- ВОССТАНОВЛЕННЫЕ МЕТОДЫ ДЛЯ СЦЕНЫ И СОХРАНЕНИЙ ---

        public void ShowPausePanel(bool show)
        {
            if (isTransitioning) return;
            if (show)
            {
                PauseGame(false);
                if (pausePanel != null) pausePanel.SetActive(true);
            }
            else
            {
                ResumeGame();
                if (pausePanel != null) pausePanel.SetActive(false);
            }
        }

        public void ShowDirectorDesk()
        {
            if (isTransitioning) return;
            StartOfDayPanel deskPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            if (deskPanel != null)
            {
                PauseGame(true);
                StartCoroutine(deskPanel.Fade(true, true));
            }
        }

        public void OnSaveSlotClicked(int slotIndex)
        {
            if (isTransitioning) return;
            SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
            SaveLoadManager.Instance.isNewGame = false;
            StartCoroutine(LoadSceneRoutine(gameSceneName));
        }

        public void OnNewGameClicked(int slotIndex)
        {
            if (isTransitioning) return;
            SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
            SaveLoadManager.Instance.isNewGame = true;
            SaveData newGameData = new SaveData { day = 1, money = 1000 };
            SaveLoadManager.Instance.SaveNewGame(slotIndex, newGameData);
            StartCoroutine(LoadSceneRoutine(gameSceneName));
        }

        public void GoToMainMenu()
        {
            if (isTransitioning) return;
            ResumeGame();
            if (SceneManager.GetActiveScene().name == gameSceneName && !SaveLoadManager.Instance.isNewGame)
            {
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.GetCurrentSlot());
            }
            StartCoroutine(LoadSceneRoutine(mainMenuSceneName));
        }

        public void StartOrResumeGameplay()
        {
            if (isTransitioning) return;
            if (Managers.Teletype.TeletypeManager.Instance != null)
                Managers.Teletype.TeletypeManager.Instance.ActivateSystem();

            StartCoroutine(StartGameplaySequence());
        }

        private IEnumerator StartGameplaySequence()
        {
            isTransitioning = true;
            if (pausePanel != null) pausePanel.SetActive(false);

            StartOfDayPanel sodp = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include); 
            if (sodp != null) yield return StartCoroutine(sodp.Fade(false, false));

            _pauseCount = 0;
            ResumeGame();
            isTransitioning = false;
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            isTransitioning = true;
            if (TransitionManager.Instance != null)
                yield return TransitionManager.Instance.TransitionToScene(sceneName);
            else
                yield return SceneManager.LoadSceneAsync(sceneName);
            
            // Логика инициализации после загрузки GameScene может быть добавлена здесь
            isTransitioning = false;
        }
    }
}