// Файл: Assets/Scripts/Managers/MainUIManager.cs
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
        [Header("Настройки переходов")]
        public float splashScreenDwellTime = 2.0f;
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";
        public bool isTransitioning { get; private set; } = false;
        private int _pauseCount = 0;
        public int pauseCount => _pauseCount; // For debugging
	
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null); 
                DontDestroyOnLoad(gameObject); 
                Debug.Log($"<color=green>[MainUIManager]</color> Awake: Я стал Singleton.");
            }
            else if (Instance != this)
            {
                Destroy(gameObject); 
            }
        }

        private void Start()
        {
            // Сбрасываем флаг при старте любой сцены (фикс залипания кнопок меню)
            isTransitioning = false; 
        }
	
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) 
            {
                StartOfDayPanel deskPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
                bool isDirectorDeskOpen = deskPanel != null && deskPanel.gameObject.activeInHierarchy;
            
                if (!isDirectorDeskOpen)
                {
                    bool isPaused = _pauseCount > 0;
                    ShowPausePanel(!isPaused);
                }
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
            else
            {
                Debug.LogError("[MainUIManager] Не удалось найти StartOfDayPanel на сцене!");
            }
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            isTransitioning = true;
            Debug.Log($"[MainUIManager] LoadSceneRoutine: Переход к {sceneName}");
        
            if (TransitionManager.Instance != null)
            {
                yield return TransitionManager.Instance.TransitionToScene(sceneName);
            }
            else
            {
                Debug.LogWarning("TransitionManager не найден, сцена загружается без перехода.");
                yield return SceneManager.LoadSceneAsync(sceneName);
            }

            if (sceneName == gameSceneName)
            {
                StartOfDayPanel startOfDayPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
                OrderSelectionUI orderSelectionUI = FindFirstObjectByType<OrderSelectionUI>(FindObjectsInactive.Include);
                DaySplashScreenController daySplashScreenController = FindFirstObjectByType<DaySplashScreenController>(FindObjectsInactive.Include);
                yield return StartCoroutine(UnveilSequence(startOfDayPanel, orderSelectionUI, daySplashScreenController));
            }
            else
            {
                isTransitioning = false;
            }
        }
    
        private IEnumerator UnveilSequence(StartOfDayPanel startOfDayPanel, OrderSelectionUI orderSelectionUI, DaySplashScreenController daySplashScreenController)
        {
            PauseGame(true);

            // --- ИСПРАВЛЕНИЕ: ПРИНУДИТЕЛЬНЫЙ СБРОС ДАННЫХ ---
            
            // 1. Всегда загружаем данные из слота. 
            // Если это "Новая игра", то в слоте УЖЕ лежат чистые данные (мы их записали при клике на кнопку),
            // и LoadGame корректно сбросит кошелек, календарь и архивы.
            bool loadSuccess = SaveLoadManager.Instance.LoadGame(SaveLoadManager.Instance.GetCurrentSlot());
            
            if (!loadSuccess && SaveLoadManager.Instance.isNewGame)
            {
                // Если вдруг файл не создался (маловероятно), сбрасываем вручную
                Debug.LogError("[MainUIManager] Файл сохранения не найден для новой игры! Сбрасываем вручную.");
                PlayerWallet.Instance.ResetState(); // 100$
                CalendarManager.Instance.StartNewGame(); // День 1
                ArchiveManager.Instance.ResetState();
            }

            // 2. Дополнительный сброс для систем, которые не сохраняются в JSON
            if (SaveLoadManager.Instance.isNewGame)
            {
                Debug.Log("[MainUIManager] Новая игра: Дополнительный сброс менеджеров.");
                DirectorManager.Instance.ResetState(); 
                HiringManager.Instance.ResetState(); // Очищаем список сотрудников
                OrderManager.Instance.ResetState();  // Очищаем приказы
                // StoryStateManager сбрасывается внутри SaveLoadManager.SaveNewGame, но для надежности:
                StoryStateManager.Instance?.ResetState(); 
            }
            // ------------------------------------------------

            DirectorManager.Instance.PrepareDay();

            DirectorAvatarController directorController = FindFirstObjectByType<DirectorAvatarController>();
            if (directorController != null && directorController.directorChairPoint != null)
            {
                directorController.TeleportTo(directorController.directorChairPoint.position);
                directorController.ForceSetAtDeskState(true);
            }
        
            if (daySplashScreenController != null)
            {
                daySplashScreenController.gameObject.SetActive(true);
                daySplashScreenController.Setup(CalendarManager.Instance.CurrentDay); 
                daySplashScreenController.GetComponent<CanvasGroup>().alpha = 1f;
            }

            if (orderSelectionUI != null)
            {
                orderSelectionUI.gameObject.SetActive(true);
                orderSelectionUI.Setup();
                var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
                orderCG.alpha = 1f;
                orderCG.interactable = false;
                orderCG.blocksRaycasts = false;
            }

            yield return new WaitForSecondsRealtime(splashScreenDwellTime);

            if (daySplashScreenController != null) { yield return daySplashScreenController.Fade(false); }

            if (orderSelectionUI != null) {
                var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
                orderCG.interactable = true;
                orderCG.blocksRaycasts = true;
            }

            isTransitioning = false;
        }

        public void OnSaveSlotClicked(int slotIndex)
        {
            if (isTransitioning) return;
            
            Debug.Log($"[MainUIManager] Загрузка слота {slotIndex}");
            SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
            SaveLoadManager.Instance.isNewGame = false;
            StartCoroutine(LoadSceneRoutine(gameSceneName));
        }

        public void OnNewGameClicked(int slotIndex)
        {
            if (isTransitioning) return;

            Debug.Log($"[MainUIManager] Создание НОВОЙ игры в слоте {slotIndex}");
            SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
            SaveLoadManager.Instance.isNewGame = true;
            
            // Создаем чистый сейв
            SaveData newGameData = new SaveData { day = 1, money = 1000 };
            SaveLoadManager.Instance.SaveNewGame(slotIndex, newGameData);
            
            StartCoroutine(LoadSceneRoutine(gameSceneName));
        }

        public void StartOrResumeGameplay()
        {
            if (isTransitioning) return;
            StartCoroutine(StartGameplaySequence());
        }

        private IEnumerator StartGameplaySequence()
        {
            isTransitioning = true;
            
            if (pausePanel != null) pausePanel.SetActive(false);

            StartOfDayPanel sodp = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include); 
            if (sodp != null)
            {
                yield return StartCoroutine(sodp.Fade(false, false));
            }

            _pauseCount = 0;
            ResumeGame();
            HiringManager.Instance?.ActivateAllScheduledStaff();
        
            if (MusicPlayer.Instance != null)
            {
                MusicPlayer.Instance.StartGameplayMusic();
                // --- ДОБАВЛЕНО: Принудительно обновляем трек, если уже день ---
                MusicPlayer.Instance.RequestNextTrack(); 
            }

            // --- ДОБАВЛЕНО: Пинаем WaveManager ---
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.ForceCheckMorningEvents();
            }
        
            isTransitioning = false;
        }

        public void ShowPausePanel(bool show)
        {
            if (isTransitioning) return;

            if (show)
            {
                PauseGame(false); 
                MusicPlayer.Instance?.PauseGameplayMusicForManualPause();
                if (pausePanel != null) pausePanel.SetActive(true);
            }
            else 
            { 
                ResumeGame();
                MusicPlayer.Instance?.ResumeGameplayMusicFromManualPause();
                if (pausePanel != null) pausePanel.SetActive(false);
            }
        }

        public void GoToMainMenu()
        {
            if (isTransitioning) return;
            ResumeGame();
        
            if (SceneManager.GetActiveScene().name == gameSceneName && 
                SaveLoadManager.Instance != null && 
                !SaveLoadManager.Instance.isNewGame) 
            {
                try
                {
                    SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.GetCurrentSlot());
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MainUIManager] Ошибка автосохранения: {e.Message}");
                }
            }
        
            StartCoroutine(LoadSceneRoutine(mainMenuSceneName));
        }

        public void TriggerNextDayTransition()
        {
            if (isTransitioning) return;
            StartCoroutine(LoadSceneRoutine(gameSceneName));
        }

        public void PauseGame(bool playMusic = true)
        {
            _pauseCount++;
            Debug.Log($"<color=yellow>[Pause] _pauseCount: {_pauseCount} (PauseGame called)</color>");
            if (_pauseCount == 1)
            {
                Time.timeScale = 0f;
                Debug.Log("<color=yellow>[Pause] Time.timeScale = 0f</color>");
                if (playMusic && MusicPlayer.Instance != null) MusicPlayer.Instance.PauseGameplayMusicAndPlayOfficeTheme();
            }
        }

        public void ResumeGame()
        {
            if (_pauseCount > 0) _pauseCount--;
            Debug.Log($"<color=yellow>[Pause] _pauseCount: {_pauseCount} (ResumeGame called)</color>");
            if (_pauseCount == 0)
            {
                Time.timeScale = 1f;
                Debug.Log("<color=yellow>[Pause] Time.timeScale = 1f</color>");
            }
        }

        public void PushPause() => PauseGame(false);
        public void PopPause() => ResumeGame();
    }
}