// Файл: Assets/Scripts/UI/Tutorial/TutorialMascot.cs (ФИНАЛЬНАЯ ВЕРСИЯ v6)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(AudioSource))]
public class TutorialMascot : MonoBehaviour
{
    public static TutorialMascot Instance { get; private set; }

    [Header("UI (внутренние ссылки префаба)")]
    [SerializeField] private Image folderBackImage;
    [SerializeField] private Image folderFrontImage;
    [SerializeField] private Image pointerHandImage;
    [SerializeField] private GameObject textBubbleObject;
    [SerializeField] private RectTransform textSheetRect;
    [SerializeField] private TextMeshProUGUI helpText;
    [SerializeField] private Button nextButton; // Невидимая кнопка на всю папку
    [SerializeField] private Button closeButton;

    [Header("Звуки")]
    [SerializeField] private AudioClip textBeepSound;
    [SerializeField] private AudioClip textFinalBeepSound;
    [SerializeField] private AudioClip mascotClickSound; // Звук нажатия на "Next" (саму папку)
    [SerializeField] private AudioClip closeClickSound; // Звук нажатия на "Close"

    [Header("Настройки Анимации")]
    [SerializeField] private float fadeDuration = 0.2f; // Обычная скорость
    [SerializeField] private float ceremonialFadeDuration = 1.5f; // Медленная скорость
    [SerializeField] private float hoverAmplitude = 5f;
    [SerializeField] private float hoverSpeed = 2f;
    [SerializeField] private float sheetStepDelay = 0.02f;

    // Настройки, загружаемые из TutorialScreenConfig
    private float currentInitialHintDelay = 3.0f;
    private float currentIdleMessageDelay = 10.0f;
    private float currentFirstEverDelay = 2.0f;
    private float currentNextHintDelay = 5.0f;
    private float currentSceneLoadDelay = 1.0f; 

    // Внутренние компоненты
    private AudioSource audioSource;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    // Состояние
    private TutorialScreenConfig currentConfig;
    private TutorialContextGroup activeContextGroup;
    private Dictionary<string, List<string>> visitedSpotIDs = new Dictionary<string, List<string>>();
    private bool isHidden = true;
    private Vector2 baseHoverPosition;
    private Coroutine tutorialCoroutine;
    private Coroutine soundCoroutine;
    private Coroutine sheetAnimationCoroutine;
    private Coroutine initialHintDelayCoroutine;
    private Coroutine nextHintDelayCoroutine;
    
    private bool isSceneLoadLogicRunning = false; 

    // --- <<< ИЗМЕНЕНИЕ 1 (Б): Добавляем "защитный флаг" >>> ---
    private bool isTutorialResetting = false; 

    private string currentScreenID = "";
    
    private bool isFirstEverAppearance = true; 
    private bool isCeremonialAppearance = false; 
    private bool isFirstAppearanceThisSession = true;

    private RectTransform lastUsedIdleSpot = null;
    private int lastUsedGreetingIndex = -1;
    private int lastUsedTipIndex = -1;

    #region Инициализация и Сцены

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
            if (transform.parent != null)
            {
                transform.parent.SetParent(null);
                DontDestroyOnLoad(transform.parent.gameObject);
                Debug.Log($"<color=green>[TutorialMascot]</color> Awake: Я стал Singleton. Мой родитель '{transform.parent.name}' сделан бессмертным.");
            }
            else
            {
                DontDestroyOnLoad(gameObject);
                Debug.LogWarning($"<color=yellow>[TutorialMascot]</color> Awake: Я стал Singleton, НО У МЕНЯ НЕТ РОДИТЕЛЯ (Canvas). Делаю бессмертным себя. UI может не работать!");
            }
            
            SceneManager.sceneLoaded += OnSceneLoadedStarter;
            Debug.Log("[TutorialMascot] Awake: Подписался на событие SceneManager.sceneLoaded.");

            isFirstEverAppearance = PlayerPrefs.GetInt("Mascot_FirstEverAppearance", 0) == 0;
            Debug.Log($"[TutorialMascot] Awake: Загружено isFirstEverAppearance = {isFirstEverAppearance}");
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[TutorialMascot] Awake: Найден дубликат (Instance: {Instance.gameObject.name}, Я: {gameObject.name}). Уничтожаю *своего родителя* (этот Canvas).");
            
            if (transform.parent != null)
            {
                Destroy(transform.parent.gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            return;
        }

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        audioSource = GetComponent<AudioSource>();
        audioSource.ignoreListenerPause = true;

        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        isHidden = true;
    }

    void Start()
    {
        nextButton.onClick.AddListener(OnNextButtonClicked);
        closeButton.onClick.AddListener(OnCloseButtonClicked);

        Debug.Log($"[TutorialMascot] Start: (isFirstEverAppearance уже = {isFirstEverAppearance})");
        LoadVisitedState();
    }
    
    void OnSceneLoadedStarter(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[TutorialMascot] OnSceneLoadedStarter: Сцена '{scene.name}' загружена.");
        
        if (isSceneLoadLogicRunning)
        {
            Debug.LogWarning("[TutorialMascot] OnSceneLoadedStarter: Попытка повторного запуска DelayedSceneLoadLogic, пока он уже выполняется. Игнорирую.");
            return;
        }
        
        StartCoroutine(DelayedSceneLoadLogic(scene));
    }

