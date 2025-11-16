// Файл: Assets/Scripts/UI/Tutorial/TutorialMascot.cs
// ВЕРСЯ V21 (Исправлена логика Mute-контекстов заменой FirstOrDefault на LastOrDefault)

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
    
    // Загружаемые настройки
    private float currentInitialHintDelay = 3.0f;
    private float currentIdleMessageDelay = 10.0f;
    private float currentFirstEverDelay = 2.0f;
    private float currentNextHintDelay = 5.0f;
    private float currentSceneLoadDelay = 1.0f;
    
    // Компоненты
    private AudioSource audioSource;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private RectTransform textBubbleRect; 
    
    // Состояние
    private TutorialScreenConfig currentConfig;
    private TutorialContextGroup activeContextGroup = null;
    private Dictionary<string, List<string>> visitedSpotIDs = new Dictionary<string, List<string>>();
    private bool isHidden = true;
    private Vector2 baseHoverPosition;
    private Coroutine tutorialCoroutine; // "Мозг"
    private Coroutine soundCoroutine;
    private Coroutine sheetAnimationCoroutine;
    
    // Флаги управления
    private bool isInitializing = true; 
    private Coroutine sceneLoadCoroutine; // Корутина, которая ждет 2с
    private bool isTutorialResetting = false;
    private string currentScreenID = "";
    private bool isFirstEverAppearance = true;
    private bool isFirstAppearanceThisSession = true;
    private bool isSilenced = false; // (GDD 4.2)
    
    // Состояние Idle
    private RectTransform lastUsedIdleSpot = null;
    private int lastUsedGreetingIndex = -1;
    private int lastUsedTipIndex = -1;


    #region Инициализация и Сцены

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoadedStarter;
            isFirstEverAppearance = PlayerPrefs.GetInt("Mascot_FirstEverAppearance", 0) == 0;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
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
        isInitializing = true; 
    }

    void Start()
    {
        nextButton.onClick.AddListener(OnNextButtonClicked);
        closeButton.onClick.AddListener(OnCloseButtonClicked);
        LoadVisitedState();
    }
    
    void OnSceneLoadedStarter(Scene scene, LoadSceneMode mode)
    {
        sceneLoadCoroutine = StartCoroutine(DelayedSceneLoadLogic(scene));
    }

    IEnumerator DelayedSceneLoadLogic(Scene scene)
    {
        isInitializing = true; 
        isTutorialResetting = false;
        isSilenced = false; 

        if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
        if (soundCoroutine != null) StopCoroutine(soundCoroutine);
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;
        
        currentConfig = FindObjectOfType<TutorialScreenConfig>();
        if (currentConfig != null)
        {
            currentScreenID = currentConfig.screenID;
            currentInitialHintDelay = currentConfig.initialHintDelay;
            currentIdleMessageDelay = currentConfig.idleMessageChangeDelay;
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

            isInitializing = false;
            Debug.Log($"<color=cyan>[TutorialMascot] DelayedSceneLoadLogic: Конфиг загружен. isInitializing = false. (Кнопка '?' теперь работает)</color>");

            Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Ожидание задержки загрузки сцены: {currentSceneLoadDelay}с.");
            yield return new WaitForSecondsRealtime(currentSceneLoadDelay);
            
            Debug.Log("[TutorialMascot] DelayedSceneLoadLogic: Задержка прошла. Поиск активного контекста...");

            if (currentConfig.contextGroups != null)
            {
                // --- <<< ИЗМЕНЕНИЕ V21 (1/4) >>> ---
                activeContextGroup = currentConfig.contextGroups.LastOrDefault(
                    g => g != null && g.contextPanel != null && g.contextPanel.activeInHierarchy
                );
            }

            if (tutorialCoroutine != null) 
            {
                Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: 'Мозг' (tutorialCoroutine) уже занят (вероятно, кнопкой '?'). Приветствие отменено.");
            }
            else if (activeContextGroup != null && activeContextGroup.muteTutorial)
            {
                Debug.Log("[TutorialMascot] DelayedSceneLoadLogic: Начальный контекст 'mute'. Ничего не запускаем.");
            }
            else if (activeContextGroup != null)
            {
                Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Запуск 'RunTutorialForContext' для '{activeContextGroup.contextID}'.");
                tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup, useCeremonialDelay));
            }
            else
            {
                Debug.Log($"[TutorialMascot] DelayedSceneLoadLogic: Запуск 'RunIdleLogic' (Базовый контекст).");
                tutorialCoroutine = StartCoroutine(RunIdleLogic(useCeremonialDelay));
            }
        }
        else
        {
            currentScreenID = "";
            activeContextGroup = null;
            isHidden = true;
            Debug.LogWarning("[TutorialMascot] DelayedSceneLoadLogic: TutorialScreenConfig НЕ НАЙДЕН.");
            isInitializing = false; 
        }
        
        sceneLoadCoroutine = null; 
        Debug.Log($"<color=cyan>[TutorialMascot] DelayedSceneLoadLogic: Завершено.</color>");
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedStarter;
        }
    }
    
    #endregion

    #region Главная Логика (Update)
    
    void Update()
    {
        if (currentConfig == null || isInitializing || isTutorialResetting)
        {
            return;
        }
        
        TutorialContextGroup newContext = null;
        if (currentConfig.contextGroups != null)
        {
            // --- <<< ИЗМЕНЕНИЕ V21 (2/4) >>> ---
            newContext = currentConfig.contextGroups.LastOrDefault(
                 g => g != null && g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
        }

        if (sceneLoadCoroutine != null)
        {
            if (newContext != null && activeContextGroup == null)
            {
                activeContextGroup = newContext;
                Debug.Log($"<color=yellow>[TutorialMascot] Update: Контекст '{newContext.contextID}' пойман во время 'sceneLoadCoroutine'.</color>");
            }
            return;
        }

        if (newContext != activeContextGroup)
        {
            Debug.Log($"<color=yellow>[TutorialMascot] Update: КОНТЕКСТ ИЗМЕНИЛСЯ. Старый: '{activeContextGroup?.contextID ?? "Базовый"}' -> Новый: '{newContext?.contextID ?? "Базовый"}'</color>");
            
            StopAllCoroutines();
            tutorialCoroutine = null;
            soundCoroutine = null;
            sheetAnimationCoroutine = null;
            sceneLoadCoroutine = null; 
            
            activeContextGroup = newContext;
            
            HideInternal(); 
            
            if (isSilenced)
            {
                 Debug.Log("[TutorialMascot] Update: Контекст сменился, но 'isSilenced' = true. Остаемся скрытыми.");
            }
            else if (activeContextGroup != null && activeContextGroup.muteTutorial)
            {
                Debug.Log("[TutorialMascot] Update: Новый контекст 'muteTutorial'. Остаемся скрытыми.");
            }
            else if (activeContextGroup != null)
            {
                Debug.Log("[TutorialMascot] Update: Запуск 'RunTutorialForContext' для нового контекста.");
                tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup, false)); 
            }
            else
            {
                Debug.Log("[TutorialMascot] Update: Запуск 'RunIdleLogic' (Базовый контекст).");
                tutorialCoroutine = StartCoroutine(RunIdleLogic(false));
            }
        }
        else if (tutorialCoroutine == null && !isSilenced) 
        {
            Debug.Log($"[TutorialMascot] Update: 'tutorialCoroutine' == null и НЕ 'isSilenced'. Мозг свободен. Вызов RequestNextHintSmart().");
            RequestNextHintSmart(); 
        }
        
        if (!isHidden)
        {
            rectTransform.anchoredPosition = baseHoverPosition +
                new Vector2(0, Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude);
        }
    }
    #endregion


    #region Логика Туториала и Idle (GDD 3.1, 3.2, 3.3)

    private IEnumerator RunIdleLogic(bool useCeremonial = false)
    {
        Debug.Log($"[TutorialMascot] RunIdleLogic: (Базовый) Старт. (useCeremonial = {useCeremonial})");
        if (isFirstEverAppearance)
        {
            isFirstEverAppearance = false; 
            PlayerPrefs.SetInt("Mascot_FirstEverAppearance", 1);
            PlayerPrefs.Save();
        }
        isFirstAppearanceThisSession = false;

        bool allSpotsOnScreenVisited = AreAllSpotsOnScreenVisited();

        Debug.Log("[TutorialMascot] RunIdleLogic: (Базовый) Запуск GDD 3.1 (Приветствие).");
        var visualContext = currentConfig.contextGroups.FirstOrDefault(g => g != null && !g.muteTutorial);
        
        List<RectTransform> idleSpots = GetContextIdleSpots(visualContext);
        if (idleSpots == null || idleSpots.Count == 0) 
        {
            Debug.LogWarning($"[TutorialMascot] RunIdleLogic: Не найдены Idle Spots. Не могу показать приветствие.");
            HideInternal(); 
            tutorialCoroutine = null; 
            yield break; 
        }
        
        RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];
        lastUsedIdleSpot = randomIdleSpot;
        List<string> messageList = GetGreetingListFromConfig(visualContext); 
        string message = (messageList != null && messageList.Count > 0) ? messageList[Random.Range(0, messageList.Count)] : "Привет!";
        
        yield return StartCoroutine(TeleportToSpot(
            randomIdleSpot.position, message, 
            visualContext?.greetingEmotion, 
            visualContext?.greetingPointerSprite, 
            visualContext?.greetingPointerRotation ?? 0f, 
            visualContext?.greetingPointerOffset ?? Vector2.zero, 
            useCeremonial
        ));
        
        if (!allSpotsOnScreenVisited)
        {
            Debug.Log($"[TutorialMascot] RunIdleLogic: (Базовый) Приветствие показано. Ожидание initialHintDelay: {currentInitialHintDelay}с.");
            yield return new WaitForSecondsRealtime(currentInitialHintDelay);
            
            Debug.Log("[TutorialMascot] RunIdleLogic: (Базовый) GDD 3.1 завершен. 'Мозг' освобожден (Update() вызовет 1-ю подсказку).");
            tutorialCoroutine = null;
        }
        else
        {
            Debug.Log("[TutorialMascot] RunIdleLogic: (Базовый) Все подсказки просмотрены. Немедленный переход в GDD 3.3 (Цикл Idle/Tips).");
            tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(null)); 
            yield break; 
        }
    }

    private IEnumerator RunTutorialForContext(TutorialContextGroup context, bool useCeremonial = false)
    {
        Debug.Log($"[TutorialMascot] RunTutorialForContext: Старт для '{context.contextID}'. (useCeremonial = {useCeremonial})");

        Debug.Log($"[TutorialMascot] RunTutorialForContext: Показ Приветствия для '{context.contextID}'.");
        
        List<RectTransform> idleSpots = GetContextIdleSpots(context);
        if (idleSpots == null || idleSpots.Count == 0)
        {
             Debug.LogWarning($"[TutorialMascot] RunTutorialForContext: Не найдены Idle Spots для '{context.contextID}'. Не могу показать приветствие.");
             HideInternal();
             tutorialCoroutine = null; 
             yield break;
        }
        
        RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];
        
        string greeting = "Привет!";
        if (context.greetingTexts != null && context.greetingTexts.Count > 0)
        {
            int newIndex = Random.Range(0, context.greetingTexts.Count);
            if (context.greetingTexts.Count > 1 && newIndex == lastUsedGreetingIndex)
            {
                newIndex = (newIndex + 1) % context.greetingTexts.Count;
            }
            greeting = context.greetingTexts[newIndex];
            lastUsedGreetingIndex = newIndex;
        }

        yield return StartCoroutine(TeleportToSpot(
             randomIdleSpot.position,
            greeting,
            context.greetingEmotion, 
            context.greetingPointerSprite,
            context.greetingPointerRotation,
            context.greetingPointerOffset,
            useCeremonial
        ));
        
        bool allSpotsInContextVisited = AreAllSpotsInCurrentContextVisited();

        if (allSpotsInContextVisited)
        {
            Debug.Log($"[TutorialMascot] RunTutorialForContext: Все подсказки для '{context.contextID}' просмотрены. Переход в RunIdleLogicForContext (Tips-loop).");
            tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(context));
            yield break; 
        }
        else
        {
            Debug.Log($"[TutorialMascot] RunTutorialForContext: Приветствие показано. Ожидание InitialHintDelayTimer ({currentInitialHintDelay}с) перед показом 1-й подсказки.");
            yield return new WaitForSecondsRealtime(currentInitialHintDelay);
            
            Debug.Log($"[TutorialMascot] RunTutorialForContext: InitialHintDelayTimer завершен. 'Мозг' освобожден (Update() вызовет 1-ю подсказку).");
            tutorialCoroutine = null; 
        }
    }

    private IEnumerator RunIdleLogicForContext(TutorialContextGroup context)
    {
        string contextID = (context != null) ? context.contextID : null;
        Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Старт (режим Idle) для '{contextID ?? "Базовый"}'.");

        while (activeContextGroup == context && !isSilenced)
        {
            List<RectTransform> idleSpots = GetContextIdleSpots(context);
            if (idleSpots == null || idleSpots.Count == 0)
            {
                 Debug.LogWarning($"[TutorialMascot] RunIdleLogicForContext: Не найдены Idle Spots для '{contextID ?? "Базовый"}'. Прерывание Idle-цикла.");
                 HideInternal();
                 tutorialCoroutine = null;
                 yield break;
            }
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
                messageList = GetGreetingListFromConfig(context); 
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
            
            Sprite emotion = (context != null) ? context.greetingEmotion : GetGreetingListFromConfig(null).Any() ? currentConfig.contextGroups.First(g => g!=null && !g.muteTutorial).greetingEmotion : null;
            Sprite pointer = (context != null) ? context.greetingPointerSprite : GetGreetingListFromConfig(null).Any() ? currentConfig.contextGroups.First(g => g!=null && !g.muteTutorial).greetingPointerSprite : null;
            float pointerRot = (context != null) ? context.greetingPointerRotation : GetGreetingListFromConfig(null).Any() ? currentConfig.contextGroups.First(g => g!=null && !g.muteTutorial).greetingPointerRotation : 0f;
            Vector2 pointerOffset = (context != null) ? context.greetingPointerOffset : GetGreetingListFromConfig(null).Any() ? currentConfig.contextGroups.First(g => g!=null && !g.muteTutorial).greetingPointerOffset : Vector2.zero;

            yield return StartCoroutine(TeleportToSpot(
                randomIdleSpot.position,
                message,
                emotion, 
                pointer,
                pointerRot,
                pointerOffset,
                false 
            ));

            Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Ожидание Idle ({currentIdleMessageDelay}с).");
            yield return new WaitForSecondsRealtime(currentIdleMessageDelay);

            if (activeContextGroup == context) 
            {
                Debug.Log("[TutorialMascot] RunIdleLogicForContext: Контекст не изменился. Плавное скрытие.");
                yield return StartCoroutine(Fade(0f, fadeDuration)); 
            }
        }
        Debug.Log($"[TutorialMascot] RunIdleLogicForContext: Цикл завершен (контекст изменился или 'уснул'). 'Мозг' освобожден.");
        tutorialCoroutine = null;
    }
    
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
            visitedSpotIDs[currentScreenID] = new List<string>();
        }
        
        if (!visitedSpotIDs[currentScreenID].Contains(spot.spotID))
        {
            Debug.Log($"[TutorialMascot] ShowSpecificSpotAndManageCoroutine: '{spot.spotID}' отмечается как просмотренный.");
            visitedSpotIDs[currentScreenID].Add(spot.spotID);
            SaveVisitedState();
        }
        
        yield return StartCoroutine(TeleportToSpot(
            (Vector2)spot.targetElement.position + spot.mascotPositionOffset,
            spot.helpText,
            spot.mascotEmotionSprite,
            spot.pointerSprite,
            spot.pointerRotation,
            spot.pointerPositionOffset,
            false 
        ));
        
        Debug.Log($"[TutorialMascot] ShowSpecificSpotAndManageCoroutine: Ожидание NextHintDelayTimer ({currentNextHintDelay}с).");
        yield return new WaitForSecondsRealtime(currentNextHintDelay);
        
        Debug.Log($"[TutorialMascot] ShowSpecificSpotAndManageCoroutine: NextHintDelayTimer завершен. 'Мозг' освобожден (Update() вызовет GDD 3.2-auto).");
        tutorialCoroutine = null;
        
        if (isTutorialResetting)
        {
            isTutorialResetting = false;
        }
    }

    #endregion

    #region Управление (Нажатия кнопок)

    private void OnNextButtonClicked()
    {
        if (isHidden) return;
        Debug.Log("<color=cyan>[TutorialMascot] OnNextButtonClicked: Клик по 'Next'.</color>");
        PlaySound(mascotClickSound);
        
        Debug.Log("[TutorialMascot] OnNextButtonClicked: StopAllCoroutines().");
        StopAllCoroutines(); 
        sceneLoadCoroutine = null; 
        tutorialCoroutine = null; 
        isSilenced = false;

        RequestNextHintSmart();
    }
    
    private void OnCloseButtonClicked()
    {
        if (isHidden) return; 
        Debug.Log("<color=red>[TutorialMascot] OnCloseButtonClicked: Клик по 'X'.</color>");
        PlaySound(closeClickSound);
        
        Debug.Log("[TutorialMascot] OnCloseButtonClicked: StopAllCoroutines().");
        StopAllCoroutines();
        sceneLoadCoroutine = null; 
        
        Debug.Log("[TutorialMascot] OnCloseButtonClicked: HideInternal().");
        HideInternal(); 

        isSilenced = true;
        Debug.Log("[TutorialMascot] OnCloseButtonClicked: Установка isSilenced = true. 'Мозг' остановлен.");
        tutorialCoroutine = null; 
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
        Debug.Log($"[TutorialMascot] TeleportToSpot: Старт. Позиция: {worldPosition}, Текст: '{(text != null && text.Length > 10 ? text.Substring(0, 10) : text)}...' (isCeremonial = {isCeremonial})");
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        if (soundCoroutine != null) StopCoroutine(soundCoroutine); 

        float duration = isCeremonial ? ceremonialFadeDuration : fadeDuration;

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
    
    public void RequestNextHintSmart()
    {
        if (isInitializing)
        {
            Debug.LogWarning($"[TutorialMascot] RequestNextHintSmart: Вызван во время isInitializing. Игнорирую.");
            return;
        }
        
        if (IsBusy())
        {
            Debug.LogWarning("[TutorialMascot] RequestNextHintSmart: Вызван, когда 'мозг' УЖЕ БЫЛ ЗАНЯТ. Игнорирую.");
            return;
        }

        Debug.Log("<color=cyan>[TutorialMascot] RequestNextHintSmart: Старт.</color>");
        
        isSilenced = false; 
        
        if (sceneLoadCoroutine != null)
        {
            StopCoroutine(sceneLoadCoroutine);
            sceneLoadCoroutine = null;
            Debug.Log("[TutorialMascot] RequestNextHintSmart: Корутина 'DelayedSceneLoadLogic' остановлена (взят ручной контроль).");
        }
        
        tutorialCoroutine = StartCoroutine(RequestNextHintSmart_Coroutine());
    }

    private IEnumerator RequestNextHintSmart_Coroutine()
    {
        if (soundCoroutine != null) StopCoroutine(soundCoroutine);
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        
        if (activeContextGroup == null && currentConfig != null && currentConfig.contextGroups != null)
        {
            // --- <<< ИЗМЕНЕНИЕ V21 (3/4) >>> ---
             activeContextGroup = currentConfig.contextGroups.LastOrDefault(
                g => g != null && g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
             Debug.Log($"[Mascot Brain] Контекст был null. Принудительный поиск -> '{activeContextGroup?.contextID ?? "Базовый"}'.");
        }
        
        if (activeContextGroup != null)
        {
            TutorialHelpSpot nextSpotInContext = FindNextUnvisitedSpotInContext(activeContextGroup);
            if (nextSpotInContext != null)
            {
                Debug.Log($"[Mascot Brain] Найдена подсказка в КОНТЕКСТЕ: '{nextSpotInContext.spotID}'.");
                yield return StartCoroutine(ShowSpecificSpotAndManageCoroutine(nextSpotInContext));
            }
            else
            {
                Debug.Log($"[Mascot Brain] Подсказки в контексте '{activeContextGroup.contextID}' закончились. Запуск Idle (Контекст).");
                yield return StartCoroutine(RunIdleLogicForContext(activeContextGroup));
            }
        }
        else
        {
            TutorialHelpSpot nextSpotOnScreen = FindNextUnvisitedSpotOnScreen();
            if (nextSpotOnScreen != null)
            {
                Debug.Log($"[Mascot Brain] (Базовый) Найдена подсказка на ЭКРАНЕ: '{nextSpotOnScreen.spotID}'.");
                yield return StartCoroutine(ShowSpecificSpotAndManageCoroutine(nextSpotOnScreen));
            }
            else
            {
                Debug.Log("[Mascot Brain] (Базовый) Подсказки на экране закончились. Запуск Idle (Базовый).");
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
            !string.IsNullOrEmpty(spot.spotID) && 
            !visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );
    }
    
    public void ResetCurrentScreenTutorial()
    {
        if (isInitializing)
        {
            Debug.LogWarning("[TutorialMascot] ResetCurrentScreenTutorial: Вызван во время isInitializing. Игнорирую.");
            return;
        }
        
        if (isTutorialResetting)
        {
             Debug.LogWarning("[TutorialMascot] ResetCurrentScreenTutorial: Вызван во время сброса. Игнорирую.");
            return;
        }

        Debug.Log($"<color=orange>[TutorialMascot] ResetCurrentScreenTutorial: Сброс прогресса для '{currentScreenID}'.</color>");
        isTutorialResetting = true; 
        isSilenced = false; 

        if (string.IsNullOrEmpty(currentScreenID))
        {
            Debug.LogError("[TutorialMascot] ResetCurrentScreenTutorial: currentScreenID пуст. Сброс невозможен.");
            isTutorialResetting = false;
            return;
        }
        
        if (visitedSpotIDs.ContainsKey(currentScreenID))
        {
            if (activeContextGroup != null && activeContextGroup.helpSpots != null)
            {
                Debug.Log($"[TutorialMascot] ResetCurrentScreenTutorial: Сброс контекста '{activeContextGroup.contextID}'.");
                
                List<string> spotsInThisContext = activeContextGroup.helpSpots
                    .Where(s => s != null && !string.IsNullOrEmpty(s.spotID))
                    .Select(s => s.spotID)
                    .ToList();
                
                int removedCount = visitedSpotIDs[currentScreenID].RemoveAll(visitedID => spotsInThisContext.Contains(visitedID));
                Debug.Log($"[TutorialMascot] ResetCurrentScreenTutorial: Удалено {removedCount} посещенных подсказок, принадлежащих '{activeContextGroup.contextID}'.");
            }
            else
            {
                Debug.Log("[TutorialMascot] ResetCurrentScreenTutorial: Сброс в 'Базовом' контексте. Очистка ВСЕХ подсказок для экрана '{currentScreenID}'.");
                visitedSpotIDs[currentScreenID].Clear();
            }
        }
        
        SaveVisitedState();
        
        StopAllCoroutines();
        sceneLoadCoroutine = null; 
        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;
        
        HideInternal();

        if (currentConfig != null && currentConfig.contextGroups != null)
        {
            // --- <<< ИЗМЕНЕНИЕ V21 (4/4) >>> ---
            activeContextGroup = currentConfig.contextGroups.LastOrDefault(
                g => g != null && g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
        }
        else
        {
             activeContextGroup = null;
        }

        lastUsedIdleSpot = null;
        lastUsedGreetingIndex = -1;
        lastUsedTipIndex = -1;
        
        Debug.Log("[TutorialMascot] ResetCurrentScreenTutorial: Принудительный запуск 'мозга' для показа Приветствия.");
        
        if (activeContextGroup != null)
        {
            tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup, false));
        }
        else
        {
            tutorialCoroutine = StartCoroutine(RunIdleLogic(false));
        }
    }

    
    public bool AreAllSpotsInCurrentContextVisited()
    {
        List<TutorialHelpSpot> spotsToCkeck;

        if (activeContextGroup != null)
        {
            spotsToCkeck = activeContextGroup.helpSpots;
        }
        else
        {
            if (currentConfig == null || currentConfig.contextGroups == null) return true;
            spotsToCkeck = currentConfig.contextGroups
                .Where(g => g != null && !g.muteTutorial && g.helpSpots != null)
                .SelectMany(g => g.helpSpots)
                .ToList();
        }

        if (spotsToCkeck == null || spotsToCkeck.Count == 0)
        {
            return true; 
        }
        if (!visitedSpotIDs.ContainsKey(currentScreenID))
        {
            return false; 
        }
            
        bool result = spotsToCkeck
            .Where(s => s != null && !string.IsNullOrEmpty(s.spotID)) 
            .All(spot => visitedSpotIDs[currentScreenID].Contains(spot.spotID));
        
        return result;
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
                .Where(s => s != null && !string.IsNullOrEmpty(s.spotID)) 
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
                if (spot != null && !string.IsNullOrEmpty(spot.spotID) && !visitedSpotIDs[currentScreenID].Contains(spot.spotID))
                {
                    return spot;
                }
            }
        }
        return null; 
    }

    private List<RectTransform> GetContextIdleSpots(TutorialContextGroup context)
    {
        List<RectTransform> validSpots = null;

        if (context != null && context.contextIdleSpots != null && context.contextIdleSpots.Count > 0)
        {
            validSpots = context.contextIdleSpots.Where(s => s != null).ToList();
            if (validSpots.Count > 0)
            {
                return validSpots;
            }
        }
        
        if (currentConfig != null && currentConfig.idleSpots != null && currentConfig.idleSpots.Count > 0)
        {
            validSpots = currentConfig.idleSpots.Where(s => s != null).ToList();
            if (validSpots.Count > 0)
            {
                return validSpots;
            }
        }
        
        Debug.LogWarning($"[TutorialMascot] GetContextIdleSpots: Не найдено НИ ОДНОГО валидного (не null) IdleSpot.");
        return new List<RectTransform>();
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

    private List<string> GetGreetingListFromConfig(TutorialContextGroup context)
    {
        if (context != null && context.greetingTexts != null && context.greetingTexts.Count > 0)
        {
            return context.greetingTexts;
        }
        
        var firstContext = currentConfig?.contextGroups?.FirstOrDefault(g => g != null && !g.muteTutorial);
        
        // --- <<< ИСПРАВЛЕНИЕ V20 (Опечатка) >>> ---
        if (firstContext != null && firstContext.greetingTexts != null && firstContext.greetingTexts.Count > 0)
        {
            return firstContext.greetingTexts;
        }
        
        return new List<string> { "Привет!" };
    }
    
    public bool IsBusy()
    {
        // "Занят" = "мозг" думает
        bool busy = (tutorialCoroutine != null);
        return busy;
    }

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