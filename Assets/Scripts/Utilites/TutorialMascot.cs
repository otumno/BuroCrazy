// Файл: Assets/Scripts/UI/Tutorial/TutorialMascot.cs
// ВЕРСИЯ С ЛОГАМИ И ИСПРАВЛЕНИЯМИ GDD (Приветствие + Авто-переключение + '?' + 'X')

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
    [SerializeField] private TextMeshProUGUI helpText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    [Header("Звуки")]
    [SerializeField] private AudioClip textBeepSound;
    [SerializeField] private AudioClip textFinalBeepSound;
    [SerializeField] private AudioClip mascotClickSound;
    [SerializeField] private AudioClip closeClickSound;

    [Header("Настройки Анимации")]
    [Tooltip("Примерная высота одной строки текста. Используется для авто-расчета высоты листка.")]
    [SerializeField] private float autoLineHeight = 65f;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float ceremonialFadeDuration = 1.5f;
    [SerializeField] private float hoverAmplitude = 5f;
    [SerializeField] private float hoverSpeed = 2f;
    [SerializeField] private float sheetStepDelay = 0.02f;
    
    private float currentInitialHintDelay = 3.0f;
    private float currentIdleMessageDelay = 10.0f;
    private float currentFirstEverDelay = 2.0f;
    private float currentNextHintDelay = 5.0f;
    private float currentSceneLoadDelay = 1.0f;
    private AudioSource audioSource;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private RectTransform textBubbleRect; 
    private TutorialScreenConfig currentConfig;
    
    private TutorialContextGroup activeContextGroup = null; 
    
    private Dictionary<string, List<string>> visitedSpotIDs = new Dictionary<string, List<string>>();
    private bool isHidden = true;
    private Vector2 baseHoverPosition;
    private Coroutine tutorialCoroutine; // "Мозг"
    private Coroutine soundCoroutine;
    private Coroutine sheetAnimationCoroutine;
    private bool isSceneLoadLogicRunning = false;
    private bool isTutorialResetting = false;
    private string currentScreenID = "";
    private bool isFirstEverAppearance = true;
    private bool isCeremonialAppearance = false;
    private bool isFirstAppearanceThisSession = true;
    private RectTransform lastUsedIdleSpot = null;
    private int lastUsedGreetingIndex = -1;
    private int lastUsedTipIndex = -1;

    // --- ФЛАГ "СНА" (GDD 4.2) ---
    private bool isSilenced = false;

    #region Инициализация и Сцены

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("<color=green>[TutorialMascot] Awake: Я стал Singleton.</color>");
            
            if (transform.parent != null)
            {
                // --- ИСПРАВЛЕНИЕ ДЛЯ DontDestroyOnLoad ---
                // Мы должны сделать "бессмертным" самого верхнего родителя
                Transform root = transform;
                while (root.parent != null)
                {
                    root = root.parent;
                }
                DontDestroyOnLoad(root.gameObject);
                Debug.Log($"<color=green>[TutorialMascot] Awake: Сделал {root.name} бессмертным.</color>");
            }
            else
            {
                DontDestroyOnLoad(gameObject);
                Debug.Log($"<color=green>[TutorialMascot] Awake: У меня нет родителя, делаю {gameObject.name} бессмертным.</color>");
            }
            
            SceneManager.sceneLoaded += OnSceneLoadedStarter;
            isFirstEverAppearance = PlayerPrefs.GetInt("Mascot_FirstEverAppearance", 0) == 0;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[TutorialMascot] Awake: Найден дубликат. Уничтожаю.");
            // --- ИСПРАВЛЕНИЕ ДЛЯ DontDestroyOnLoad ---
            // Уничтожаем *всю* иерархию дубликата
            if (transform.parent != null)
            {
                Transform root = transform;
                while (root.parent != null)
                {
                    root = root.parent;
                }
                Debug.LogWarning($"[TutorialMascot] Уничтожаю дубликат {root.name}.");
                Destroy(root.gameObject);
            }
            else
            {
                Debug.LogWarning($"[TutorialMascot] Уничтожаю дубликат {gameObject.name}.");
                Destroy(gameObject);
            }
            return;
        }

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        audioSource = GetComponent<AudioSource>();
        audioSource.ignoreListenerPause = true;
        
        if (textBubbleObject != null)
        {
            textBubbleRect = textBubbleObject.GetComponent<RectTransform>();
        }
        else
        {
            Debug.LogError("[TutorialMascot] textBubbleObject не назначен в инспекторе!");
        }

        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        isHidden = true;
    }

    void Start()
    {
        nextButton.onClick.AddListener(OnNextButtonClicked);
        closeButton.onClick.AddListener(OnCloseButtonClicked);
        LoadVisitedState();
    }
    
    void OnSceneLoadedStarter(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"<color=purple>[TutorialMascot] OnSceneLoadedStarter: Сцена '{scene.name}' загружена.</color>");
        if (isSceneLoadLogicRunning)
        {
            Debug.LogWarning("[TutorialMascot] OnSceneLoadedStarter: Предыдущая загрузка еще выполняется. Выход.");
            return;
        }
        StartCoroutine(DelayedSceneLoadLogic(scene));
    }

    IEnumerator DelayedSceneLoadLogic(Scene scene)
    {
        isSceneLoadLogicRunning = true;
        isTutorialResetting = false;
        isSilenced = false; // Сбрасываем "сон" при загрузке сцены

        Debug.Log("[TutorialMascot] DelayedSceneLoadLogic: StopAllCoroutines() при загрузке сцены.");
        StopAllCoroutines();
        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;

        currentConfig = FindObjectOfType<TutorialScreenConfig>();
        if (currentConfig != null)
        {
            currentScreenID = currentConfig.screenID;
            Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: TutorialScreenConfig НАЙДЕН. ScreenID: '{currentScreenID}'.");

            currentInitialHintDelay = currentConfig.initialHintDelay;
            currentIdleMessageDelay = currentConfig.idleMessageChangeDelay;
            currentSceneLoadDelay = currentConfig.sceneLoadDelay;
            currentNextHintDelay = currentConfig.nextHintDelay;

            bool useCeremonialDelay = false;
            if (isFirstEverAppearance)
            {
                currentSceneLoadDelay = currentConfig.firstEverAppearanceDelay;
                useCeremonialDelay = true;
            }
            else if (isFirstAppearanceThisSession)
            {
                currentSceneLoadDelay = currentConfig.firstEverAppearanceDelay;
                useCeremonialDelay = true;
            }
            else 
            {
                currentSceneLoadDelay = currentConfig.sceneLoadDelay;
                useCeremonialDelay = false;
            }
            
            if (!visitedSpotIDs.ContainsKey(currentScreenID))
            {
                string key = "Mascot_" + currentScreenID;
                if (PlayerPrefs.HasKey(key))
                {
                    string data = PlayerPrefs.GetString(key);
                    visitedSpotIDs[currentScreenID] = new List<string>(data.Split(';').Where(s => !string.IsNullOrEmpty(s)));
                }
                else
                {
                    visitedSpotIDs[currentScreenID] = new List<string>();
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

            Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Ожидание задержки загрузки сцены: {currentSceneLoadDelay}с.");
            yield return new WaitForSecondsRealtime(currentSceneLoadDelay);
            Debug.Log("[TutorialMascot] DelayedSceneLoadLogic: Задержка прошла.");
            
            // --- ИСПРАВЛЕНИЕ (GDD 3.1 / 3.3) ---
            // 'tutorialCoroutine' здесь = null. Мы должны ПРИНУДИТЕЛЬНО 
            // запустить правильную стартовую логику, а не ждать Update().

            TutorialContextGroup startContext = null;
            if (currentConfig.contextGroups != null)
            {
                startContext = currentConfig.contextGroups.FirstOrDefault(
                    g => g != null && g.contextPanel != null && g.contextPanel.activeInHierarchy
                );
            }

            activeContextGroup = startContext; // Устанавливаем начальный контекст

            if (activeContextGroup != null && activeContextGroup.muteTutorial)
            {
                Debug.Log("[TutorialMascot] DelayedSceneLoadLogic: Начальный контекст 'mute'. Ничего не запускаем.");
            }
            else if (activeContextGroup != null)
            {
                // GDD 3.1: Запускаем Приветствие/Логику для активного контекста
                Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Запуск 'RunTutorialForContext' для '{activeContextGroup.contextID}'.");
                tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup));
            }
            else
            {
                // GDD 3.1 / 3.3: Запускаем Приветствие/Idle для Базового контекста
                Debug.Log("[TutorialMascot] DelayedSceneLoadLogic: Запуск 'RunIdleLogic' (Базовый контекст).");
                tutorialCoroutine = StartCoroutine(RunIdleLogic(useCeremonialDelay));
            }
            // --- КОНЕЦ ИСПРАВЛЕНИЯ ---
        }
        else
        {
            currentScreenID = "";
            activeContextGroup = null;
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
    }


    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedStarter;
            Debug.Log("[TutorialMascot] OnDestroy: Отписался от SceneManager.sceneLoaded.");
        }
    }
    
    #endregion

    #region Главная Логика (Update)
    
    void Update()
    {
        // Анимация парения (Hover)
        if (!isHidden)
        {
            rectTransform.anchoredPosition = baseHoverPosition +
                new Vector2(0, Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude);
        }

        // Пропускаем логику, если конфиг не найден или идет загрузка сцены
        if (currentConfig == null || isSceneLoadLogicRunning || isTutorialResetting)
        {
            return;
        }
        
        // 1. ОПРЕДЕЛЯЕМ ТЕКУЩИЙ КОНТЕКСТ
        TutorialContextGroup newContext = null;
        if (currentConfig.contextGroups != null)
        {
            newContext = currentConfig.contextGroups.FirstOrDefault(
                 g => g != null && g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
        }

        // 2. ПРОВЕРЯЕМ, ИЗМЕНИЛСЯ ЛИ КОНТЕКСТ (GDD 2)
        if (newContext != activeContextGroup)
        {
            Debug.Log($"<color=yellow>[TutorialMascot] Update: КОНТЕКСТ ИЗМЕНИЛСЯ. Старый: '{activeContextGroup?.contextID ?? "Базовый"}' -> Новый: '{newContext?.contextID ?? "Базовый"}'</color>");
            
            Debug.Log("[TutorialMascot] Update: StopAllCoroutines() из-за смены контекста.");
            StopAllCoroutines();
            
            tutorialCoroutine = null;
            soundCoroutine = null;
            sheetAnimationCoroutine = null;
            
            activeContextGroup = newContext;
            isCeremonialAppearance = false;
            isSilenced = false; // Сбрасываем "сон" при смене контекста

            Debug.Log("[TutorialMascot] Update: Немедленное скрытие (HideInternal()) из-за смены контекста.");
            HideInternal(); // GDD 2: "немедленно исчезает"
            
            if (activeContextGroup != null && activeContextGroup.muteTutorial)
            {
                // GDD 2: "Тихий" Контекст
                Debug.Log("[TutorialMascot] Update: Новый контекст 'muteTutorial'. Остаемся скрытыми.");
            }
            else if (activeContextGroup != null)
            {
                // GDD 3.1: Новый Контекст
                Debug.Log("[TutorialMascot] Update: Запуск 'RunTutorialForContext' для нового контекста.");
                tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup));
            }
            else
            {
                // GDD 3.3: Возврат в Базовый/Idle контекст
                Debug.Log("[TutorialMascot] Update: Запуск 'RunIdleLogic' (Базовый контекст).");
                tutorialCoroutine = StartCoroutine(RunIdleLogic(false));
            }
        }
        // 3. ЕСЛИ КОНТЕКСТ НЕ МЕНЯЛСЯ, НО "МОЗГ" СВОБОДЕН
        // GDD 3.2 (Авто-переключение) / GDD 3.3 (Idle-цикл)
        else if (tutorialCoroutine == null && !isSilenced) // <-- ДОБАВЛЕН ФИКС 'isSilenced' (GDD 4.2)
        {
            // Это происходит, когда:
            // 1. Закончился 'initialHintDelay' (GDD 3.1)
            // 2. Закончился 'nextHintDelay' (GDD 3.2)
            // 3. Нажата кнопка 'Next' (GDD 4.1)
            // 4. Закончился 'idleMessageDelay' (GDD 3.3)
            Debug.Log($"[TutorialMascot] Update: 'tutorialCoroutine' == null и НЕ 'isSilenced'. Мозг свободен. Вызов RequestNextHintSmart().");
            RequestNextHintSmart(); // <-- ИСПРАВЛЕНИЕ: Вызываем void-метод
        }
    }

    #endregion

    #region Логика Туториала и Idle (GDD 3.1, 3.2, 3.3)

    // Логика для Базового контекста (когда activeContextGroup == null)
    private IEnumerator RunIdleLogic(bool useCeremonial = false)
    {
        Debug.Log("[TutorialMascot] RunIdleLogic: (Базовый) Старт.");
        if (isFirstEverAppearance)
        {
            isFirstEverAppearance = false; 
            PlayerPrefs.SetInt("Mascot_FirstEverAppearance", 1);
            PlayerPrefs.Save();
            Debug.Log("[TutorialMascot] RunIdleLogic: Это первое появление в игре.");
        }
        isFirstAppearanceThisSession = false;

        // --- ИСПРАВЛЕНИЕ: Разделение логики GDD 3.1 и 3.3 ---
        bool allSpotsOnScreenVisited = AreAllSpotsOnScreenVisited();

        if (!allSpotsOnScreenVisited)
        {
            // --- GDD 3.1: ПРИВЕТСТВИЕ (выполняется ОДИН РАЗ) ---
            Debug.Log("[TutorialMascot] RunIdleLogic: (Базовый) Обнаружены непросмотренные подсказки. Запуск GDD 3.1 (Приветствие).");
            var visualContext = currentConfig.contextGroups.FirstOrDefault(g => g != null && !g.muteTutorial);
            if (visualContext == null) { HideInternal(); tutorialCoroutine = null; yield break; }
            List<RectTransform> idleSpots = GetContextIdleSpots(visualContext);
            if (idleSpots == null || idleSpots.Count == 0) { HideInternal(); tutorialCoroutine = null; yield break; }
            RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];
            lastUsedIdleSpot = randomIdleSpot;
            List<string> messageList = visualContext.greetingTexts; 
            string message = (messageList != null && messageList.Count > 0) ? messageList[Random.Range(0, messageList.Count)] : "Привет!";
            
            yield return StartCoroutine(TeleportToSpot(
                randomIdleSpot.position, message, visualContext.greetingEmotion, 
                visualContext.greetingPointerSprite, visualContext.greetingPointerRotation, 
                visualContext.greetingPointerOffset, useCeremonial
            ));
            
            // GDD 3.1: "Она ждет N секунд (initialHintDelay)"
            Debug.Log($"[TutorialMascot] RunIdleLogic: (Базовый) Приветствие показано. Ожидание initialHintDelay: {currentInitialHintDelay}с.");
            yield return new WaitForSecondsRealtime(currentInitialHintDelay);
            
            Debug.Log("[TutorialMascot] RunIdleLogic: (Базовый) GDD 3.1 завершен. 'Мозг' освобожден (Update() вызовет 1-ю подсказку).");
            tutorialCoroutine = null;
            // НЕ ИСПОЛЬЗУЕМ yield break, корутина просто завершается.
        }
        else
        {
            // --- GDD 3.3: РЕЖИМ "ОТДЫХА" (цикл) ---
            Debug.Log("[TutorialMascot] RunIdleLogic: (Базовый) Все подсказки просмотрены. Запуск GDD 3.3 (Цикл Idle/Tips).");
            while (activeContextGroup == null && !isSilenced) // GDD 3.3: "Она остается в этом режиме, пока..."
            {
                var visualContext = currentConfig.contextGroups.FirstOrDefault(g => g != null && !g.muteTutorial);
                if (visualContext == null) { HideInternal(); tutorialCoroutine = null; yield break; }
                List<RectTransform> idleSpots = GetContextIdleSpots(visualContext);
                if (idleSpots == null || idleSpots.Count == 0) { HideInternal(); tutorialCoroutine = null; yield break; }
                RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];
                lastUsedIdleSpot = randomIdleSpot;
                List<string> messageList = GetContextIdleTips(visualContext); // GDD 3.3: "показывает 'советы' (Idle Tips)"
                if (messageList == null || messageList.Count == 0) messageList = visualContext.greetingTexts; // Fallback
                string message = (messageList != null && messageList.Count > 0) ? messageList[Random.Range(0, messageList.Count)] : "Я здесь, если что!";

                yield return StartCoroutine(TeleportToSpot(
                    randomIdleSpot.position, message, visualContext.greetingEmotion, 
                    visualContext.greetingPointerSprite, visualContext.greetingPointerRotation, 
                    visualContext.greetingPointerOffset, false // 'useCeremonial' = false
                ));

                // GDD 3.3: "периодически (раз в M секунд)"
                Debug.Log($"[TutorialMascot] RunIdleLogic: (Базовый) GDD 3.3 Сообщение показано. Ожидание idleMessageDelay: {currentIdleMessageDelay}с.");
                yield return new WaitForSecondsRealtime(currentIdleMessageDelay);
                
                if (activeContextGroup == null) // Прячемся перед следующей итерацией
                {
                    yield return StartCoroutine(Fade(0f, fadeDuration));
                }
            }
            
            Debug.Log("[TutorialMascot] RunIdleLogic: (Базовый) Цикл GDD 3.3 завершен (контекст изменился или 'уснул'). 'Мозг' освобожден.");
            tutorialCoroutine = null;
        }
    }

    // Логика для Активного Контекста
    private IEnumerator RunTutorialForContext(TutorialContextGroup context)
    {
        Debug.Log($"[TutorialMascot] RunTutorialForContext: Старт для '{context.contextID}'.");
        bool allSpotsInContextVisited = context.helpSpots.All(spot => 
            spot == null || 
            visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );

        if (allSpotsInContextVisited)
        {
            // GDD 3.3: Все подсказки в этом контексте просмотрены
            Debug.Log($"[TutorialMascot] RunTutorialForContext: Все подсказки для '{context.contextID}' уже просмотрены. Переход в RunIdleLogicForContext.");
            tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(context));
            yield break;
        }

        bool isFirstTimeInContext = !context.helpSpots.Any(spot => 
            spot != null && 
            visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );

        if (isFirstTimeInContext)
        {
            // GDD 3.1: Приветствие
            Debug.Log($"[TutorialMascot] RunTutorialForContext: Первое появление в '{context.contextID}'. Показ Приветствия.");
            List<RectTransform> idleSpots = GetContextIdleSpots(context);
            RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];
            
            string greeting = "Привет!";
            if (context.greetingTexts != null && context.greetingTexts.Count > 0)
            {
                greeting = context.greetingTexts[Random.Range(0, context.greetingTexts.Count)];
            }

            yield return StartCoroutine(TeleportToSpot(
                 randomIdleSpot.position,
                greeting,
                context.greetingEmotion, 
                context.greetingPointerSprite,
                context.greetingPointerRotation,
                context.greetingPointerOffset,
                false 
            ));
            
            // GDD 3.1: "Она ждет N секунд (initialHintDelay)"
            Debug.Log($"[TutorialMascot] RunTutorialForContext: Приветствие показано. Ожидание InitialHintDelayTimer ({currentInitialHintDelay}с).");
            yield return new WaitForSecondsRealtime(currentInitialHintDelay);
            
            Debug.Log($"[TutorialMascot] RunTutorialForContext: InitialHintDelayTimer завершен. 'Мозг' освобожден.");
            tutorialCoroutine = null;
        }
        else
        {
            // Мы вернулись в контекст, где уже что-то видели, но не всё.
            Debug.Log($"[TutorialMascot] RunTutorialForContext: Возвращение в '{context.contextID}'. 'Мозг' освобожден (Update() подхватит).");
            tutorialCoroutine = null; 
        }
    }

    // Логика Idle для Активного Контекста (GDD 3.3)
    private IEnumerator RunIdleLogicForContext(TutorialContextGroup context)
    {
        Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Старт (режим Idle) для '{context.contextID}'.");
        while (activeContextGroup != null && activeContextGroup.contextID == context.contextID && !isSilenced)
        {
            List<RectTransform> idleSpots = GetContextIdleSpots(context);
            RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];

            bool isUsingGreetings;
            int lastIndex;
            
            List<string> messageList = GetContextIdleTips(context);
            if (messageList != null && messageList.Count > 0)
            {
                isUsingGreetings = false;
                lastIndex = lastUsedTipIndex;
            }
            else
            {
                messageList = context.greetingTexts;
                isUsingGreetings = true;
                lastIndex = lastUsedGreetingIndex;
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

            Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Показ Idle сообщения: '{message}'");
            yield return StartCoroutine(TeleportToSpot(
                randomIdleSpot.position,
                message,
                context.greetingEmotion, 
                context.greetingPointerSprite,
                context.greetingPointerRotation,
                context.greetingPointerOffset,
                false 
            ));

            // GDD 3.3: "периодически (раз в M секунд)"
            Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Ожидание Idle ({currentIdleMessageDelay}с).");
            yield return new WaitForSecondsRealtime(currentIdleMessageDelay);

            if (activeContextGroup != null && activeContextGroup.contextID == context.contextID)
            {
                Debug.Log("[TutorialMascot] RunIdleLogicForContext: Контекст не изменился. Плавное скрытие.");
                yield return StartCoroutine(Fade(0f, fadeDuration)); 
            }
        }
        Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Цикл завершен (контекст изменился или 'уснул'). 'Мозг' освобожден.");
        tutorialCoroutine = null; 
    }
    
    // GDD 3.2 Авто-переключение
    private IEnumerator ShowSpecificSpotAndManageCoroutine(TutorialHelpSpot spot)
    {
        Debug.Log($"[TutorialMascot] ShowSpecificSpotAndManageCoroutine: Старт для '{spot.spotID}'.");
        
        if (spot.targetElement == null)
        {
            Debug.LogError($"<color=red>[TutorialMascot] ShowSpecificSpot: У подсказки '{spot.spotID}' не назначен 'targetElement'!</color>");
            tutorialCoroutine = null; 
            yield break;
        }

        if (!visitedSpotIDs.ContainsKey(currentScreenID))
        {
            // Эта ситуация не должна возникать, если LoadVisitedState отработал
            visitedSpotIDs[currentScreenID] = new List<string>();
        }
        
        if (!visitedSpotIDs[currentScreenID].Contains(spot.spotID))
        {
            Debug.Log($"[TutorialMascot] ShowSpecificSpotAndManageCoroutine: '{spot.spotID}' отмечается как просмотренный.");
            visitedSpotIDs[currentScreenID].Add(spot.spotID);
            SaveVisitedState();
        }
        
        // GDD 3.2: "Папочка 'телепортируется'... Она показывает свой листок"
        yield return StartCoroutine(TeleportToSpot(
            (Vector2)spot.targetElement.position + spot.mascotPositionOffset,
            spot.helpText,
            spot.mascotEmotionSprite,
            spot.pointerSprite,
            spot.pointerRotation,
            spot.pointerPositionOffset,
            false 
        ));
        
        // GDD 3.2: "Папочка ждет N секунд (nextHintDelay)"
        Debug.Log($"[TutorialMascot] ShowSpecificSpotAndManageCoroutine: Ожидание NextHintDelayTimer ({currentNextHintDelay}с).");
        yield return new WaitForSecondsRealtime(currentNextHintDelay);
        
        // GDD 3.2: "она автоматически переходит к показу следующей"
        Debug.Log($"[TutorialMascot] ShowSpecificSpotAndManageCoroutine: NextHintDelayTimer завершен. 'Мозг' освобожден (Update() вызовет GDD 3.2-auto).");
        tutorialCoroutine = null;
    }

    #endregion

    #region Управление (Нажатия кнопок)

    // GDD 4.1: Клик по маскоту ("Next")
    private void OnNextButtonClicked()
    {
        if (isHidden) return;
        Debug.Log("<color=cyan>[TutorialMascot] OnNextButtonClicked: Клик по 'Next'.</color>");
        PlaySound(mascotClickSound);
        
        // GDD 4.1: "Немедленно прерывает... таймер"
        Debug.Log("[TutorialMascot] OnNextButtonClicked: StopAllCoroutines().");
        StopAllCoroutines(); // Прерываем ВСЕ, включая 'мозг' (tutorialCoroutine)

        // GDD 4.1: "Немедленно запускает поиск следующей подсказки"
        tutorialCoroutine = null; 

        isSilenced = false; // "Разбудить", если спал

        // --- ИСПРАВЛЕНИЕ: Вызываем немедленно, не ждем Update() ---
        RequestNextHintSmart();
    }
    
    // GDD 4.2: Клик по 'X' ("Close")
    private void OnCloseButtonClicked()
    {
        if (isHidden) return; 
        Debug.Log("<color=red>[TutorialMascot] OnCloseButtonClicked: Клик по 'X'.</color>");
        PlaySound(closeClickSound);
        
        // GDD 4.2: "Немедленно прерывает все"
        Debug.Log("[TutorialMascot] OnCloseButtonClicked: StopAllCoroutines().");
        StopAllCoroutines();
        
        // GDD 4.2: "и прячется"
        Debug.Log("[TutorialMascot] OnCloseButtonClicked: HideInternal().");
        HideInternal(); 

        // --- НОВЫЙ ФИКС (GDD 4.2) ---
        // "Папочка "засыпает" в этом контексте."
        isSilenced = true;
        Debug.Log("[TutorialMascot] OnCloseButtonClicked: Установка isSilenced = true. 'Мозг' остановлен.");
        tutorialCoroutine = null; // Освобождаем "мозг", но 'isSilenced' не даст ему запуститься в Update()
    }
    
    private void HideInternal()
    {
        Debug.Log("[TutorialMascot] HideInternal: Скрытие (alpha=0, interactable=false).");
        if (!isHidden)
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
        }
        isHidden = true;
    }

    #endregion

    #region Эффекты (Звук / Появление)

    private IEnumerator TeleportToSpot(Vector2 worldPosition, string text, Sprite emotion, 
        Sprite pointer, float pointerRot, Vector2 pointerOffset, bool isCeremonial = false)
    {
        Debug.Log($"[TutorialMascot] TeleportToSpot: Старт. Позиция: {worldPosition}, Текст: '{(text != null && text.Length > 10 ? text.Substring(0, 10) : text)}...'");
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        if (soundCoroutine != null) StopCoroutine(soundCoroutine); 

        float duration = isCeremonial ? ceremonialFadeDuration : fadeDuration;
        isCeremonialAppearance = isCeremonial; 

        if (!isHidden)
        {
            Debug.Log("[TutorialMascot] TeleportToSpot: Маскот был виден. Плавное скрытие перед телепортацией.");
            yield return StartCoroutine(Fade(0f, fadeDuration)); 
        }
        
        rectTransform.position = worldPosition;
        baseHoverPosition = rectTransform.anchoredPosition; 
        if (folderFrontImage != null) folderFrontImage.sprite = emotion;
        
        if (helpText != null) 
        {
            helpText.text = text;
            helpText.ForceMeshUpdate(); 
        }
        
        int lineCount = 0;
        if (helpText != null && !string.IsNullOrEmpty(text))
        {
            lineCount = helpText.textInfo.lineCount;
        }

        float calculatedSheetHeight = (lineCount + 1) * autoLineHeight; 
        int calculatedHeightSteps = lineCount + 1;
        
        if (textBubbleRect != null) textBubbleRect.sizeDelta = new Vector2(textBubbleRect.sizeDelta.x, 0);
        
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
        
        if (textBubbleObject != null) textBubbleObject.SetActive(text != null && calculatedSheetHeight > 0);
        
        Debug.Log("[TutorialMascot] TeleportToSpot: Плавное появление.");
        yield return StartCoroutine(Fade(1f, duration)); 
        
        if (textBubbleRect != null && calculatedSheetHeight > 0)
        {
            Debug.Log("[TutorialMascot] TeleportToSpot: Запуск анимации 'листка' и звуков.");
            soundCoroutine = StartCoroutine(PlayBeepLoop());
            
            sheetAnimationCoroutine = StartCoroutine(AnimateSheetHeight(calculatedSheetHeight, calculatedHeightSteps));
            yield return sheetAnimationCoroutine; 
            
            if (soundCoroutine != null) StopCoroutine(soundCoroutine);
            soundCoroutine = null;
            PlaySound(textFinalBeepSound);
            Debug.Log("[TutorialMascot] TeleportToSpot: Анимация 'листка' завершена.");
        }
        
        if (isTutorialResetting)
        {
            isTutorialResetting = false;
        }
        
        // --- ЗАПУСК ТАЙМЕРА УБРАН ОТСЮДА ---
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
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
    }

    private IEnumerator AnimateSheetHeight(float targetHeight, int steps)
    {
        if (textBubbleRect == null) yield break;
        
        float startHeight = 0f; 
        int numSteps = Mathf.Max(1, steps); 

        if (numSteps == 1)
        {
            textBubbleRect.sizeDelta = new Vector2(textBubbleRect.sizeDelta.x, targetHeight);
        }
        else
        {
            for (int i = 1; i <= numSteps; i++)
            {
                float progress = (float)i / numSteps;
                float newHeight = Mathf.Lerp(startHeight, targetHeight, progress);
                textBubbleRect.sizeDelta = new Vector2(textBubbleRect.sizeDelta.x, newHeight);
                yield return new WaitForSecondsRealtime(sheetStepDelay);
            }
        }
        
         textBubbleRect.sizeDelta = new Vector2(textBubbleRect.sizeDelta.x, targetHeight);
    }

    private IEnumerator PlayBeepLoop()
    {
        if (textBeepSound == null) yield break; 

        while (true)
        {
            int phraseLength = Random.Range(1, 4); 
            for (int i = 0; i < phraseLength; i++)
            {
                audioSource.PlayOneShot(textBeepSound);
                yield return new WaitForSecondsRealtime(textBeepSound.length * 0.7f); 
            }
            yield return new WaitForSecondsRealtime(Random.Range(0.2f, 0.5f));
        }
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
    
    // GDD 4.1 / 4.3 / 3.2 / 3.3
    // Это ЕДИНСТВЕННЫЙ метод, который должен запускать "мозг" (кроме загрузки сцены и сброса)
    public void RequestNextHintSmart()
    {
        // --- ЗАЩИТА ОТ ДВОЙНОГО ВЫЗОВА ---
        if (IsBusy())
        {
            Debug.LogWarning("[TutorialMascot] RequestNextHintSmart: Вызван, когда 'мозг' УЖЕ БЫЛ ЗАНЯТ. Игнорирую.");
            return;
        }
        // --- КОНЕЦ ЗАЩИТЫ ---

        Debug.Log("<color=cyan>[TutorialMascot] RequestNextHintSmart: Старт.</color>");
        
        isSilenced = false; // "Разбудить", если спал
        
        // Мы запускаем корутину и СРАЗУ ЖЕ присваиваем ее 'tutorialCoroutine', 
        // делая 'IsBusy()' истинным.
        tutorialCoroutine = StartCoroutine(RequestNextHintSmart_Coroutine());
    }

    // Эта корутина - фактический "мозг", который запускается ОДИН РАЗ
    private IEnumerator RequestNextHintSmart_Coroutine()
    {
        // Прерываем только "младшие" корутины, но не "мозг" (который = this)
        if (soundCoroutine != null) StopCoroutine(soundCoroutine);
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        
        if (activeContextGroup != null)
        {
            // GDD 3.2 (Контекст)
            TutorialHelpSpot nextSpotInContext = FindNextUnvisitedSpotInContext(activeContextGroup);
            if (nextSpotInContext != null)
            {
                Debug.Log($"[Mascot Brain] Найдена подсказка в КОНТЕКСТЕ: '{nextSpotInContext.spotID}'.");
                // Эта корутина сама освободит "мозг" (`tutorialCoroutine = null;`)
                yield return StartCoroutine(ShowSpecificSpotAndManageCoroutine(nextSpotInContext));
            }
            else
            {
                // GDD 3.3 (Контекст)
                Debug.Log($"[Mascot Brain] Подсказки в контексте '{activeContextGroup.contextID}' закончились. Запуск Idle (Контекст).");
                // Эта корутина сама освободит "мозг" (`tutorialCoroutine = null;`)
                yield return StartCoroutine(RunIdleLogicForContext(activeContextGroup));
            }
        }
        else
        {
            // GDD 3.2 (Базовый)
            TutorialHelpSpot nextSpotOnScreen = FindNextUnvisitedSpotOnScreen();
            if (nextSpotOnScreen != null)
            {
                Debug.Log($"[Mascot Brain] (Базовый) Найдена подсказка на ЭКРАНЕ: '{nextSpotOnScreen.spotID}'.");
                // Эта корутина сама освободит "мозг" (`tutorialCoroutine = null;`)
                yield return StartCoroutine(ShowSpecificSpotAndManageCoroutine(nextSpotOnScreen));
            }
            else
            {
                // GDD 3.3 (Базовый)
                Debug.Log("[Mascot Brain] (Базовый) Подсказки на экране закончились. Запуск Idle (Базовый).");
                // Эта корутина сама освободит "мозг" (`tutorialCoroutine = null;`)
                yield return StartCoroutine(RunIdleLogic(false));
            }
        }
    }


    private TutorialHelpSpot FindNextUnvisitedSpotInContext(TutorialContextGroup context)
    {
        if (context == null || context.helpSpots == null || !visitedSpotIDs.ContainsKey(currentScreenID))
        {
            return null;
        }
        
        return context.helpSpots.FirstOrDefault(spot => 
            spot != null && 
            !visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );
    }
    
    // GDD 4.3 (Сброс)
    public void ResetCurrentScreenTutorial()
    {
        Debug.Log($"<color=orange>[TutorialMascot] ResetCurrentScreenTutorial: Сброс прогресса для экрана '{currentScreenID}'.</color>");
        isTutorialResetting = true;
        isSilenced = false; // "Разбудить"

        if (string.IsNullOrEmpty(currentScreenID))
        {
            Debug.LogError("[TutorialMascot] ResetCurrentScreenTutorial: currentScreenID пуст. Сброс невозможен.");
            isTutorialResetting = false;
            return;
        }
        
        if (visitedSpotIDs.ContainsKey(currentScreenID))
        {
            visitedSpotIDs[currentScreenID].Clear();
            Debug.Log($"[TutorialMascot] ResetCurrentScreenTutorial: Список visitedSpotIDs для '{currentScreenID}' очищен.");
        }
        SaveVisitedState();
        
        Debug.Log("[TutorialMascot] ResetCurrentScreenTutorial: StopAllCoroutines().");
        StopAllCoroutines();
        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;
        
        Debug.Log("[TutorialMascot] ResetCurrentScreenTutorial: HideInternal().");
        HideInternal();

        // Обновляем текущий контекст
        if (currentConfig != null && currentConfig.contextGroups != null)
        {
            activeContextGroup = currentConfig.contextGroups.FirstOrDefault(
                g => g != null && g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
        }
        else
        {
             activeContextGroup = null;
        }
        Debug.Log($"[TutorialMascot] ResetCurrentScreenTutorial: Текущий контекст обновлен на '{activeContextGroup?.contextID ?? "Базовый"}'.");

        lastUsedIdleSpot = null;
        lastUsedGreetingIndex = -1;
        lastUsedTipIndex = -1;
        
        // --- ИСПРАВЛЕНИЕ: Принудительно запускаем "мозг" ---
        // (Раньше мы ждали Update(), теперь GDD 4.3 требует немедленного старта)
        Debug.Log("[TutorialMascot] ResetCurrentScreenTutorial: Принудительный запуск 'мозга' для показа Приветствия.");
        RequestNextHintSmart();
    }
    
    // GDD 4.3 (Проверка для '?')
    public bool AreAllSpotsInCurrentContextVisited()
    {
        if (activeContextGroup != null)
        {
            // Проверяем только текущий контекст
            if (activeContextGroup.helpSpots == null || !visitedSpotIDs.ContainsKey(currentScreenID))
            {
                return true;
            }
            bool result = activeContextGroup.helpSpots
                .Where(s => s != null)
                .All(spot => visitedSpotIDs[currentScreenID].Contains(spot.spotID));
            
            return result;
        }
        else
        {
            // Проверяем ВЕСЬ экран (Базовый контекст)
            return AreAllSpotsOnScreenVisited();
        }
    }
    
    private bool AreAllSpotsOnScreenVisited()
    {
        if (currentConfig == null || currentConfig.contextGroups == null) return true;
        if (!visitedSpotIDs.ContainsKey(currentScreenID)) return false;
        
        bool result = currentConfig.contextGroups.All(g =>
            g == null ||
            g.muteTutorial ||
            (g.helpSpots == null) ||
            g.helpSpots
                .Where(s => s != null)
                .All(spot => visitedSpotIDs[currentScreenID].Contains(spot.spotID))
        );
        return result;
    }
    
    private TutorialHelpSpot FindNextUnvisitedSpotOnScreen()
    {
        if (currentConfig == null || currentConfig.contextGroups == null) return null;
        if (!visitedSpotIDs.ContainsKey(currentScreenID)) return null;

        foreach (var context in currentConfig.contextGroups)
        {
            if (context == null || context.muteTutorial || context.helpSpots == null) continue;

            foreach (var spot in context.helpSpots)
            {
                if (spot != null && !visitedSpotIDs[currentScreenID].Contains(spot.spotID))
                {
                    return spot;
                }
            }
        }
        return null; // Все показано
    }

    private List<RectTransform> GetContextIdleSpots(TutorialContextGroup context)
    {
        if (context != null && context.contextIdleSpots != null && context.contextIdleSpots.Count > 0)
        {
            return context.contextIdleSpots;
        }
        return currentConfig.idleSpots;
    }

    private List<string> GetContextIdleTips(TutorialContextGroup context)
    {
        if (context != null && context.contextIdleTips != null && context.contextIdleTips.Count > 0)
        {
            return context.contextIdleTips;
        }
        if (currentConfig != null && currentConfig.idleTips != null && currentConfig.idleTips.Count > 0)
        {
            return currentConfig.idleTips;
        }
        return null;
    }

    private List<string> GetGreetingListFromConfig()
    {
        var firstContext = currentConfig?.contextGroups?.FirstOrDefault(g => g != null && !g.muteTutorial);
        
        if (firstContext != null && firstContext.greetingTexts != null && firstContext.greetingTexts.Count > 0)
        {
            return firstContext.greetingTexts;
        }
        
        return new List<string> { "Привет!" };
    }
    
    // --- НОВЫЙ МЕТОД (GDD 4.3) ---
    /// <summary>
    /// Проверяет, занят ли Маскот (показывает анимацию, ждет таймер).
    /// </summary>
    public bool IsBusy()
    {
        // "Занят" означает, что "мозг" (tutorialCoroutine) выполняет какую-то задачу.
        bool busy = tutorialCoroutine != null;
        // Debug.Log($"[TutorialMascot] IsBusy() Check: {busy} (tutorialCoroutine is {(tutorialCoroutine == null ? "NULL" : "ACTIVE")})");
        return busy;
    }
    // --- КОНЕЦ НОВОГО МЕТОДА ---

    private void SaveVisitedState()
    {
        Debug.Log("[TutorialMascot] SaveVisitedState: Сохранение прогресса...");
        foreach (var pair in visitedSpotIDs)
        {
            string key = "Mascot_" + pair.Key;
            string data = string.Join(";", pair.Value);
            PlayerPrefs.SetString(key, data);
            Debug.Log($"[TutorialMascot] SaveVisitedState: Сохранено {pair.Value.Count} ID для ключа '{key}'.");
        }
        PlayerPrefsNext.Save();
    }

    private void LoadVisitedState()
    {
        // Логика перенесена в DelayedSceneLoadLogic
        Debug.Log("[TutorialMascot] LoadVisitedState: (Пусто) Загрузка будет в DelayedSceneLoadLogic.");
    }

    #endregion
}

// --- КЛАСС ДЛЯ БЕЗОПАСНОГО СОХРАНЕНИЯ ---
public static class PlayerPrefsNext
{
    private static bool isSaving = false;

    public static void Save()
    {
        if (isSaving) return;
        
        if (TutorialMascot.Instance != null && TutorialMascot.Instance.gameObject.activeInHierarchy)
        {
            isSaving = true;
            TutorialMascot.Instance.StartCoroutine(SaveRoutine());
        }
    }

    private static IEnumerator SaveRoutine()
    {
        PlayerPrefs.Save();
        yield return null;
        isSaving = false;
    }
}