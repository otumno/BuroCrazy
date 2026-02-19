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

        // --- ЛОГИКА ПАУЗЫ (ИСПРАВЛЕНО: Мгновенная остановка) ---

        public void PauseGame(bool playMusic = true)
        {
            _pauseCount++;
            
            // Если это первая блокировка - останавливаем время СРАЗУ
            if (_pauseCount == 1)
            {
                Time.timeScale = 0f; // Мгновенно! Никаких корутин.
                
                Debug.Log("<color=yellow>[MainUIManager] Game Paused (TimeScale = 0)</color>");
                
                if (playMusic && MusicPlayer.Instance != null) 
                    MusicPlayer.Instance.PauseGameplayMusicAndPlayOfficeTheme();
            }
        }

        public void ResumeGame()
        {
            if (_pauseCount > 0) _pauseCount--;
            
            // Если блокировок больше нет - запускаем время
            if (_pauseCount == 0)
            {
                Time.timeScale = 1f;
                Debug.Log("<color=yellow>[MainUIManager] Game Resumed (TimeScale = 1)</color>");
            }
        }

        public void PushPause() => PauseGame(false);
        public void PopPause() => ResumeGame();

        // --- МЕТОДЫ УПРАВЛЕНИЯ UI ---

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
            
            // Активируем телетайп только сейчас
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

            // Принудительно сбрасываем все паузы перед стартом
            _pauseCount = 0;
            Time.timeScale = 1f; 
            
            if (MusicPlayer.Instance != null)
            {
                MusicPlayer.Instance.OnGameStarted();
                MusicPlayer.Instance.StartGameplayMusic();
                MusicPlayer.Instance.RequestNextTrack(); 
            }

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.ForceCheckMorningEvents();
            }

            isTransitioning = false;
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            isTransitioning = true;
            if (TransitionManager.Instance != null)
                yield return TransitionManager.Instance.TransitionToScene(sceneName);
            else
                yield return SceneManager.LoadSceneAsync(sceneName);

            // Специфичная логика для GameScene
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
        
        private IEnumerator UnveilSequence(StartOfDayPanel _, OrderSelectionUI orderSelectionUI, DaySplashScreenController daySplashScreenController)
        {
            // !!! ВАЖНО !!! Ставим паузу ДО любых действий
            PauseGame(true);

            // Загрузка данных
            bool loadSuccess = SaveLoadManager.Instance.LoadGame(SaveLoadManager.Instance.GetCurrentSlot());
            if (!loadSuccess && SaveLoadManager.Instance.isNewGame)
            {
                PlayerWallet.Instance.ResetState();
                CalendarManager.Instance.StartNewGame();
                ArchiveManager.Instance.ResetState();
            }

            if (SaveLoadManager.Instance.isNewGame)
            {
                DirectorManager.Instance.ResetState(); 
                HiringManager.Instance.ResetState();
                OrderManager.Instance.ResetState();
                StoryStateManager.Instance?.ResetState(); 
            }

            DirectorManager.Instance.PrepareDay();

            // Телепортация директора
            DirectorAvatarController directorController = FindFirstObjectByType<DirectorAvatarController>();
            if (directorController != null && directorController.directorChairPoint != null)
            {
                directorController.TeleportTo(directorController.directorChairPoint.position);
                directorController.ForceSetAtDeskState(true);
            }

            // Настройка UI
            if (orderSelectionUI != null)
            {
                orderSelectionUI.gameObject.SetActive(true);
                orderSelectionUI.Setup();
                // Блокируем взаимодействие пока идет заставка
                var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
                if(orderCG) 
                {
                    orderCG.alpha = 1f;
                    orderCG.interactable = false;
                    orderCG.blocksRaycasts = false;
                }
            }

            // Ждем на заставке (Realtime, т.к. игра на паузе)
            yield return new WaitForSecondsRealtime(splashScreenDwellTime);

            // Убираем сплэш
            if (daySplashScreenController != null)
            {
                yield return daySplashScreenController.Fade(false);
            }

            // Разрешаем выбирать приказы
            if (orderSelectionUI != null) 
            {
                var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
                if(orderCG) 
                {
                    orderCG.interactable = true;
                    orderCG.blocksRaycasts = true;
                }
            }

            isTransitioning = false;
        }
    }
}