    IEnumerator DelayedSceneLoadLogic(Scene scene)
    {
        isSceneLoadLogicRunning = true; 
        
        // --- <<< ИЗМЕНЕНИЕ (Б): Сбрасываем флаг при загрузке сцены >>> ---
        isTutorialResetting = false; 
        Debug.Log($"<color=cyan>[TutorialMascot] DelayedSceneLoadLogic: Старт. (isSceneLoadLogicRunning = true, isTutorialResetting = false)</color>");

        if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
        if (soundCoroutine != null) StopCoroutine(soundCoroutine);
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        if (initialHintDelayCoroutine != null) StopCoroutine(initialHintDelayCoroutine);
        if (nextHintDelayCoroutine != null) StopCoroutine(nextHintDelayCoroutine);

        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;
        initialHintDelayCoroutine = null;
        nextHintDelayCoroutine = null;

        currentConfig = FindObjectOfType<TutorialScreenConfig>();
        if (currentConfig != null)
        {
            currentScreenID = currentConfig.screenID;
            currentInitialHintDelay = currentConfig.initialHintDelay;
            currentIdleMessageDelay = currentConfig.idleMessageChangeDelay;
            currentSceneLoadDelay = currentConfig.sceneLoadDelay; 
            currentNextHintDelay = currentConfig.nextHintDelay;

            bool useCeremonialDelay = false;
            if (isFirstEverAppearance) 
            {
                currentSceneLoadDelay = currentConfig.firstEverAppearanceDelay;
                useCeremonialDelay = true;
                Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Это ПЕРВЫЙ ЗАПУСК. Установлена задержка: {currentSceneLoadDelay}с.");
            }
            else if (isFirstAppearanceThisSession) 
            {
                currentSceneLoadDelay = currentConfig.firstEverAppearanceDelay; 
                useCeremonialDelay = true;
                Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Это ПЕРВЫЙ ЗАПУСК В СЕССИИ. Установлена задержка: {currentSceneLoadDelay}с.");
            }
            else 
            {
                currentSceneLoadDelay = currentConfig.sceneLoadDelay;
                useCeremonialDelay = false;
                Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Обычная загрузка. Задержка: {currentSceneLoadDelay}с.");
            }
            
            Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Конфиг '{currentScreenID}' найден. Задержка загрузки сцены: {currentSceneLoadDelay}с.");

            if (!visitedSpotIDs.ContainsKey(currentScreenID))
            {
                string key = "Mascot_" + currentScreenID;
                if (PlayerPrefs.HasKey(key))
                {
                    string data = PlayerPrefs.GetString(key);
                    visitedSpotIDs[currentScreenID] = new List<string>(data.Split(';').Where(s => !string.IsNullOrEmpty(s)));
                    Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Загружено {visitedSpotIDs[currentScreenID].Count} посещенных спотов для '{currentScreenID}'.");
                }
                else
                {
                    visitedSpotIDs[currentScreenID] = new List<string>();
                    Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Не найдено сохранений для '{currentScreenID}'. Создан новый список.");
                }
            }

            activeContextGroup = null;
            lastUsedIdleSpot = null; 
            lastUsedGreetingIndex = -1;
            lastUsedTipIndex = -1;
            
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            isHidden = true;
            isCeremonialAppearance = false; 
            Debug.Log("[TutorialMascot] DelayedSceneLoadLogic: Маскот принудительно скрыт (без Fade).");

            Debug.Log($"<color=orange>[TutorialMascot] DelayedSceneLoadLogic: Ожидание {currentSceneLoadDelay}с перед запуском...</color>");
            
            yield return new WaitForSecondsRealtime(currentSceneLoadDelay);
            
            Debug.Log($"<color=green>[TutorialMascot] DelayedSceneLoadLogic: Задержка прошла!</color>");
            
            if (activeContextGroup == null)
            {
                Debug.Log($"<color=cyan>[TutorialMascot] DelayedSceneLoadLogic: Запуск RunIdleLogic. (useCeremonial = {useCeremonialDelay})</color>");
                tutorialCoroutine = StartCoroutine(RunIdleLogic(useCeremonialDelay));
            }
        }
        else
        {
            currentScreenID = "";
            lastUsedIdleSpot = null;
            lastUsedGreetingIndex = -1;
            lastUsedTipIndex = -1;
            
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            isHidden = true;
            isCeremonialAppearance = false; 
            
            Debug.LogWarning("[TutorialMascot] DelayedSceneLoadLogic: TutorialScreenConfig НЕ НАЙДЕН на сцене. Маскот будет неактивен.");
        }
        
        isSceneLoadLogicRunning = false;
        Debug.Log($"<color=cyan>[TutorialMascot] DelayedSceneLoadLogic: Завершено. (isSceneLoadLogicRunning = false)</color>");
    }


    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedStarter;
            Debug.Log("[TutorialMascot] OnDestroy: Отписался от события SceneManager.sceneLoaded.");
        }
    }
    
    #endregion

    #region Главная Логика (Update)

    void Update()
    {
        if (!isHidden)
        {
            rectTransform.anchoredPosition = baseHoverPosition +
                new Vector2(0, Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude);
        }

        // --- <<< ИЗМЕНЕНИЕ 2 (Б): Добавляем проверку на флаг >>> ---
        if (currentConfig == null || isSceneLoadLogicRunning || isTutorialResetting)
        {
            // Блокируем смену контекста, пока идет загрузка сцены ИЛИ сброс туториала
            return;
        }
        // --- <<< КОНЕЦ ИЗМЕНЕНИЯ 2 >>> ---
        
        if (tutorialCoroutine == null)
        {
            return;
        }

        TutorialContextGroup newContext = null;
        if (currentConfig.contextGroups != null)
        {
            newContext = currentConfig.contextGroups.FirstOrDefault(
                g => g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
        }

        if (newContext != activeContextGroup)
        {
            Debug.Log($"<color=yellow>[TutorialMascot] Update: КОНТЕКСТ ИЗМЕНИЛСЯ. Старый: '{activeContextGroup?.contextID ?? "null"}' -> Новый: '{newContext?.contextID ?? "null"}'</color>");
            activeContextGroup = newContext;

            Debug.Log("<color=cyan>[TutorialMascot] Update: СТОП ВСЕХ КОРУТИН из-за смены контекста.</color>");
            StopAllCoroutines();
            tutorialCoroutine = null;
            soundCoroutine = null;
            sheetAnimationCoroutine = null;
            initialHintDelayCoroutine = null;
            nextHintDelayCoroutine = null;
            
            isCeremonialAppearance = false;

            if (activeContextGroup != null && activeContextGroup.muteTutorial)
            {
                Debug.Log("[TutorialMascot] Update: Новый контекст 'muteTutorial = true'. Скрытие...");
                Hide();
            }
            else if (activeContextGroup != null)
            {
                Debug.Log("<color=cyan>[TutorialMascot] Update: Запуск RunTutorialForContext.</color>");
                tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup));
            }
            else
            {
                Debug.Log("<color=cyan>[TutorialMascot] Update: Контекст = null. Запуск RunIdleLogic.</color>");
                tutorialCoroutine = StartCoroutine(RunIdleLogic(false));
            }
        }
    }

    #endregion

    #region Логика Туториала и Idle

    private IEnumerator RunIdleLogic(bool useCeremonial = false)
    {
        Debug.Log($"<color=cyan>[TutorialMascot] RunIdleLogic: Старт. (useCeremonial = {useCeremonial})</color>");
        
        if (isFirstEverAppearance)
        {
            Debug.Log("[TutorialMascot] RunIdleLogic: Первый запуск, установка PlayerPrefs.");
            isFirstEverAppearance = false; 
            PlayerPrefs.SetInt("Mascot_FirstEverAppearance", 1);
            PlayerPrefs.Save();
        }
        isFirstAppearanceThisSession = false;
        Debug.Log("[TutorialMascot] RunIdleLogic: isFirstAppearanceThisSession установлен в False.");

        while (activeContextGroup == null)
        {
            Debug.Log("[TutorialMascot] RunIdleLogic: Итерация цикла (контекст все еще null).");
            var visualContext = currentConfig.contextGroups.FirstOrDefault(g => !g.muteTutorial);
            if (visualContext == null) 
            {
                Debug.LogWarning($"<color=red>[TutorialMascot] RunIdleLogic: На экране '{currentScreenID}' нет ни одного 'TutorialContextGroup' (кроме muteTutorial=true). Маскот будет скрыт.</color>");
                Hide(); 
                yield break; 
            }

            List<RectTransform> idleSpots = GetContextIdleSpots(visualContext); // Используем helper
            if (idleSpots == null || idleSpots.Count == 0)
            {
                Debug.LogWarning($"<color=red>[TutorialMascot] RunIdleLogic: Не найдено точек 'idleSpots' ни в '{visualContext.contextID}', ни в ScreenConfig. Маскот будет скрыт.</color>");
                Hide(); 
                yield break;
            }
            
            List<RectTransform> availableSpots = idleSpots.Where(s => s != lastUsedIdleSpot).ToList();
            if (availableSpots.Count == 0) availableSpots = idleSpots; 
            RectTransform randomIdleSpot = availableSpots[Random.Range(0, availableSpots.Count)];
            lastUsedIdleSpot = randomIdleSpot; 

            bool allSpotsOnScreenVisited = AreAllSpotsOnScreenVisited();
            List<string> messageList;
            bool isUsingGreetings = false;

            if (allSpotsOnScreenVisited)
            {
                Debug.Log("[TutorialMascot] RunIdleLogic: Все подсказки просмотрены. Ищем 'Idle Tips'...");
                messageList = GetContextIdleTips(visualContext); // Новый helper
                isUsingGreetings = false; // Мы показываем "советы"

                if (messageList == null || messageList.Count == 0)
                {
                    Debug.Log("[TutorialMascot] RunIdleLogic: 'Idle Tips' не найдены. Возвращаемся к 'Приветствиям'.");
                    messageList = visualContext.greetingTexts;
                    isUsingGreetings = true; // Мы показываем "приветствия"
                }
            }
            else
            {
                Debug.Log("[TutorialMascot] RunIdleLogic: Есть непросмотренные подсказки. Используем 'Приветствия'.");
                messageList = visualContext.greetingTexts;
                isUsingGreetings = true;
            }

            string message = "Я здесь, если что!";
            if (messageList != null && messageList.Count > 0)
            {
                int newIndex = Random.Range(0, messageList.Count);
                int lastIndex = isUsingGreetings ? lastUsedGreetingIndex : lastUsedTipIndex;
                if (messageList.Count > 1 && newIndex == lastIndex)
                {
                    newIndex = (newIndex + 1) % messageList.Count;
                }
                message = messageList[newIndex];
                if (isUsingGreetings) lastUsedGreetingIndex = newIndex;
                else lastUsedTipIndex = newIndex;
            }
            Debug.Log($"[TutorialMascot] RunIdleLogic: Выбрано сообщение: '{message.Substring(0, Mathf.Min(message.Length, 20))}...'");

            yield return StartCoroutine(TeleportToSpot(
                randomIdleSpot.position,
                message,
                visualContext.greetingEmotion, 
                visualContext.greetingSheetHeight, 
                visualContext.greetingHeightSteps, 
                visualContext.greetingPointerSprite,
                visualContext.greetingPointerRotation,
                visualContext.greetingPointerOffset,
                visualContext.greetingSoundRepetitions,
                useCeremonial // Используем флаг (true только 1 раз)
            ));

            useCeremonial = false; 

            if (!allSpotsOnScreenVisited)
            {
                Debug.Log("<color=orange>[TutorialMascot] RunIdleLogic: Запуск InitialHintDelayTimer (для первой подсказки).</color>");
                if (initialHintDelayCoroutine != null) StopCoroutine(initialHintDelayCoroutine);
                initialHintDelayCoroutine = StartCoroutine(InitialHintDelayTimer());
                yield break; 
            }
            
            Debug.Log($"<color=orange>[TutorialMascot] RunIdleLogic: Ожидание {currentIdleMessageDelay}с до следующего Idle-сообщения.</color>");
            
            yield return new WaitForSecondsRealtime(currentIdleMessageDelay);
            
            if (activeContextGroup == null)
            {
                Debug.Log("[TutorialMascot] RunIdleLogic: Контекст все еще null. Прячемся перед сменой позиции.");
                yield return StartCoroutine(Fade(0f, fadeDuration)); 
            }
        }
        Debug.Log("<color=cyan>[TutorialMascot] RunIdleLogic: Цикл завершен (контекст изменился).</color>");
    }

    private IEnumerator RunTutorialForContext(TutorialContextGroup context)
    {
        Debug.Log($"<color=cyan>[TutorialMascot] RunTutorialForContext: Старт для '{context.contextID}'.</color>");
        
        bool allSpotsInContextVisited = context.helpSpots.All(spot => 
            spot == null || 
            visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );
        Debug.Log($"[TutorialMascot] RunTutorialForContext: allSpotsInContextVisited = {allSpotsInContextVisited}");

        if (allSpotsInContextVisited)
        {
            Debug.Log("<color=cyan>[TutorialMascot] RunTutorialForContext: Все подсказки в этом контексте посещены. Запуск RunIdleLogicForContext.</color>");
            tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(context));
            yield break;
        }

        bool isFirstTimeInContext = !context.helpSpots.Any(spot => 
            spot != null && 
            visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );
        Debug.Log($"[TutorialMascot] RunTutorialForContext: isFirstTimeInContext = {isFirstTimeInContext}");

        if (isFirstTimeInContext)
        {
            List<RectTransform> idleSpots = GetContextIdleSpots(context);
            RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];
            
            string greeting = "Привет!";
            if (context.greetingTexts != null && context.greetingTexts.Count > 0)
            {
                greeting = context.greetingTexts[Random.Range(0, context.greetingTexts.Count)];
            }

            Debug.Log($"[TutorialMascot] RunTutorialForContext: Показ приветствия '{greeting.Substring(0, Mathf.Min(greeting.Length, 20))}'");
            yield return StartCoroutine(TeleportToSpot(
                randomIdleSpot.position,
                greeting,
                context.greetingEmotion, 
                context.greetingSheetHeight, 
                context.greetingHeightSteps, 
                context.greetingPointerSprite,
                context.greetingPointerRotation,
                context.greetingPointerOffset,
                context.greetingSoundRepetitions,
                false // Обычный фейд
            ));

            Debug.Log("<color=orange>[TutorialMascot] RunTutorialForContext: Запуск InitialHintDelayTimer.</color>");
            if (initialHintDelayCoroutine != null) StopCoroutine(initialHintDelayCoroutine);
            initialHintDelayCoroutine = StartCoroutine(InitialHintDelayTimer());
        }
        else
        {
            Debug.Log("[TutorialMascot] RunTutorialForContext: Это не первый вход. Сразу показываем следующую подсказку.");
            ShowNextUnvisitedSpotInContext(activeContextGroup);
        }
    }

    private IEnumerator RunIdleLogicForContext(TutorialContextGroup context)
    {
        Debug.Log($"<color=cyan>[TutorialMascot] RunIdleLogicForContext: Старт для '{context.contextID}'.</color>");
        
        while (activeContextGroup == context)
        {
            Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Итерация цикла для '{context.contextID}'.");
            List<RectTransform> idleSpots = GetContextIdleSpots(context);
            RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];

            bool isUsingGreetings;
            int lastIndex;
            
            List<string> messageList = GetContextIdleTips(context);
            if (messageList != null && messageList.Count > 0)
            {
                isUsingGreetings = false;
                lastIndex = lastUsedTipIndex;
                Debug.Log("[TutorialMascot] RunIdleLogicForContext: Используем 'Context Idle Tips'.");
            }
            else
            {
                messageList = context.greetingTexts;
                isUsingGreetings = true;
                lastIndex = lastUsedGreetingIndex;
                Debug.Log("[TutorialMascot] RunIdleLogicForContext: 'Context Idle Tips' не найдены. Используем 'Greeting Texts'.");
            }

            string message = "Я здесь, если что!";
            if (messageList != null && messageList.Count > 0)
            {
                int newIndex = Random.Range(0, messageList.Count);
                if (messageList.Count > 1 && newIndex == lastIndex)
                {
                   newIndex = (newIndex + 1) % messageList.Count;
                }
                message = messageList[newIndex];
                
                if (isUsingGreetings) lastUsedGreetingIndex = newIndex;
                else lastUsedTipIndex = newIndex;
            }
            Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Выбрано сообщение: '{message.Substring(0, Mathf.Min(message.Length, 20))}'");

            yield return StartCoroutine(TeleportToSpot(
                randomIdleSpot.position,
                message,
                context.greetingEmotion, 
                context.greetingSheetHeight, 
                context.greetingHeightSteps, 
                context.greetingPointerSprite,
                context.greetingPointerRotation,
                context.greetingPointerOffset,
                context.greetingSoundRepetitions,
                false // Обычный фейд
            ));

            Debug.Log($"<color=orange>[TutorialMascot] RunIdleLogicForContext: Ожидание {currentIdleMessageDelay}с до следующего сообщения.</color>");
            
            yield return new WaitForSecondsRealtime(currentIdleMessageDelay);

            if (activeContextGroup == context)
            {
                Debug.Log("[TutorialMascot] RunIdleLogicForContext: Контекст не изменился. Прячемся перед сменой позиции.");
                yield return StartCoroutine(Fade(0f, fadeDuration)); // Обычный фейд
            }
        }
        Debug.Log($"<color=cyan>[TutorialMascot] RunIdleLogicForContext: Цикл завершен (контекст изменился).</color>");
    }

    // Таймер для (Приветствие -> Подсказка 1)
    private IEnumerator InitialHintDelayTimer()
    {
        Debug.Log($"<color=orange>[TutorialMascot] InitialHintDelayTimer: Старт. Ожидание {currentInitialHintDelay}с...</color>");
        
        yield return new WaitForSecondsRealtime(currentInitialHintDelay);

        initialHintDelayCoroutine = null; 
        Debug.Log("<color=orange>[TutorialMascot] InitialHintDelayTimer: Время вышло.</color>");

        if (activeContextGroup != null) 
        {
            Debug.Log("[TutorialMascot] InitialHintDelayTimer: Вызов ShowNextUnvisitedSpotInContext.");
            ShowNextUnvisitedSpotInContext(activeContextGroup);
        }
        else 
        {
            Debug.Log("[TutorialMascot] InitialHintDelayTimer: Вызов ShowNextUnvisitedSpotOnScreen.");
            ShowNextUnvisitedSpotOnScreen();
        }
    }
    
    // Таймер для (Подсказка N -> Подсказка N+1)
    private IEnumerator NextHintDelayTimer()
    {
        Debug.Log($"<color=orange>[TutorialMascot] NextHintDelayTimer: Старт. Ожидание {currentNextHintDelay}с...</color>");
        
        yield return new WaitForSecondsRealtime(currentNextHintDelay);
        
        nextHintDelayCoroutine = null;
        Debug.Log("<color=orange>[TutorialMascot] NextHintDelayTimer: Время вышло.</color>");
        
        if (!isHidden)
        {
            Debug.Log("[TutorialMascot] NextHintDelayTimer: Маскот видим. Вызов ShowNextUnvisitedSpotOnScreen.");
            ShowNextUnvisitedSpotOnScreen();
        }
        else
        {
            Debug.Log("[TutorialMascot] NextHintDelayTimer: Маскот скрыт. Ничего не делаем.");
        }
    }

    private void ShowNextUnvisitedSpotInContext(TutorialContextGroup context)
    {
        Debug.Log($"[TutorialMascot] ShowNextUnvisitedSpotInContext: Поиск подсказки для '{context?.contextID ?? "null"}'.");
        if (context == null) return;

        TutorialHelpSpot nextSpot = context.helpSpots.FirstOrDefault(spot => 
            spot != null && 
            !visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );

        if (nextSpot != null)
        {
            Debug.Log($"<color=green>[TutorialMascot] ShowNextUnvisitedSpotInContext: Найдена подсказка '{nextSpot.spotID}'. Вызов ShowSpecificSpot.</color>");
            ShowSpecificSpot(nextSpot);
        }
        else
        {
            Debug.Log($"[TutorialMascot] ShowNextUnvisitedSpotInContext: Подсказки для '{context.contextID}' закончились. Переход в RunIdleLogicForContext.");
            if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
            tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(context));
        }
    }

    public void ShowNextUnvisitedSpotOnScreen()
    {
        Debug.Log($"<color=cyan>[TutorialMascot] ShowNextUnvisitedSpotOnScreen: СТОП ВСЕХ КОРУТИН.</color>");
        StopAllCoroutines();
        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;
        initialHintDelayCoroutine = null;
        nextHintDelayCoroutine = null; 
        
        TutorialHelpSpot nextSpot = FindNextUnvisitedSpotOnScreen();
        
        if (nextSpot != null)
        {
            Debug.Log($"<color=green>[TutorialMascot] ShowNextUnvisitedSpotOnScreen: Найдена следующая подсказка: '{nextSpot.spotID}'. Вызов ShowSpecificSpot.</color>");
            ShowSpecificSpot(nextSpot);
        }
        else
        {
            Debug.Log("[TutorialMascot] ShowNextUnvisitedSpotOnScreen: Подсказки на этом экране закончились.");
            
            // --- <<< НАЧАЛО ИЗМЕНЕНИЯ (Фикс Б1) >>> ---
            if (activeContextGroup != null)
            {
                // Если мы были в контексте (MainMenu_Default), мы должны запустить Idle *для этого контекста*
                Debug.Log($"[TutorialMascot] ShowNextUnvisitedSpotOnScreen: Переход в RunIdleLogicForContext (контекст: {activeContextGroup.contextID}).");
                tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(activeContextGroup));
            }
            else
            {
                // Если мы были в Idle (контекст null), запускаем Idle
                Debug.Log("[TutorialMascot] ShowNextUnvisitedSpotOnScreen: Переход в RunIdleLogic (контекст null).");
                tutorialCoroutine = StartCoroutine(RunIdleLogic(false)); 
            }
            // --- <<< КОНЕЦ ИЗМЕНЕНИЯ (Фикс Б1) >>> ---

            // --- <<< ИЗМЕНЕНИЕ (Б): Сбрасываем флаг, когда подсказки кончились >>> ---
            if (isTutorialResetting)
            {
                isTutorialResetting = false;
                Debug.Log("[TutorialMascot] ShowNextUnvisitedSpotOnScreen: Сброшен флаг 'isTutorialResetting = false' (подсказки закончились).");
            }
            // --- <<< КОНЕЦ ИЗМЕНЕНИЯ >>> ---
        }
    }

    private void ShowSpecificSpot(TutorialHelpSpot spot)
    {
        if (spot == null)
        {
            Debug.LogWarning("[TutorialMascot] ShowSpecificSpot: Получен null spot.");
            return;
        }
        
        if (spot.targetElement == null)
        {
            Debug.LogError($"<color=red>[TutorialMascot] ShowSpecificSpot: У подсказки '{spot.spotID}' не назначен 'targetElement'!</color>");
            return;
        }

        Debug.Log($"<color=green>[TutorialMascot] ShowSpecificSpot: Показ подсказки '{spot.spotID}' у элемента '{spot.targetElement.name}'.</color>");
        Vector2 targetPos = (Vector2)spot.targetElement.position + spot.mascotPositionOffset;
        
        tutorialCoroutine = StartCoroutine(TeleportToSpot(
            targetPos,
            spot.helpText,
            spot.mascotEmotionSprite,
            spot.textSheetHeight,
            spot.heightSteps, 
            spot.pointerSprite,
            spot.pointerRotation,
            spot.pointerPositionOffset,
            spot.soundRepetitions,
            false // Обычный фейд
        ));

        Debug.Log("<color=orange>[TutorialMascot] ShowSpecificSpot: Запуск NextHintDelayTimer.</color>");
        if (nextHintDelayCoroutine != null) StopCoroutine(nextHintDelayCoroutine);
        nextHintDelayCoroutine = StartCoroutine(NextHintDelayTimer());
        
        if (!visitedSpotIDs[currentScreenID].Contains(spot.spotID))
        {
            Debug.Log($"[TutorialMascot] ShowSpecificSpot: Отметка '{spot.spotID}' как посещенной.");
            visitedSpotIDs[currentScreenID].Add(spot.spotID);
            SaveVisitedState();
        }
    }

    #endregion

    #region Управление (Нажатия кнопок)

    private void OnNextButtonClicked()
    {
        if (isHidden) return;
        Debug.Log("[TutorialMascot] Нажата кнопка 'Next'.");
        PlaySound(mascotClickSound);
        ShowNextUnvisitedSpotOnScreen();
    }
    
    private void OnCloseButtonClicked()
    {
        Debug.Log("[TutorialMascot] Нажата кнопка 'Close'.");
        PlaySound(closeClickSound);
        Hide(isCeremonialAppearance); 
    }

    public void Hide(bool useCeremonialFade = false)
    {
        Debug.Log($"[TutorialMascot] Hide: Вызван. (isCeremonial = {useCeremonialFade})");
        
        Debug.Log($"<color=cyan>[TutorialMascot] Hide: Остановка целевых корутин...</color>");
        
        if (tutorialCoroutine != null) 
        {
            StopCoroutine(tutorialCoroutine);
            tutorialCoroutine = null;
            Debug.Log("<color=cyan>[TutorialMascot] Hide: Остановлена 'tutorialCoroutine'.</color>");
        }
        if (soundCoroutine != null) 
        {
            StopCoroutine(soundCoroutine);
            soundCoroutine = null;
        }
        if (sheetAnimationCoroutine != null) 
        {
            StopCoroutine(sheetAnimationCoroutine);
            sheetAnimationCoroutine = null;
        }
        if (initialHintDelayCoroutine != null) 
        {
            StopCoroutine(initialHintDelayCoroutine);
            initialHintDelayCoroutine = null;
        }
        if (nextHintDelayCoroutine != null) 
        {
            StopCoroutine(nextHintDelayCoroutine);
            nextHintDelayCoroutine = null;
        }
        
        if (!isHidden)
        {
            float duration = useCeremonialFade ? ceremonialFadeDuration : fadeDuration;
            Debug.Log($"[TutorialMascot] Hide: Запуск Fade (out). Длительность: {duration}");
            StartCoroutine(Fade(0f, duration));
        }
        
        isHidden = true;
        isCeremonialAppearance = false; 
    }

    #endregion

    #region Эффекты (Звук / Появление)

    private IEnumerator TeleportToSpot(Vector2 worldPosition, string text, Sprite emotion, float sheetHeight, int heightSteps, Sprite pointer, float pointerRot, Vector2 pointerOffset, int beeps, bool isCeremonial = false)
    {
        Debug.Log($"<color=green>[TutorialMascot] TeleportToSpot: Старт. (isCeremonial = {isCeremonial})</color>");
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        
        if (nextHintDelayCoroutine != null)
        {
            Debug.Log("<color=orange>[TutorialMascot] TeleportToSpot: Остановка NextHintDelayTimer.</color>");
            StopCoroutine(nextHintDelayCoroutine);
            nextHintDelayCoroutine = null;
        }

        float duration = isCeremonial ? ceremonialFadeDuration : fadeDuration;
        isCeremonialAppearance = isCeremonial; 

        if (!isHidden)
        {
            Debug.Log("[TutorialMascot] TeleportToSpot: Маскот видим. Сначала прячем (Fade out).");
            yield return StartCoroutine(Fade(0f, fadeDuration)); 
        }
        
        rectTransform.position = worldPosition;
        baseHoverPosition = rectTransform.anchoredPosition; 
        if (folderFrontImage != null) folderFrontImage.sprite = emotion;
        
        if (textSheetRect != null) textSheetRect.sizeDelta = new Vector2(textSheetRect.sizeDelta.x, 0);
        if (helpText != null) helpText.text = text;
        
        if (pointer != null && pointerHandImage != null)
        {
            pointerHandImage.gameObject.SetActive(true);
            pointerHandImage.sprite = pointer;
            pointerHandImage.rectTransform.localEulerAngles = new Vector3(0, 0, pointerRot);
            pointerHandImage.rectTransform.anchoredPosition = pointerOffset;
        }
        else if (pointerHandImage != null)
        {
            pointerHandImage.gameObject.SetActive(false);
        }
        
        if (textBubbleObject != null) textBubbleObject.SetActive(text != null && sheetHeight > 0);
        
        Debug.Log($"[TutorialMascot] TeleportToSpot: Появление (Fade in). Длительность: {duration}");
        yield return StartCoroutine(Fade(1f, duration)); 
        
        if (textSheetRect != null && sheetHeight > 0)
        {
            Debug.Log("[TutorialMascot] TeleportToSpot: Запуск AnimateSheetHeight.");
            sheetAnimationCoroutine = StartCoroutine(AnimateSheetHeight(sheetHeight, heightSteps));
        }

        if (soundCoroutine != null) StopCoroutine(soundCoroutine);
        if (beeps > 0) 
        {
            Debug.Log("[TutorialMascot] TeleportToSpot: Запуск PlayArrivalSound.");
            soundCoroutine = StartCoroutine(PlayArrivalSound(beeps));
        }
        Debug.Log("<color=green>[TutorialMascot] TeleportToSpot: Завершено.</color>");
        
        // --- <<< ИЗМЕНЕНИЕ (Б): Сбрасываем флаг в КОНЦЕ показа >>> ---
        if (isTutorialResetting)
        {
            isTutorialResetting = false;
            Debug.Log("[TutorialMascot] TeleportToSpot: Сброшен флаг 'isTutorialResetting = false'");
        }
        // --- <<< КОНЕЦ ИЗМЕНЕНИЯ >>> ---
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        Debug.Log($"[TutorialMascot] Fade: Старт. Цель: {targetAlpha}, Длительность: {duration}");
        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.interactable = (targetAlpha > 0);
        
        isHidden = (targetAlpha == 0);
        Debug.Log($"[TutorialMascot] Fade: Завершено. Alpha = {targetAlpha}, isHidden = {isHidden}");
    }

    private IEnumerator AnimateSheetHeight(float targetHeight, int steps)
    {
        if (textSheetRect == null) yield break;
        float startHeight = 0f; 
        int numSteps = Mathf.Max(1, steps); 

        if (numSteps == 1)
        {
            textSheetRect.sizeDelta = new Vector2(textSheetRect.sizeDelta.x, targetHeight);
            yield break;
        }

        for (int i = 1; i <= numSteps; i++)
        {
            float progress = (float)i / numSteps;
            float newHeight = Mathf.Lerp(startHeight, targetHeight, progress);
            textSheetRect.sizeDelta = new Vector2(textSheetRect.sizeDelta.x, newHeight);
            yield return new WaitForSecondsRealtime(sheetStepDelay);
        }
        textSheetRect.sizeDelta = new Vector2(textSheetRect.sizeDelta.x, targetHeight);
    }

    private IEnumerator PlayArrivalSound(int repetitions)
    {
        if (textBeepSound == null || textFinalBeepSound == null) yield break;
        
        int beepsDone = 0;
        while(beepsDone < repetitions)
        {
            int phraseLength = Random.Range(1, 4); 
            for (int i = 0; i < phraseLength && beepsDone < repetitions; i++)
            {
                audioSource.PlayOneShot(textBeepSound);
                beepsDone++;
                yield return new WaitForSecondsRealtime(textBeepSound.length * 0.7f); 
            }
            yield return new WaitForSecondsRealtime(Random.Range(0.2f, 0.5f));
        }
        
        audioSource.PlayOneShot(textFinalBeepSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Сохранение, Сброс и Вспомогательные методы

    public void ResetCurrentScreenTutorial()
    {
        Debug.Log($"<color=yellow>[TutorialMascot] ResetCurrentScreenTutorial: СБРОС для экрана '{currentScreenID}'</color>");
        
        // --- <<< ИЗМЕНЕНИЕ (Б): Устанавливаем флаг вначале >>> ---
        isTutorialResetting = true; 
        Debug.Log("[TutorialMascot] ResetCurrentScreenTutorial: Установлен флаг 'isTutorialResetting = true'");
        // --- <<< КОНЕЦ ИЗМЕНЕНИЯ >>> ---

        if (string.IsNullOrEmpty(currentScreenID))
        {
            Debug.LogWarning("<color=red>[TutorialMascot] ResetCurrentScreenTutorial: currentScreenID ПУСТ. Не могу сбросить.</color>");
            isTutorialResetting = false; // Сбрасываем флаг при ошибке
            return;
        }
        
        if (visitedSpotIDs.ContainsKey(currentScreenID))
        {
            visitedSpotIDs[currentScreenID].Clear();
            Debug.Log("[TutorialMascot] ResetCurrentScreenTutorial: Список посещенных спотов очищен.");
        }
        SaveVisitedState(); 
        
        Debug.Log("<color=cyan>[TutorialMascot] ResetCurrentScreenTutorial: СТОП ВСЕХ КОРУТИН.</color>");
        StopAllCoroutines();
        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;
        initialHintDelayCoroutine = null;
        nextHintDelayCoroutine = null;
        
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        isHidden = true;

        // --- <<< ИЗМЕНЕНИЕ (Б/Б1): НЕ сбрасываем контекст, а НАХОДИМ его >>> ---
        if (currentConfig != null && currentConfig.contextGroups != null)
        {
             activeContextGroup = currentConfig.contextGroups.FirstOrDefault(
                g => g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
             Debug.Log($"[TutorialMascot] ResetCurrentScreenTutorial: Контекст принудительно установлен на '{activeContextGroup?.contextID ?? "null"}'.");
        }
        else
        {
             activeContextGroup = null;
        }
        // --- <<< КОНЕЦ ИЗМЕНЕНИЯ >>> ---

        lastUsedIdleSpot = null;
        lastUsedGreetingIndex = -1;
        lastUsedTipIndex = -1;
        
        Debug.Log("[TutorialMascot] ResetCurrentScreenTutorial: Вызов ShowNextUnvisitedSpotOnScreen (для бонуса Б.2).");
        ShowNextUnvisitedSpotOnScreen(); // Этот вызов в итоге сбросит флаг 'isTutorialResetting'
    }
    
    public bool AreAllSpotsOnScreenVisited()
    {
        if (currentConfig == null || currentConfig.contextGroups == null)
        {
            Debug.Log("[TutorialMascot] AreAllSpotsOnScreenVisited: Конфиг не найден. Возвращаю True.");
            return true;
        }
        if (!visitedSpotIDs.ContainsKey(currentScreenID))
        {
            Debug.Log("[TutorialMascot] AreAllSpotsOnScreenVisited: Нет ключа в visitedSpotIDs. Возвращаю False.");
            return false;
        }
        
        bool result = currentConfig.contextGroups.All(g =>
            g.muteTutorial || 
            (g.helpSpots == null) || 
            g.helpSpots
                .Where(s => s != null) 
                .All(spot => visitedSpotIDs[currentScreenID].Contains(spot.spotID))
        );
        Debug.Log($"<color=yellow>[TutorialMascot] AreAllSpotsOnScreenVisited: Результат проверки = {result}</color>");
        return result;
    }

    private TutorialHelpSpot FindNextUnvisitedSpotOnScreen()
    {
        Debug.Log("[TutorialMascot] FindNextUnvisitedSpotOnScreen: Поиск...");
        if (currentConfig == null || currentConfig.contextGroups == null) return null;
        if (!visitedSpotIDs.ContainsKey(currentScreenID)) return null; 

        foreach (var context in currentConfig.contextGroups)
        {
            if (context.muteTutorial || context.helpSpots == null) continue;

            foreach (var spot in context.helpSpots)
            {
                if (spot != null && !visitedSpotIDs[currentScreenID].Contains(spot.spotID)) 
                {
                    Debug.Log($"[TutorialMascot] FindNextUnvisitedSpotOnScreen: Найден спот: {spot.spotID}");
                    return spot; 
                }
            }
        }
        Debug.Log("[TutorialMascot] FindNextUnvisitedSpotOnScreen: Непосещенные споты не найдены.");
        return null; // Все показано
    }

    private List<RectTransform> GetContextIdleSpots(TutorialContextGroup context)
    {
        if (context.contextIdleSpots != null && context.contextIdleSpots.Count > 0)
        {
            Debug.Log($"[TutorialMascot] GetContextIdleSpots: Используем {context.contextIdleSpots.Count} точек из контекста '{context.contextID}'.");
            return context.contextIdleSpots; 
        }
        Debug.Log($"[TutorialMascot] GetContextIdleSpots: Используем {currentConfig.idleSpots.Count} точек из ScreenConfig.");
        return currentConfig.idleSpots; 
    }

    private List<string> GetContextIdleTips(TutorialContextGroup context)
    {
        if (context != null && context.contextIdleTips != null && context.contextIdleTips.Count > 0)
        {
            Debug.Log($"[TutorialMascot] GetContextIdleTips: Используем 'Context Idle Tips' из '{context.contextID}'.");
            return context.contextIdleTips; 
        }
        if (currentConfig != null && currentConfig.idleTips != null && currentConfig.idleTips.Count > 0)
        {
            Debug.Log("[TutorialMascot] GetContextIdleTips: Используем 'Idle Tips' из ScreenConfig.");
            return currentConfig.idleTips;
        }
        Debug.Log("[TutorialMascot] GetContextIdleTips: 'Idle Tips' не найдены ни в контексте, ни в конфиге.");
        return null; 
    }

    private List<string> GetGreetingListFromConfig()
    {
        var firstContext = currentConfig?.contextGroups?.FirstOrDefault(g => !g.muteTutorial);
        if (firstContext != null && firstContext.greetingTexts != null && firstContext.greetingTexts.Count > 0)
        {
            return firstContext.greetingTexts;
        }
        return new List<string> { "Привет!" }; 
    }

    
    private void SaveVisitedState()
    {
        foreach (var pair in visitedSpotIDs)
        {
            string key = "Mascot_" + pair.Key; 
            string data = string.Join(";", pair.Value);
            PlayerPrefs.SetString(key, data);
        }
        PlayerPrefs.Save();
        Debug.Log("[TutorialMascot] SaveVisitedState: Состояние сохранено в PlayerPrefs.");
    }

    private void LoadVisitedState()
    {
        // Логика перенесена в DelayedSceneLoadLogic
        Debug.Log("[TutorialMascot] LoadVisitedState: Загрузка будет выполнена в DelayedSceneLoadLogic.");
    }

    #endregion
}