// Файл: Assets/Scripts/Utilities/TutorialMascot.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Utilities
{
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
        private Coroutine tutorialCoroutine; 
        private Coroutine soundCoroutine;
        private Coroutine sheetAnimationCoroutine;
    
        // Флаги управления
        private bool isInitializing = true; 
        private Coroutine sceneLoadCoroutine; 
        private bool isTutorialResetting = false;
        private string currentScreenID = "";
        private bool isFirstEverAppearance = true;
        private bool isFirstAppearanceThisSession = true;
        private bool isSilenced = false; 
    
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
                // Debug.Log($"[TutorialMascot] Awake: isFirstEverAppearance = {isFirstEverAppearance}");
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

        // --- ИСПРАВЛЕНИЕ: Добавлен OnEnable ---
        // Если объект был выключен в Awake (через MainMenuDialogueTrigger),
        // то при включении нужно запустить логику, которую мы пропустили.
        void OnEnable()
        {
            // Если мы не инициализированы или если корутины не работают
            if (sceneLoadCoroutine == null && tutorialCoroutine == null)
            {
                // Запускаем логику загрузки для текущей сцены
                // Debug.Log("[TutorialMascot] OnEnable: Объект включен, запускаем логику сцены.");
                sceneLoadCoroutine = StartCoroutine(DelayedSceneLoadLogic(SceneManager.GetActiveScene()));
            }
        }
    
        void OnSceneLoadedStarter(Scene scene, LoadSceneMode mode)
        {
            // --- ИСПРАВЛЕНИЕ: Проверка активности ---
            // Если объект выключен (например, его выключил диалог), мы НЕ запускаем корутину, чтобы не крашнуться.
            // Когда объект включат обратно, сработает OnEnable выше.
            if (!this.gameObject.activeInHierarchy) 
            {
                // Debug.Log("[TutorialMascot] OnSceneLoadedStarter: Объект выключен, пропускаем запуск корутины (ждет OnEnable).");
                return;
            }

            if(sceneLoadCoroutine != null) StopCoroutine(sceneLoadCoroutine);
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
        
            currentConfig = FindFirstObjectByType<TutorialScreenConfig>();
            // Debug.Log($"[TutorialMascot] currentConfig = {(currentConfig != null ? "НАЙДЕН" : "NULL")}");
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
                    // Debug.Log($"[TutorialMascot] Первый запуск игры, задержка: {currentSceneLoadDelay}с");
                }
                else if (isFirstAppearanceThisSession)
                {
                    currentSceneLoadDelay = currentConfig.firstEverAppearanceDelay;
                    useCeremonialDelay = true;
                    // Debug.Log($"[TutorialMascot] Первый запуск в сессии, задержка: {currentSceneLoadDelay}с");
                }
                else
                {
                    currentSceneLoadDelay = currentConfig.sceneLoadDelay;
                    useCeremonialDelay = false;
                    // Debug.Log($"[TutorialMascot] Обычный запуск, задержка: {currentSceneLoadDelay}с");
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

                // Debug.Log($"[TutorialMascot] Ожидание задержки: {currentSceneLoadDelay}с.");
                yield return new WaitForSecondsRealtime(currentSceneLoadDelay);
                // Debug.Log("[TutorialMascot] Задержка прошла, показываем маскота");
            
                if (currentConfig.contextGroups != null)
                {
                    activeContextGroup = currentConfig.contextGroups.LastOrDefault(g => IsContextActive(g));
                }

                if (tutorialCoroutine != null) 
                {
                    // Занят
                }
                else if (activeContextGroup != null && activeContextGroup.muteTutorial)
                {
                    // Mute
                }
                else if (activeContextGroup != null)
                {
                    tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup, useCeremonialDelay));
                }
                else
                {
                    tutorialCoroutine = StartCoroutine(RunIdleLogic(useCeremonialDelay));
                }
            }
            else
            {
                currentScreenID = "";
                activeContextGroup = null;
                isHidden = true;
                isInitializing = false; 
            }
        
            sceneLoadCoroutine = null; 
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
                newContext = currentConfig.contextGroups.LastOrDefault(g => IsContextActive(g));
            }

            if (sceneLoadCoroutine != null)
            {
                if (newContext != null && activeContextGroup == null)
                {
                    activeContextGroup = newContext;
                }
                return;
            }

            if (newContext != activeContextGroup)
            {
                StopAllCoroutines();
                tutorialCoroutine = null;
                soundCoroutine = null;
                sheetAnimationCoroutine = null;
                sceneLoadCoroutine = null; 
            
                activeContextGroup = newContext;
            
                HideInternal(); 
            
                if (isSilenced)
                {
                    // Остаемся скрытыми
                }
                else if (activeContextGroup != null && activeContextGroup.muteTutorial)
                {
                    // Mute
                }
                else if (activeContextGroup != null)
                {
                    tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup, false)); 
                }
                else
                {
                    tutorialCoroutine = StartCoroutine(RunIdleLogic(false));
                }
            }
            else if (tutorialCoroutine == null && !isSilenced) 
            {
                RequestNextHintSmart(); 
            }
        
            if (!isHidden)
            {
                rectTransform.anchoredPosition = baseHoverPosition +
                                                 new Vector2(0, Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude);
            }
        }
		
        /// <summary>
        /// Принудительно запускает логику маскота, как будто сцена только что загрузилась.
        /// Используется после диалогов или других блокирующих событий.
        /// </summary>
        public void ForceStartSequence()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            if (sceneLoadCoroutine != null) StopCoroutine(sceneLoadCoroutine);

            // Debug.Log("[TutorialMascot] ForceStartSequence: Принудительный запуск логики с задержкой.");
            sceneLoadCoroutine = StartCoroutine(DelayedForceStart());
        }

        private IEnumerator DelayedForceStart()
        {
            // Debug.Log("[TutorialMascot] DelayedForceStart: ждём 5 секунд...");
            yield return new WaitForSecondsRealtime(5f);
            // Debug.Log("[TutorialMascot] DelayedForceStart: время вышло, запускаем логику");
            sceneLoadCoroutine = StartCoroutine(DelayedSceneLoadLogic(SceneManager.GetActiveScene()));
        }
		
		
		
        #endregion

        // ... (ОСТАЛЬНОЙ КОД БЕЗ ИЗМЕНЕНИЙ, ПРОСТО ОСТАВЬТЕ ЕГО КАК БЫЛ) ...
        // ВАЖНО: Весь регион "Логика Туториала и Idle", "Управление", "Эффекты", "Сохранение" 
        // остается точно таким же, как вы присылали. Изменились только Awake, Start, OnEnable и OnSceneLoadedStarter.

        #region Логика Туториала и Idle (GDD 3.1, 3.2, 3.3)
        private IEnumerator RunIdleLogic(bool useCeremonial = false)
        {
            if (isFirstEverAppearance)
            {
                isFirstEverAppearance = false; 
                PlayerPrefs.SetInt("Mascot_FirstEverAppearance", 1);
                PlayerPrefs.Save();
            }
            isFirstAppearanceThisSession = false;

            bool allSpotsOnScreenVisited = AreAllSpotsOnScreenVisited();
            var visualContext = currentConfig.contextGroups.FirstOrDefault(g => g != null && !g.muteTutorial);
        
            List<RectTransform> idleSpots = GetContextIdleSpots(visualContext);
            if (idleSpots == null || idleSpots.Count == 0) 
            {
                HideInternal(); 
                tutorialCoroutine = null; 
                yield break; 
            }
        
            RectTransform randomIdleSpot = idleSpots[Random.Range(0, idleSpots.Count)];
            lastUsedIdleSpot = randomIdleSpot;
            List<string> messageList = GetGreetingListFromConfig(visualContext); 
            string message = (messageList != null && messageList.Count > 0) ? messageList[Random.Range(0, messageList.Count)] : "Привет!";
        
            yield return StartCoroutine(TeleportToSpot(randomIdleSpot.position, message, visualContext?.greetingEmotion, visualContext?.greetingPointerSprite, visualContext?.greetingPointerRotation ?? 0f, visualContext?.greetingPointerOffset ?? Vector2.zero, useCeremonial));
        
            if (!allSpotsOnScreenVisited)
            {
                yield return new WaitForSecondsRealtime(currentInitialHintDelay);
                tutorialCoroutine = null;
            }
            else
            {
                tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(null)); 
                yield break; 
            }
        }

        private IEnumerator RunTutorialForContext(TutorialContextGroup context, bool useCeremonial = false)
        {
            List<RectTransform> idleSpots = GetContextIdleSpots(context);
            if (idleSpots == null || idleSpots.Count == 0)
            {
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

            yield return StartCoroutine(TeleportToSpot(randomIdleSpot.position, greeting, context.greetingEmotion, context.greetingPointerSprite, context.greetingPointerRotation, context.greetingPointerOffset, useCeremonial));
        
            bool allSpotsInContextVisited = AreAllSpotsInCurrentContextVisited();

            if (allSpotsInContextVisited)
            {
                tutorialCoroutine = StartCoroutine(RunIdleLogicForContext(context));
                yield break; 
            }
            else
            {
                yield return new WaitForSecondsRealtime(currentInitialHintDelay);
                tutorialCoroutine = null; 
            }
        }

        private IEnumerator RunIdleLogicForContext(TutorialContextGroup context)
        {
            while (activeContextGroup == context && !isSilenced)
            {
                List<RectTransform> idleSpots = GetContextIdleSpots(context);
                if (idleSpots == null || idleSpots.Count == 0)
                {
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
            
                var defaultGroup = currentConfig.contextGroups.FirstOrDefault(g => g != null && !g.muteTutorial);
                Sprite emotion = (context != null) ? context.greetingEmotion : (defaultGroup != null ? defaultGroup.greetingEmotion : null);
                Sprite pointer = (context != null) ? context.greetingPointerSprite : (defaultGroup != null ? defaultGroup.greetingPointerSprite : null);
                float pointerRot = (context != null) ? context.greetingPointerRotation : (defaultGroup != null ? defaultGroup.greetingPointerRotation : 0f);
                Vector2 pointerOffset = (context != null) ? context.greetingPointerOffset : (defaultGroup != null ? defaultGroup.greetingPointerOffset : Vector2.zero);

                yield return StartCoroutine(TeleportToSpot(randomIdleSpot.position, message, emotion, pointer, pointerRot, pointerOffset, false));
                yield return new WaitForSecondsRealtime(currentIdleMessageDelay);

                if (activeContextGroup == context) 
                {
                    yield return StartCoroutine(Fade(0f, fadeDuration)); 
                }
            }
            tutorialCoroutine = null;
        }
    
        private IEnumerator ShowSpecificSpotAndManageCoroutine(TutorialHelpSpot spot)
        {
            if (spot.targetElement == null)
            {
                tutorialCoroutine = null; 
                yield break;
            }

            if (!visitedSpotIDs.ContainsKey(currentScreenID))
            {
                visitedSpotIDs[currentScreenID] = new List<string>();
            }
        
            if (!visitedSpotIDs[currentScreenID].Contains(spot.spotID))
            {
                visitedSpotIDs[currentScreenID].Add(spot.spotID);
                SaveVisitedState();
            }
        
            yield return StartCoroutine(TeleportToSpot((Vector2)spot.targetElement.position + spot.mascotPositionOffset, spot.helpText, spot.mascotEmotionSprite, spot.pointerSprite, spot.pointerRotation, spot.pointerPositionOffset, false));
            yield return new WaitForSecondsRealtime(currentNextHintDelay);
            tutorialCoroutine = null;
        
            if (isTutorialResetting)
            {
                isTutorialResetting = false;
            }
        }
        #endregion

        #region Управление
        private void OnNextButtonClicked()
        {
            if (isHidden) return;
            PlaySound(mascotClickSound);
            StopAllCoroutines(); 
            sceneLoadCoroutine = null; 
            tutorialCoroutine = null; 
            isSilenced = false;
            RequestNextHintSmart();
        }
    
        private void OnCloseButtonClicked()
        {
            if (isHidden) return; 
            PlaySound(closeClickSound);
            StopAllCoroutines();
            sceneLoadCoroutine = null; 
            HideInternal(); 
            isSilenced = true;
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

        #region Эффекты
        private IEnumerator TeleportToSpot(Vector2 worldPosition, string text, Sprite emotion, Sprite pointer, float pointerRot, Vector2 pointerOffset, bool isCeremonial = false)
        {
            if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
            if (soundCoroutine != null) StopCoroutine(soundCoroutine); 

            float duration = isCeremonial ? ceremonialFadeDuration : fadeDuration;

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
            if (clip != null) audioSource.PlayOneShot(clip);
        }
        #endregion

        #region Сохранение, Сброс и Вспомогательные методы
        public void RequestNextHintSmart()
        {
            if (isInitializing || IsBusy()) return;
            isSilenced = false; 
            if (sceneLoadCoroutine != null)
            {
                StopCoroutine(sceneLoadCoroutine);
                sceneLoadCoroutine = null;
            }
            tutorialCoroutine = StartCoroutine(RequestNextHintSmart_Coroutine());
        }

        private IEnumerator RequestNextHintSmart_Coroutine()
        {
            if (soundCoroutine != null) StopCoroutine(soundCoroutine);
            if (sheetAnimationCoroutine != null) StopCoroutine(sheetAnimationCoroutine);
        
            if (activeContextGroup == null && currentConfig != null && currentConfig.contextGroups != null)
            {
                activeContextGroup = currentConfig.contextGroups.LastOrDefault(g => IsContextActive(g));
            }
        
            if (activeContextGroup != null)
            {
                TutorialHelpSpot nextSpotInContext = FindNextUnvisitedSpotInContext(activeContextGroup);
                if (nextSpotInContext != null)
                {
                    yield return StartCoroutine(ShowSpecificSpotAndManageCoroutine(nextSpotInContext));
                }
                else
                {
                    yield return StartCoroutine(RunIdleLogicForContext(activeContextGroup));
                }
            }
            else
            {
                TutorialHelpSpot nextSpotOnScreen = FindNextUnvisitedSpotOnScreen();
                if (nextSpotOnScreen != null)
                {
                    yield return StartCoroutine(ShowSpecificSpotAndManageCoroutine(nextSpotOnScreen));
                }
                else
                {
                    yield return StartCoroutine(RunIdleLogic(false));
                }
            }
        }

        private TutorialHelpSpot FindNextUnvisitedSpotInContext(TutorialContextGroup context)
        {
            if (context == null || context.helpSpots == null || !visitedSpotIDs.ContainsKey(currentScreenID)) return null;
            return context.helpSpots.FirstOrDefault(spot => spot != null && !string.IsNullOrEmpty(spot.spotID) && !visitedSpotIDs[currentScreenID].Contains(spot.spotID));
        }
    
        public void ResetCurrentScreenTutorial()
        {
            if (isInitializing || isTutorialResetting) return;
            isTutorialResetting = true; 
            isSilenced = false; 

            if (string.IsNullOrEmpty(currentScreenID))
            {
                isTutorialResetting = false;
                return;
            }
        
            if (visitedSpotIDs.ContainsKey(currentScreenID))
            {
                if (activeContextGroup != null && activeContextGroup.helpSpots != null)
                {
                    List<string> spotsInThisContext = activeContextGroup.helpSpots
                        .Where(s => s != null && !string.IsNullOrEmpty(s.spotID))
                        .Select(s => s.spotID)
                        .ToList();
                    visitedSpotIDs[currentScreenID].RemoveAll(visitedID => spotsInThisContext.Contains(visitedID));
                }
                else
                {
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
                activeContextGroup = currentConfig.contextGroups.LastOrDefault(g => IsContextActive(g));
            }
            else
            {
                activeContextGroup = null;
            }

            lastUsedIdleSpot = null;
            lastUsedGreetingIndex = -1;
            lastUsedTipIndex = -1;
        
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

            if (spotsToCkeck == null || spotsToCkeck.Count == 0) return true; 
            if (!visitedSpotIDs.ContainsKey(currentScreenID)) return false; 
            
            return spotsToCkeck.Where(s => s != null && !string.IsNullOrEmpty(s.spotID)).All(spot => visitedSpotIDs[currentScreenID].Contains(spot.spotID));
        }
    
        private bool AreAllSpotsOnScreenVisited()
        {
            if (currentConfig == null || currentConfig.contextGroups == null) return true;
            if (!visitedSpotIDs.ContainsKey(currentScreenID)) return false;
        
            return currentConfig.contextGroups.All(g => g == null || g.muteTutorial || (g.helpSpots == null) || g.helpSpots.Where(s => s != null && !string.IsNullOrEmpty(s.spotID)).All(spot => visitedSpotIDs[currentScreenID].Contains(spot.spotID)));
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
                if (validSpots.Count > 0) return validSpots;
            }
            if (currentConfig != null && currentConfig.idleSpots != null && currentConfig.idleSpots.Count > 0)
            {
                validSpots = currentConfig.idleSpots.Where(s => s != null).ToList();
                if (validSpots.Count > 0) return validSpots;
            }
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
            if (firstContext != null && firstContext.greetingTexts != null && firstContext.greetingTexts.Count > 0)
            {
                return firstContext.greetingTexts;
            }
            return new List<string> { "Привет!" };
        }
    
        public bool IsBusy()
        {
            return (tutorialCoroutine != null);
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

        private void LoadVisitedState() { }

        private bool IsContextActive(TutorialContextGroup g)
        {
            if (g == null || g.contextPanel == null || !g.contextPanel.activeInHierarchy) 
                return false;
                
            var cg = g.contextPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                // Если CanvasGroup есть, окно считается активным только если оно не прозрачное
                return cg.alpha > 0.01f;
            }
            
            // Если CanvasGroup нет, но объект включен в иерархии — считаем его активным
            return true;
        }
        #endregion
    }

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
}