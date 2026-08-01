// Assets/Scripts/Managers/MainUIManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Data.Creation;
using Enums;
using Gameplay;
using CinematicSystem;

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
            SaveLoadManager.Instance.pendingNewGameSetup = true;
            SaveData newGameData = new SaveData { day = 1, money = 1000 };
            SaveLoadManager.Instance.SaveNewGame(slotIndex, newGameData);
            StartCoroutine(LoadSceneRoutine(gameSceneName));
        }

        public void StartNewGameWithDirectorCreation(int slotIndex, DirectorInitialState initialState, string creationCode)
        {
            if (isTransitioning) return;
            SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
            SaveLoadManager.Instance.isNewGame = true;
            SaveLoadManager.Instance.pendingNewGameSetup = true;

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

            // Сначала гасим всё, что могло остаться от прерванного геймплея (диалог/катсцена).
            // Делаем это ДО загрузки сцены, пока мы ещё в GameScene и объекты живы.
            CleanupInterruptedGameplay();

            if (SceneManager.GetActiveScene().name == gameSceneName && !SaveLoadManager.Instance.isNewGame)
            {
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.GetCurrentSlot());
            }
            StartCoroutine(LoadSceneRoutine(mainMenuSceneName));
        }

        /// <summary>
        /// Сбрасывает состояние прерванного геймплея перед выходом в главное меню.
        /// Если выйти в меню посреди туториала, диалог/катсцена не завершаются штатно, а их
        /// UI живёт на персистентном [SYSTEMS] (DontDestroyOnLoad) — панель диалога с
        /// sortingOrder 2001 остаётся поверх меню (чёрный экран), а корутины и блокировка
        /// ввода/паузы утекают в следующую сессию.
        /// </summary>
        private void CleanupInterruptedGameplay()
        {
            // 1. Принудительно закрываем активный диалог (прячет персистентную панель и фон).
            DialogueUIManager.Instance?.ForceCloseImmediate();

            // 2. Останавливаем катсцену и снимаем блокировку ввода/катсценный курсор.
            //    Stop() снимает блокировку сам, но дублируем на случай, если inputLocked уже сброшен.
            var cinematic = FindFirstObjectByType<CinematicPlayer>();
            if (cinematic != null) cinematic.Stop();

            var input = FindFirstObjectByType<PlayerInputController>();
            if (input != null) input.IsCutscenePlaying = false; // сеттер сам вернёт обычный курсор

            // 3. Жёстко сбрасываем паузу: мы уходим в меню, начинаем с чистого состояния.
            //    Не используем ResumeGame(), т.к. счётчик мог быть разбалансирован прерванным диалогом.
            _pauseCount = 0;
            _isUserPaused = false;
            Time.timeScale = 1f;
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        public void StartOrResumeGameplay()
        {
            Debug.Log($"[StartOrResume] Вызван, timeScale = {Time.timeScale}, isTransitioning = {isTransitioning}");
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
            
            // После закрытия панели стола директора устанавливаем камеру на ВТОРУЮ позицию
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var cameraToggle = mainCamera.GetComponent<Managers.CameraToggle>();
                if (cameraToggle != null)
                {
                    cameraToggle.SetToSecondPosition();
                    Debug.Log("[MainUIManager] Камера установлена на вторую позицию после закрытия панели");
                }
            }
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
            
            // Подготовка данных - проверяем туториал СРАЗУ
            int currentSlot = SaveLoadManager.Instance.GetCurrentSlot();
            var saveData = SaveLoadManager.Instance.GetDataForSlot(currentSlot);
            bool isFirstDay = CalendarManager.Instance.CurrentDay == 1;
            bool isTutorialNeeded = isFirstDay && !saveData.firstDayTutorialCompleted;
            Debug.Log($"[Tutorial] day={CalendarManager.Instance.CurrentDay}, flag={saveData.firstDayTutorialCompleted}");
            
            if (!loadSuccess && SaveLoadManager.Instance.isNewGame)
            {
                PlayerWallet.Instance.ResetState();
                CalendarManager.Instance.StartNewGame();
                ArchiveManager.Instance.ResetState();
            }

            // Проверяем pendingNewGameSetup, а не isNewGame: последний уже сброшен в false
            // (SaveNewGame при старте новой игры и LoadGame выше), поэтому по isNewGame этот блок
            // для новой игры не выполнялся бы никогда.
            if (SaveLoadManager.Instance.pendingNewGameSetup)
            {
                DirectorManager.Instance.ResetState();
                HiringManager.Instance.ResetState();
                OrderManager.Instance.ResetState();
                StoryStateManager.Instance?.ResetState();

                // [НОВОЕ] Инициализировать ArcManager для новой игры (выбирает арки)
                if (StorySystem.ArcManager.Instance != null)
                {
                    StorySystem.ArcManager.Instance.Initialize();
                }

                ApplyDirectorCreationSettings();

                // Потребляем флаг: инициализация новой игры нужна один раз за загрузку GameScene.
                SaveLoadManager.Instance.pendingNewGameSetup = false;
            }

            DirectorManager.Instance.PrepareDay();

            // Подготавливаем ссылки на панели (для возможного использования)
            var desk = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);

            // 1. Показываем сплэш-скрин (он нужен ВСЕГДА, независимо от туториала)
            if (daySplashScreenController != null)
            {
                daySplashScreenController.Setup(CalendarManager.Instance.CurrentDay);
                daySplashScreenController.gameObject.SetActive(true);
            }

            // 2. Ждем скипа заставки (сплэш-скрина)
            float timer = 0f;
            while (timer < splashScreenDwellTime)
            {
                timer += Time.unscaledDeltaTime;
                if (Input.GetMouseButtonDown(0)) break;
                yield return null;
            }

            // 3. Убираем сплэш
            if (daySplashScreenController != null) yield return daySplashScreenController.Fade(false);

            // 4. Снимаем паузу после сплеш-скрина
            ResumeGame();
            Debug.Log($"[Unveil] После сплеша, время = {Time.timeScale}");

            // 5. Проверяем туториал ПОСЛЕ скрытия сплэша
            if (isTutorialNeeded)
            {
                Debug.Log("[MainUIManager] Туториал активен, запускаем FirstDayTutorial");
                
                // НЕ создаем панели - туториал сам покажет свой UI
                isTransitioning = false;
                yield return StartCoroutine(StartTutorialRoutine());
                yield break;
            }

            // 6. ОБЫЧНЫЙ СТАРТ - показываем стол и приказы
            // Телепортируем директора к столу
            DirectorAvatarController directorController = FindFirstObjectByType<DirectorAvatarController>();
            if (directorController != null && directorController.directorChairPoint != null)
            {
                directorController.TeleportTo(directorController.directorChairPoint.position);
                directorController.ForceSetAtDeskState(true);
            }

            // Показываем стол
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

            // Показываем Приказы
            if (orderSelectionUI != null)
            {
                orderSelectionUI.gameObject.SetActive(true);
                orderSelectionUI.Setup();
                var orderCG = orderSelectionUI.GetComponent<CanvasGroup>();
                if(orderCG) { orderCG.interactable = true; orderCG.blocksRaycasts = true; }
            }

            isTransitioning = false;
        }

        private IEnumerator StartTutorialRoutine()
        {
            Debug.Log("[MainUIManager] StartTutorialRoutine: Запуск туториала первого дня через CinematicGraph");
            
            // Загружаем граф туториала
            var tutorialGraph = CinematicGraphLibrary.LoadGraph("Tutorial_Day1");
            if (tutorialGraph == null)
            {
                Debug.LogError("[MainUIManager] Tutorial_Day1.graph не найден!");
                isTransitioning = false;
                yield break;
            }
            
            // Находим или создаём CinematicPlayer
            var cinematicPlayer = FindObjectOfType<CinematicPlayer>();
            if (cinematicPlayer == null)
            {
                var playerObject = new GameObject("CinematicPlayer");
                cinematicPlayer = playerObject.AddComponent<CinematicPlayer>();
            }
            
            // Подписываемся на завершение
            bool graphFinished = false;
            System.Action onFinishedHandler = null;
            onFinishedHandler = () =>
            {
                graphFinished = true;
                // Устанавливаем флаг завершения туториала
                var slotIndex = SaveLoadManager.Instance?.GetCurrentSlot() ?? 0;
                var saveData = SaveLoadManager.Instance?.GetDataForSlot(slotIndex);
                if (saveData != null)
                {
                    saveData.firstDayTutorialCompleted = true;
                    // Сохраняем обратно через WriteSaveDataToFile (нужно добавить публичный метод)
                    // Временное решение: используем PlayerPrefs для флага
                    PlayerPrefs.SetInt("FirstDayTutorialCompleted", 1);
                    Debug.Log("[MainUIManager] firstDayTutorialCompleted = true сохранено");
                }
                cinematicPlayer.OnFinished -= onFinishedHandler;
            };
            cinematicPlayer.OnFinished += onFinishedHandler;
            
            // Запускаем граф (CinematicSystem.ExecutionMode.FullControl блокирует управление)
            cinematicPlayer.Play(tutorialGraph, CinematicSystem.ExecutionMode.FullControl);
            
            // Ждём завершения графа. Дополнительно выходим, если плеер остановлен или уничтожен
            // (например, игрок вышел в меню посреди туториала) — иначе корутина висела бы вечно
            // на персистентном MainUIManager в ожидании graphFinished, который уже не придёт.
            // На штатном завершении Finish() выставляет IsPlaying=false и graphFinished=true в
            // одном кадре, поэтому проверка while(!graphFinished) отработает раньше этой ветки.
            while (!graphFinished)
            {
                if (cinematicPlayer == null || !cinematicPlayer.IsPlaying)
                {
                    Debug.Log("[MainUIManager] Туториал прерван (катсцена остановлена) — выходим из ожидания");
                    yield break; // управление сценой/паузой берёт на себя GoToMainMenu
                }
                yield return null;
            }

            Debug.Log("[MainUIManager] Туториал первого дня завершен (через CinematicGraph)");
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

            // Применяем внешность всегда: пол задаётся книгой (SetGender), а spriteCollectionID
            // ни один выбор пока не устанавливает — поэтому по нему гейтить вызов нельзя.
            ApplyDirectorAppearance(initialState.spriteCollectionID, initialState.startingGender);

            ClearPendingDirectorData();
        }

        private void ApplyDirectorAppearance(string spriteCollectionID, Enums.Gender gender)
        {
            var director = DirectorAvatarController.Instance;
            if (director == null) return;

            EmotionSpriteCollection collection = null;
            if (!string.IsNullOrEmpty(spriteCollectionID))
            {
                collection = Resources.Load<EmotionSpriteCollection>($"DirectorSprites/{spriteCollectionID}");
                if (collection == null)
                {
                    Debug.LogWarning($"[MainUIManager] Коллекция спрайтов '{spriteCollectionID}' не найдена в Resources/DirectorSprites.");
                }
            }

            director.ApplyAppearance(gender, collection);
        }
    }
}