// Файл: Assets/Scripts/UI/Tutorial/TutorialMascot.cs (ВЕРСИЯ 3 - Исправлена ошибка)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(AudioSource))]
public class TutorialMascot : MonoBehaviour, IPointerClickHandler
{
    public static TutorialMascot Instance { get; private set; }

    [Header("UI (внутренние ссылки префаба)")]
    [SerializeField] private Image folderBackImage; // Задняя стенка папки
    [SerializeField] private Image folderFrontImage; // Передняя стенка (с эмоцией)
    [SerializeField] private Image pointerHandImage; // Рука-указатель
    [SerializeField] private GameObject textBubbleObject; // Весь объект "листка"
    [SerializeField] private RectTransform textSheetRect; // Sam листок (для изменения высоты)
    [SerializeField] private TextMeshProUGUI helpText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton; 

    [Header("Звуки (по п. 7)")]
    [SerializeField] private AudioClip textBeepSound; // Повторяющийся "бип"
    [SerializeField] private AudioClip textFinalBeepSound; // Финальный "бип"

    [Header("Настройки Анимации")]
    [SerializeField] private float fadeDuration = 0.2f; // Плавность (по п. 5)
    [SerializeField] private float hoverAmplitude = 5f; // Амплитуда "парения"
    [SerializeField] private float hoverSpeed = 2f;
    [SerializeField] private float initialDelay = 1.0f; // Задержка перед приветствием
    [SerializeField] private float nextSpotDelay = 4.0f; // Задержка перед следующей подсказкой

    // Внутренние компоненты
    private AudioSource audioSource;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    // Состояние
    private TutorialScreenConfig currentConfig;
    private TutorialContextGroup activeContextGroup;
    private Dictionary<string, List<string>> visitedSpotIDs = new Dictionary<string, List<string>>(); 
    private bool allSpotsShownForThisContext = false;
    private bool isHidden = true;
    private Vector2 baseHoverPosition;
    private Coroutine tutorialCoroutine;
    private Coroutine soundCoroutine;
    
    private string currentScreenID = "";

