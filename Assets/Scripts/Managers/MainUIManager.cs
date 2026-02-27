// Assets/Scripts/Managers/MainUIManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Data.Creation;
using Enums;

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
        private BootFadeEffect _currentSceneFade;
        private bool _isTransitioning = false;
        
        public void RegisterSceneFade(BootFadeEffect fade) => _currentSceneFade = fade;
        public bool isTransitioning { get; private set; }

        private static DirectorInitialState _pendingDirectorInitialState;
        private static string _pendingDirectorCreationCode;

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
            StartCoroutine(DirectorDeskTransitionRoutine(true));
        }

        public void HideDirectorDesk()
        {
            if (isTransitioning) return;
            StartCoroutine(DirectorDeskTransitionRoutine(false));
        }

        private IEnumerator DirectorDeskTransitionRoutine(bool open)
        {
            if (_isTransitioning) yield break;
            _isTransitioning = true;

            if (_currentSceneFade != null)
                yield return StartCoroutine(_currentSceneFade.PlayCloseRoutine());

            if (open) 
            {
                PauseGame(true);
                if (StartOfDayPanel.Instance != null) StartOfDayPanel.Instance.gameObject.SetActive(true);
                MusicPlayer.Instance.PlayDirectorsOfficeTheme();
            } 
            else 
            {
                if (StartOfDayPanel.Instance != null) StartOfDayPanel.Instance.gameObject.SetActive(false);
                MusicPlayer.Instance.StartGameplayMusic();
                ResumeGame();
                Debug.Log("<color=green>[MainUIManager]</color> Игра запущена!");
            }

            if (_currentSceneFade != null)
            {
                StartCoroutine(_currentSceneFade.PlayOpenRoutine());
            }

            _isTransitioning = false;
        }

        private void ExecuteDeskSwitch(bool open)
        {
            if (open) {
                PauseGame(true);
                if (StartOfDayPanel.Instance != null) 
                    StartOfDayPanel.Instance.gameObject.SetActive(true);
                MusicPlayer.Instance.PauseGameplayMusicAndPlayOfficeTheme();
            } else {
                if (StartOfDayPanel.Instance != null) 
                    StartOfDayPanel.Instance.gameObject.SetActive(false);
                
                MusicPlayer.Instance.StartGameplayMusic();
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

        public void StartNewGameWithDirectorCreation(int slotIndex, DirectorInitialState initialState, string creationCode)
        {
            if (isTransitioning) return;
            SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
            SaveLoadManager.Instance.isNewGame = true;
            
            _pendingDirectorInitialState = initialState;
            _pendingDirectorCreationCode = creationCode;
            
            SaveData newGameData = new SaveData 
            { 
                day = 1, 
                money = initialState.startingMoney,
                directorCreationCode = creationCode
            };
            
            SaveLoadManager.Instance.SaveNewGame(slotIndex, newGameData);
            StartCoroutine(LoadSceneRoutine(gameSceneName));
        }

        public static DirectorInitialState GetPendingDirectorInitialState()
        {
            return _pendingDirectorInitialState;
        }

        public static string GetPendingDirectorCreationCode()
        {
            return _pendingDirectorCreationCode;
        }

        public static void ClearPendingDirectorData()
        {
            _pendingDirectorInitialState = null;
            _pendingDirectorCreationCode = null;
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

                ApplyDirectorCreationSettings();
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

        private void ApplyDirectorCreationSettings()
        {
            var initialState = GetPendingDirectorInitialState();
            if (initialState == null)
            {
                Debug.Log("[MainUIManager] Нет данных о создании директора, используем настройки по умолчанию.");
                return;
            }

            Debug.Log($"[MainUIManager] Применяем настройки создания директора: {GetPendingDirectorCreationCode()}");

            if (PlayerWallet.Instance != null)
            {
                PlayerWallet.Instance.ResetState(initialState.startingMoney);
            }

            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.AddInfluence(initialState.startingInfluence);

                foreach (var regionID in initialState.unlockedRegions)
                {
                    ProgressionManager.Instance.UnlockRegion(regionID);
                }
            }

            if (DirectorManager.Instance != null)
            {
                DirectorManager.Instance.SetStrikes(initialState.startingStrikes);
            }

            if (initialState.startingStaff.Count > 0 && HiringManager.Instance != null)
            {
                foreach (var staffData in initialState.startingStaff)
                {
                    HiringManager.Instance.SpawnStaff(staffData.role, staffData.customName, staffData.skillLevel);
                }
            }

            if (DirectorAvatarController.Instance != null && !string.IsNullOrEmpty(initialState.spriteCollectionID))
            {
                ApplyDirectorAppearance(initialState.spriteCollectionID, initialState.startingGender);
            }

            ClearPendingDirectorData();
        }

        private void ApplyDirectorAppearance(string spriteCollectionID, Enums.Gender gender)
        {
            Debug.Log($"[MainUIManager] Применяем внешний вид директора: {spriteCollectionID}, Gender: {gender}");
        }
    }
}