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
        private bool _isUserPaused = false;
        
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

        private void Update()
        {
            // Если идет анимация загрузки или смена дня - игнорируем нажатия
            if (isTransitioning) return;


            // Ручная пауза на пробел
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ToggleUserPause();
            }
        }


        public void ToggleUserPause()
        {
            // 1. Проверяем, не сидим ли мы за столом Директора
            var desk = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            if (desk != null)
            {
                var cg = desk.GetComponent<CanvasGroup>();
                if (cg != null && cg.alpha > 0.01f) return; 
            }


            // 2. Игнорируем пробел, если открыто окно (Карта, Бухгалтерия и т.д.)
            if (!_isUserPaused && _pauseCount > 0) return; 


            // 3. Переключаем ручную паузу
            _isUserPaused = !_isUserPaused;


            if (_isUserPaused)
            {
                PushPause();
                if (pausePanel != null) pausePanel.SetActive(true);
                
                // 🎵 Плавно уводим игру в паузу (с сохранением времени трека)
                if (MusicPlayer.Instance != null)
                {
                    MusicPlayer.Instance.PauseGameplayMusicForManualPause(); 
                }
            }
            else
            {
                PopPause();
                if (pausePanel != null) pausePanel.SetActive(false);
                
                // 🎵 Возвращаем игровую музыку ровно с того места, где остановились
                if (MusicPlayer.Instance != null)
                {
                    MusicPlayer.Instance.ResumeGameplayMusicFromManualPause();
                }
            }
        }

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
            
            // Ставим игру на паузу через PushPause()
            PushPause();
            
            // Находим StartOfDayPanel
            var sodp = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            if (sodp != null)
            {
                // Используем UIWindowAnimator если есть, иначе мгновенно включаем
                var animator = sodp.GetComponent<UIWindowAnimator>();
                if (animator != null)
                {
                    sodp.gameObject.SetActive(true);
                    animator.Open();
                }
                else
                {
                    sodp.gameObject.SetActive(true);
                    var cg = sodp.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = 1f;
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    }
                }
            }
            
            // Включаем музыку офиса
            MusicPlayer.Instance?.PlayDirectorsOfficeTheme();
        }

        public void HideDirectorDesk()
        {
            if (isTransitioning) return;
            StartOrResumeGameplay();
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
            
            if (Managers.Teletype.TeletypeManager.Instance != null)
                Managers.Teletype.TeletypeManager.Instance.ActivateSystem();


            isTransitioning = true;
            if (pausePanel != null) pausePanel.SetActive(false);


            // 1. Прячем стол
            var desk = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include); 
            if (desk != null) 
            {
                var anim = desk.GetComponent<UIWindowAnimator>();
                if (anim != null) anim.Close(); 
                else 
                {
                    var cg = desk.GetComponent<CanvasGroup>();
                    if (cg != null) { cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false; }
                }
            }


            // 2. Запускаем корутину, которая сбросит паузу, когда стол исчезнет
            StartCoroutine(HardResetPauseRoutine());


            // 3. Запускаем фоновые системы
            if (MusicPlayer.Instance != null)
            {
                MusicPlayer.Instance.OnGameStarted();
                MusicPlayer.Instance.StartGameplayMusic();
                MusicPlayer.Instance.RequestNextTrack(); 
            }


            if (WaveManager.Instance != null) WaveManager.Instance.ForceCheckMorningEvents();


            isTransitioning = false;
        }


        // НОВЫЙ МЕТОД
        private System.Collections.IEnumerator HardResetPauseRoutine()
        {
            // Ждем 0.3 секунды реального времени (пока аниматор прячет стол)
            yield return new WaitForSecondsRealtime(0.3f);
            
            // Жестко сбрасываем счетчик и запускаем время
            _pauseCount = 0;
            _isUserPaused = false; // Сбрасываем флаг ручной паузы
            Time.timeScale = 1f;
            Debug.Log("<color=green>[MainUIManager] Время запущено, _pauseCount сброшен на 0.</color>");
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

            // 1. Мгновенно показываем стол (он будет лежать на дне)
            var desk = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            if (desk != null)
            {
                desk.gameObject.SetActive(true);
                var anim = desk.GetComponent<UIWindowAnimator>();
                if (anim != null) anim.ShowInstant();
                else 
                { 
                    var cg = desk.GetComponent<CanvasGroup>(); 
                    if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; } 
                }
            }


            // 2. Мгновенно показываем Приказы и выносим их ПОВЕРХ стола
            if (orderSelectionUI != null)
            {
                orderSelectionUI.gameObject.SetActive(true);
                // orderSelectionUI.transform.SetAsLastSibling(); // УДАЛЕНО: UIWindowAnimator сам управляет порядком
                orderSelectionUI.Setup();
                var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
                if(orderCG) { orderCG.alpha = 1f; orderCG.interactable = false; orderCG.blocksRaycasts = false; }
            }


            // 3. Ждем скипа заставки (сплэш-скрина)
            float timer = 0f;
            while (timer < splashScreenDwellTime)
            {
                timer += Time.unscaledDeltaTime;
                if (Input.GetMouseButtonDown(0)) break;
                yield return null;
            }


            // 4. Убираем сплэш
            if (daySplashScreenController != null) yield return daySplashScreenController.Fade(false);


            // 5. Разрешаем кликать по приказам
            if (orderSelectionUI != null) 
            {
                var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
                if(orderCG) { orderCG.interactable = true; orderCG.blocksRaycasts = true; }
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