    #region Инициализация и Сцены

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null); 
            DontDestroyOnLoad(gameObject); 
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

        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        isHidden = true;
    }

    void Start()
    {
        nextButton.onClick.AddListener(OnNextButtonClicked);
        closeButton.onClick.AddListener(Hide); 
        
        LoadVisitedState(); 
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentConfig = FindObjectOfType<TutorialScreenConfig>();
        if (currentConfig != null)
        {
            currentScreenID = currentConfig.screenID;
            if (!visitedSpotIDs.ContainsKey(currentScreenID))
            {
                visitedSpotIDs[currentScreenID] = new List<string>();
            }
            activeContextGroup = null;
            Hide(); 
        }
        else
        {
            currentScreenID = "";
            Hide();
        }
    }

    #endregion

    #region Главная Логика (Update)

    void Update()
    {
        // 1. Анимация "парения" (п. 1)
        if (!isHidden)
        {
            rectTransform.anchoredPosition = baseHoverPosition + 
                new Vector2(0, Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude);
        }

        if (currentConfig == null) return;

        // 2. Логика поиска контекста (п. 8)
        TutorialContextGroup newContext = null;
        if (currentConfig.contextGroups != null)
        {
            newContext = currentConfig.contextGroups.FirstOrDefault(
                g => g.contextPanel != null && g.contextPanel.activeInHierarchy
            );
        }

        // 3. Сравниваем с текущим контекстом
        if (newContext != activeContextGroup)
        {
            activeContextGroup = newContext;
            if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
            
            if (activeContextGroup != null)
            {
                tutorialCoroutine = StartCoroutine(RunTutorialForContext(activeContextGroup));
            }
            else
            {
                tutorialCoroutine = StartCoroutine(RunIdleLogic());
            }
        }
    }

    #endregion

    #region Логика Туториала

    private IEnumerator RunIdleLogic()
    {
        if (currentConfig.idleSpots == null || currentConfig.idleSpots.Count == 0)
        {
            Hide();
            yield break;
        }

        bool allSpotsOnScreenVisited = currentConfig.contextGroups.All(g => 
            g.helpSpots.All(spot => visitedSpotIDs[currentScreenID].Contains(spot.spotID))
        );
        
        if (allSpotsOnScreenVisited)
        {
            yield return StartCoroutine(TeleportToSpot(currentConfig.idleSpots[Random.Range(0, currentConfig.idleSpots.Count)].anchoredPosition, 
                                        "Я всегда здесь, если забудешь!",
                                        null, 0, null, 0, Vector2.zero, 0));
        }
        else
        {
            yield return new WaitForSeconds(initialDelay); 
            yield return StartCoroutine(TeleportToSpot(currentConfig.idleSpots[Random.Range(0, currentConfig.idleSpots.Count)].anchoredPosition, 
                                        "Привет! Я тут, если понадоблюсь.",
                                        null, 150f, null, 0, Vector2.zero, 3));
        }
    }

    private IEnumerator RunTutorialForContext(TutorialContextGroup context)
    {
        allSpotsShownForThisContext = context.helpSpots.All(spot => 
            visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );

        if (allSpotsShownForThisContext)
        {
            Hide();
            yield break;
        }
        
        yield return new WaitForSeconds(initialDelay);
        Vector2 greetingPos = currentConfig.idleSpots[Random.Range(0, currentConfig.idleSpots.Count)].anchoredPosition;
        
        yield return StartCoroutine(TeleportToSpot(greetingPos, 
                                    context.greetingText, 
                                    context.greetingEmotion, 
                                    context.greetingSheetHeight, 
                                    null, 0, Vector2.zero, 
                                    context.greetingSoundRepetitions));

        yield return new WaitForSeconds(nextSpotDelay);
        ShowNextUnvisitedSpot();
    }

    private IEnumerator TeleportToSpot(Vector2 position, string text, Sprite emotion, float sheetHeight, Sprite pointer, float pointerRot, Vector2 pointerOffset, int beeps)
    {
        yield return StartCoroutine(Fade(0f));
        
        rectTransform.anchoredPosition = position;
        baseHoverPosition = position; 
        
        if (folderFrontImage != null) folderFrontImage.sprite = emotion;
        if (textSheetRect != null) textSheetRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sheetHeight);
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
        
        if (textBubbleObject != null) textBubbleObject.SetActive(text != null);
        if (nextButton != null) nextButton.gameObject.SetActive(text != null && !allSpotsShownForThisContext); 

        yield return StartCoroutine(Fade(1f));

        if (soundCoroutine != null) StopCoroutine(soundCoroutine);
        if (beeps > 0) 
        {
            soundCoroutine = StartCoroutine(PlayArrivalSound(beeps));
        }
    }

    private void ShowNextUnvisitedSpot()
    {
        if (activeContextGroup == null || allSpotsShownForThisContext)
        {
            if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
            tutorialCoroutine = StartCoroutine(RunIdleLogic());
            return;
        }

        TutorialHelpSpot nextSpot = activeContextGroup.helpSpots.FirstOrDefault(spot => 
            !visitedSpotIDs[currentScreenID].Contains(spot.spotID)
        );

        if (nextSpot != null)
        {
            Vector2 targetPos = (Vector2)nextSpot.targetElement.anchoredPosition + nextSpot.mascotPositionOffset;
            
            if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
            tutorialCoroutine = StartCoroutine(TeleportToSpot(
                targetPos,
                nextSpot.helpText,
                nextSpot.mascotEmotionSprite,
                nextSpot.textSheetHeight,
                nextSpot.pointerSprite,
                nextSpot.pointerRotation,
                nextSpot.pointerPositionOffset,
                nextSpot.soundRepetitions
            ));

            visitedSpotIDs[currentScreenID].Add(nextSpot.spotID);
            SaveVisitedState();
        }
        else
        {
            allSpotsShownForThisContext = true;
            if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
            tutorialCoroutine = StartCoroutine(RunIdleLogic());
        }
    }

    #endregion

    #region Управление (Нажатия кнопок)

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isHidden) return;
        
        if (currentConfig.idleSpots != null && currentConfig.idleSpots.Count > 0)
        {
            if (allSpotsShownForThisContext)
            {
                ResetCurrentScreenTutorial();
            }
            else
            {
                if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
                tutorialCoroutine = StartCoroutine(RunIdleLogic());
            }
        }
        else
        {
            Hide();
        }
    }

    public void ToggleHelp()
    {
        if (isHidden)
        {
            ResetCurrentScreenTutorial();
        }
        else
        {
            Hide();
        }
    }
    
    private void OnNextButtonClicked()
    {
        if (isHidden) return;
        
        if (textBubbleObject != null) textBubbleObject.SetActive(false);
        if (pointerHandImage != null) pointerHandImage.gameObject.SetActive(false);
        ShowNextUnvisitedSpot();
    }

    #endregion

    #region Эффекты (Звук / Появление)

    private IEnumerator Fade(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.interactable = (targetAlpha > 0);
        
        if (targetAlpha > 0)
        {
            isHidden = false;
        }
        else
        {
            isHidden = true;
        }
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

    
    // Прячет маскота
    public void Hide()
    {
        if (tutorialCoroutine != null) StopCoroutine(tutorialCoroutine);
        if (soundCoroutine != null) StopCoroutine(soundCoroutine);
        
        audioSource.Stop();
        
        if (!isHidden)
        {
            StartCoroutine(Fade(0f));
        }
        
        isHidden = true;
        // --- ВОТ ИСПРАВЛЕНИЕ ---
        // Я удалил строку "isMoving = false;", которая вызывала ошибку
    }

    #endregion

    #region Сохранение и Сброс

    public void ResetCurrentScreenTutorial()
    {
        if (string.IsNullOrEmpty(currentScreenID)) return;
        
        Debug.Log($"[Mascot] Сброс прогресса для '{currentScreenID}'.");
        if (visitedSpotIDs.ContainsKey(currentScreenID))
        {
            visitedSpotIDs[currentScreenID].Clear();
        }
        SaveVisitedState(); 
        
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void UpdateAllSpotsShownState()
    {
        if (currentConfig == null || currentConfig.contextGroups == null || currentConfig.contextGroups.Count == 0)
        {
            allSpotsShownForThisContext = true; 
            return;
        }
        
        allSpotsShownForThisContext = currentConfig.contextGroups.All(g => 
            g.helpSpots.All(spot => 
                visitedSpotIDs.ContainsKey(currentScreenID) && 
                visitedSpotIDs[currentScreenID].Contains(spot.spotID)
            )
        );
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
        Debug.Log($"[Mascot] Весь прогресс сохранен.");
    }

    private void LoadVisitedState()
    {
        visitedSpotIDs.Clear();
        // Загрузка происходит по мере необходимости в OnSceneLoaded
    }

    #endregion
}