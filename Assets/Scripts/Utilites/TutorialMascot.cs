// Файл: Assets/Scripts/UI/Tutorial/TutorialMascot.cs (ОТКАТ к v10 + ФИКС v12)
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
    
    // --- ВОЗВРАЩАЕМ 'class' (он может быть null) ---
    private TutorialContextGroup activeContextGroup = null; 
    
    private Dictionary<string, List<string>> visitedSpotIDs = new Dictionary<string, List<string>>();
    private bool isHidden = true;
    private Vector2 baseHoverPosition;
    private Coroutine tutorialCoroutine; // "Мозг"
    private Coroutine soundCoroutine;
    private Coroutine sheetAnimationCoroutine;
    private Coroutine initialHintDelayCoroutine;
    private Coroutine nextHintDelayCoroutine;
    private bool isSceneLoadLogicRunning = false;
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
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }
            
            SceneManager.sceneLoaded += OnSceneLoadedStarter;
            isFirstEverAppearance = PlayerPrefs.GetInt("Mascot_FirstEverAppearance", 0) == 0;
        }
        else if (Instance != this)
        {
            if (transform.parent != null) Destroy(transform.parent.gameObject);
            else Destroy(gameObject);
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
        if (isSceneLoadLogicRunning) return;
        StartCoroutine(DelayedSceneLoadLogic(scene));
    }

    IEnumerator DelayedSceneLoadLogic(Scene scene)
    {
        isSceneLoadLogicRunning = true;
        isTutorialResetting = false;

        // --- ФИКС "НЕИСЧЕЗАЮЩЕЙ ПАПКИ" ---
        // (v12) Останавливаем ВСЕ корутины при загрузке сцены
        StopAllCoroutines();
        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;
        initialHintDelayCoroutine = null;
        nextHintDelayCoroutine = null;
        // --- КОНЕЦ ФИКСА ---

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

            yield return new WaitForSecondsRealtime(currentSceneLoadDelay);
            
            // --- ФИКС v12 ---
            // 'tutorialCoroutine' здесь = null, 'Update()' его подхватит
            // и запустит RunIdleLogic
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
        }
    }
    
    #endregion

    #region Главная Логика (Update)
    
    // --- ФИКС v12 ---
    // Логика 'Update' возвращена к v12 (она чинит "неисчезающую папку"
    // и "застревание" при смене контекста)
    void Update()
    {
        if (!isHidden)
        {
            rectTransform.anchoredPosition = baseHoverPosition +
                new Vector2(0, Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude);
        }

        if (currentConfig == null || isSceneLoadLogicRunning || isTutorialResetting)
        {
            return;
        }
        
        TutorialContextGroup newContext = null;
        if (currentConfig.contextGroups != null)
        {
            newContext = currentConfig.contextGroups.FirstOrDefault(
                 g => g != null && g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
        }

        if (newContext != activeContextGroup)
        {
            Debug.Log($"<color=yellow>[TutorialMascot] Update: КОНТЕКСТ ИЗМЕНИЛСЯ. Старый: '{activeContextGroup?.contextID ?? "null"}' -> Новый: '{newContext?.contextID ?? "null"}'</color>");
            
            StopAllCoroutines();
            
            tutorialCoroutine = null;
            soundCoroutine = null;
            sheetAnimationCoroutine = null;
            initialHintDelayCoroutine = null;
            nextHintDelayCoroutine = null;
            
            activeContextGroup = newContext;
            isCeremonialAppearance = false;

            // --- ФИКС v12 (чтобы папка исчезала при ЛЮБОЙ смене) ---
            // Мы *сначала* прячем папку, *потом* решаем, что делать.
            HideInternal(); // Немедленно прячем (без fade-out)
            
            if (activeContextGroup != null && activeContextGroup.muteTutorial)
            {
                // Контекст "тихий". Маскот уже скрыт.
                Debug.Log("[TutorialMascot] Update: Новый контекст 'muteTutorial'. Остаемся скрытыми.");
            }
            else if (activeContextGroup != null)
            {
                // Новый НЕ "тихий" контекст.
                Debug.Log("[TutorialMascot] Update: Запуск 'RunTutorialForContext'.");
                tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup));
            }
            else
            {
                // Контекст стал null (Idle).
                Debug.Log("[TutorialMascot] Update: Запуск 'RunIdleLogic'.");
                tutorialCoroutine = StartCoroutine(RunIdleLogic(false));
            }
        }
        else if (tutorialCoroutine == null)
        {
            // --- КОНТЕКСТ НЕ ИЗМЕНИЛСЯ, НО "МОЗГ" СВОБОДЕН ---
            // (Это происходит, когда таймер/кнопка освободили 'tutorialCoroutine')
            Debug.Log($"[TutorialMascot] Update: 'tutorialCoroutine' был null. Запуск RequestNextHintSmart() для контекста '{activeContextGroup?.contextID ?? "null"}'.");
            RequestNextHintSmart();
        }
    }

    #endregion

    #region Логика Туториала и Idle

    private IEnumerator RunIdleLogic(bool useCeremonial = false)
    {
        if (isFirstEverAppearance)
        {
            isFirstEverAppearance = false; 
            PlayerPrefs.SetInt("Mascot_FirstEverAppearance", 1);
            PlayerPrefs.Save();
        }
        isFirstAppearanceThisSession = false;

        while (activeContextGroup == null)
        {
            var visualContext = currentConfig.contextGroups.FirstOrDefault(g => g != null && !g.muteTutorial);
            if (visualContext == null) 
            {
                HideInternal(); 
                tutorialCoroutine = null; 
                yield break; 
            }

            List<RectTransform> idleSpots = GetContextIdleSpots(visualContext);
            if (idleSpots == null || idleSpots.Count == 0)
            {
                HideInternal(); 
                tutorialCoroutine = null; 
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
                messageList = GetContextIdleTips(visualContext);
                isUsingGreetings = false;

                if (messageList == null || messageList.Count == 0)
                {
                    messageList = visualContext.greetingTexts;
                    isUsingGreetings = true;
                }
            }
            else
            {
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

            yield return StartCoroutine(TeleportToSpot(
                randomIdleSpot.position,
                message,
                visualContext.greetingEmotion, 
                visualContext.greetingPointerSprite,
                visualContext.greetingPointerRotation,
                visualContext.greetingPointerOffset,
                useCeremonial 
            ));

            useCeremonial = false; 

            if (!allSpotsOnScreenVisited)
            {
                if (initialHintDelayCoroutine != null) StopCoroutine(initialHintDelayCoroutine);
                initialHintDelayCoroutine = StartCoroutine(InitialHintDelayTimer());
                yield return initialHintDelayCoroutine; // --- ФИКС v18 (Ждем таймер)
            }
            else
            {
                yield return new WaitForSecondsRealtime(currentIdleMessageDelay);
            }
            
            if (activeContextGroup == null)
            {
                yield return StartCoroutine(Fade(0f, fadeDuration)); 
            }
        }
        
        tutorialCoroutine = null; 
    }

    private IEnumerator RunTutorialForContext(TutorialContextGroup context)
    {
        bool allSpotsInContextVisited = context.helpSpots.All(spot => 
            spot == null || 
            visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );

        if (allSpotsInContextVisited)
        {
            tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(context));
            yield break;
        }

        bool isFirstTimeInContext = !context.helpSpots.Any(spot => 
            spot != null && 
            visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );

        if (isFirstTimeInContext)
        {
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

            if (initialHintDelayCoroutine != null) StopCoroutine(initialHintDelayCoroutine);
            initialHintDelayCoroutine = StartCoroutine(InitialHintDelayTimer());
            yield return initialHintDelayCoroutine; // --- ФИКС v18 (Ждем таймер)
            
            // --- ФИКС v18: После того, как приветствие и таймер отработали, ---
            // --- "мозг" освобождается, чтобы Update() мог вызвать RequestNextHintSmart() ---
            tutorialCoroutine = null;
        }
        else
        {
            tutorialCoroutine = null; 
            RequestNextHintSmart(); 
        }
    }

    private IEnumerator RunIdleLogicForContext(TutorialContextGroup context)
    {
        while (activeContextGroup != null && activeContextGroup.contextID == context.contextID)
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

            yield return StartCoroutine(TeleportToSpot(
                randomIdleSpot.position,
                message,
                context.greetingEmotion, 
                context.greetingPointerSprite,
                context.greetingPointerRotation,
                context.greetingPointerOffset,
                false 
            ));

            yield return new WaitForSecondsRealtime(currentIdleMessageDelay);

            if (activeContextGroup != null && activeContextGroup.contextID == context.contextID)
            {
                yield return StartCoroutine(Fade(0f, fadeDuration)); 
            }
        }

        tutorialCoroutine = null; 
    }

    // --- ФИКС v18: Таймеры больше НЕ освобождают 'tutorialCoroutine' ---
    private IEnumerator InitialHintDelayTimer()
    {
        yield return new WaitForSecondsRealtime(currentInitialHintDelay);
        initialHintDelayCoroutine = null; 
        // tutorialCoroutine = null; // <-- УБРАНО
    }
    
    private IEnumerator NextHintDelayTimer()
    {
        yield return new WaitForSecondsRealtime(currentNextHintDelay);
        nextHintDelayCoroutine = null;
        // tutorialCoroutine = null; // <-- УБРАНО
    }
    
    // --- ФИКС v18: Эта корутина теперь ждет 'NextHintDelayTimer' ---
    private IEnumerator ShowSpecificSpotAndManageCoroutine(TutorialHelpSpot spot)
    {
        // --- ФИКС v18: Повтор подсказки (отмечаем сразу) ---
        if (!visitedSpotIDs[currentScreenID].Contains(spot.spotID))
        {
            visitedSpotIDs[currentScreenID].Add(spot.spotID);
            SaveVisitedState();
        }
        
        // Эта корутина запускает TeleportToSpot
        yield return StartCoroutine(TeleportToSpot(
            (Vector2)spot.targetElement.position + spot.mascotPositionOffset,
            spot.helpText,
            spot.mascotEmotionSprite,
            spot.pointerSprite,
            spot.pointerRotation,
            spot.pointerPositionOffset, 
            false 
        ));
        
        // 'TeleportToSpot' запускает 'NextHintDelayTimer'
        // Мы ждем, пока этот таймер завершится
        if (nextHintDelayCoroutine != null)
        {
            yield return nextHintDelayCoroutine;
        }
        
        // Только теперь "мозг" освобождается
        tutorialCoroutine = null;
    }


    private void ShowSpecificSpot(TutorialHelpSpot spot)
    {
        if (spot.targetElement == null)
        {
            Debug.LogError($"<color=red>[TutorialMascot] ShowSpecificSpot: У подсказки '{spot.spotID}' не назначен 'targetElement'!</color>");
            tutorialCoroutine = null; 
            return;
        }

        // --- ФИКС v18: Повтор подсказки (отмечаем сразу) ---
        if (!visitedSpotIDs[currentScreenID].Contains(spot.spotID))
        {
            visitedSpotIDs[currentScreenID].Add(spot.spotID);
            SaveVisitedState();
        }

        if (nextHintDelayCoroutine != null) StopCoroutine(nextHintDelayCoroutine);

        // 'TeleportToSpot' запустит 'NextHintDelayTimer', который освободит 'tutorialCoroutine'
        StartCoroutine(TeleportToSpot(
            (Vector2)spot.targetElement.position + spot.mascotPositionOffset,
            spot.helpText,
            spot.mascotEmotionSprite,
            spot.pointerSprite,
            spot.pointerRotation,
            spot.pointerPositionOffset, 
            false 
        ));
    }

    #endregion

    #region Управление (Нажатия кнопок)

    // --- ФИКС v18: Кнопки просто останавливают ВСЁ и освобождают "мозг" ---
    private void OnNextButtonClicked()
    {
        if (isHidden) return;
        PlaySound(mascotClickSound);
        
        StopAllCoroutines();
        tutorialCoroutine = null; 
    }
    
    private void OnCloseButtonClicked()
    {
        if (isHidden) return; 
        PlaySound(closeClickSound);
        
        StopAllCoroutines();
        
        HideInternal(); 
        tutorialCoroutine = null; 
    }
    
    private void HideInternal()
    {
        if (!isHidden)
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
        }
        isHidden = true;
    }

    #endregion

    #region Эффекты (Звук / Появление)

    private IEnumerator TeleportToSpot(Vector2 worldPosition, string text, Sprite emotion, Sprite pointer, float pointerRot, Vector2 pointerOffset, bool isCeremonial = false)
    {
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        if (soundCoroutine != null) StopCoroutine(soundCoroutine); 
        
        if (nextHintDelayCoroutine != null)
        {
            StopCoroutine(nextHintDelayCoroutine);
            nextHintDelayCoroutine = null;
        }

        float duration = isCeremonial ? ceremonialFadeDuration : fadeDuration;
        isCeremonialAppearance = isCeremonial; 

        if (!isHidden)
        {
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
        
        yield return StartCoroutine(Fade(1f, duration)); 
        
        if (textBubbleRect != null && calculatedSheetHeight > 0)
        {
            soundCoroutine = StartCoroutine(PlayBeepLoop());
            
            sheetAnimationCoroutine = StartCoroutine(AnimateSheetHeight(calculatedSheetHeight, calculatedHeightSteps));
            yield return sheetAnimationCoroutine; 
            
            if (soundCoroutine != null) StopCoroutine(soundCoroutine);
            soundCoroutine = null;
            PlaySound(textFinalBeepSound);
        }
        
        if (isTutorialResetting)
        {
            isTutorialResetting = false;
        }
        
        // --- ФИКС v18: Запускаем таймер в *конце* показа ---
        if (nextHintDelayCoroutine != null) StopCoroutine(nextHintDelayCoroutine);
        nextHintDelayCoroutine = StartCoroutine(NextHintDelayTimer());
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
        tutorialCoroutine = null;
        
        if (soundCoroutine != null) StopCoroutine(soundCoroutine);
        if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        if (initialHintDelayCoroutine != null) StopCoroutine(initialHintDelayCoroutine);
        if (nextHintDelayCoroutine != null) StopCoroutine(nextHintDelayCoroutine);

        if (activeContextGroup != null)
        {
            TutorialHelpSpot nextSpotInContext = FindNextUnvisitedSpotInContext(activeContextGroup);

            if (nextSpotInContext != null)
            {
                tutorialCoroutine = StartCoroutine(ShowSpecificSpotAndManageCoroutine(nextSpotInContext));
            }
            else
            {
                tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(activeContextGroup));
            }
        }
        else
        {
            TutorialHelpSpot nextSpotOnScreen = FindNextUnvisitedSpotOnScreen();

            if (nextSpotOnScreen != null)
            {
                tutorialCoroutine = StartCoroutine(ShowSpecificSpotAndManageCoroutine(nextSpotOnScreen));
            }
            else
            {
                tutorialCoroutine = StartCoroutine(RunIdleLogic(false));
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
    

    public void ResetCurrentScreenTutorial()
    {
        isTutorialResetting = true; 

        if (string.IsNullOrEmpty(currentScreenID))
        {
            isTutorialResetting = false; 
            return;
        }
        
        if (visitedSpotIDs.ContainsKey(currentScreenID))
        {
            visitedSpotIDs[currentScreenID].Clear();
        }
        SaveVisitedState(); 
        
        StopAllCoroutines();
        tutorialCoroutine = null;
        soundCoroutine = null;
        sheetAnimationCoroutine = null;
        initialHintDelayCoroutine = null;
        nextHintDelayCoroutine = null;
        
        HideInternal();

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

        lastUsedIdleSpot = null;
        lastUsedGreetingIndex = -1;
        lastUsedTipIndex = -1;
        
        // 'tutorialCoroutine' = null, 'Update()' подхватит
    }
    
    public bool AreAllSpotsInCurrentContextVisited()
    {
        if (activeContextGroup != null)
        {
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

    
    private void SaveVisitedState()
    {
        foreach (var pair in visitedSpotIDs)
        {
            string key = "Mascot_" + pair.Key; 
            string data = string.Join(";", pair.Value);
            PlayerPrefs.SetString(key, data);
        }
        PlayerPrefsNext.Save();
    }

    private void LoadVisitedState()
    {
        // Логика перенесена в DelayedSceneLoadLogic
    }

    #endregion
}

// --- НОВЫЙ КЛАСС ДЛЯ БЕЗОПАСНОГО СОХРАНЕНИЯ ---
// Помести это ВНЕ класса TutorialMascot, но в том же файле.
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