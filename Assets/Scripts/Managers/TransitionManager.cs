// Файл: Assets/Scripts/Managers/TransitionManager.cs
using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // Required for List<>
using System.Linq; // Required for Linq methods like Any()

public class TransitionManager : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static TransitionManager Instance { get; private set; }
    // Static property to allow other scripts (like FallingLeaf) to read the current fade value
    public static float GlobalFadeValue { get; private set; } = 1f; // Start fully visible
    // --- End Singleton ---

    [Header("Ссылки")]
    [Tooltip("CanvasGroup компонента черного экрана для затемнения")]
    [SerializeField] private CanvasGroup blackoutCanvasGroup;
    [Tooltip("RectTransform контейнера, куда будут добавляться листья")]
    [SerializeField] private RectTransform leavesContainer;
    [Tooltip("AudioSource для проигрывания звуков перехода")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Префаб одного листа (должен иметь компонент FallingLeaf)")]
    [SerializeField] private GameObject leafPrefab;

    [Header("Настройки Анимации Листьев")]
    [Tooltip("Количество листьев, создаваемых при переходе")]
    [SerializeField] private int numberOfLeaves = 30;
    [Tooltip("Скорость полета листьев (в юнитах/секунду)")]
    [SerializeField] private float leafSpeed = 500f;
    [Tooltip("Список точек (Transform), откуда листья начинают лететь")]
    [SerializeField] private List<Transform> startPoints;
    [Tooltip("Список точек (Transform), куда листья приземляются")]
    [SerializeField] private List<Transform> landingPoints;
    // Список для отслеживания активных листьев на сцене
    private List<GameObject> activeLeaves = new List<GameObject>();

    [Header("Настройки Затемнения по Времени")]
    [Tooltip("Длительность затемнения экрана (в секундах)")]
    public float fadeToBlackDuration = 1.5f;
    [Tooltip("Длительность паузы на черном экране (в секундах)")]
    public float blackScreenHoldDuration = 1.0f;
    [Tooltip("Длительность проявления новой сцены (в секундах)")]
    public float fadeToVisibleDuration = 1.5f;

    [Header("Звуки")]
    [Tooltip("Звук, который проигрывается, когда листья появляются")]
    [SerializeField] private AudioClip leavesSound;
	[Tooltip("Звук, который проигрывается, когда листья улетают")]
    [SerializeField] private AudioClip leavesExitSound;

    private void Awake()
    {
        // Singleton implementation
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject); // Make this object persistent across scenes
            GlobalFadeValue = 1f; // Ensure visibility on first load
             // Ensure AudioSource exists if sounds are assigned
             if ((leavesSound != null || leavesExitSound != null) && audioSource == null) {
                 audioSource = GetComponent<AudioSource>();
                 if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                 audioSource.playOnAwake = false; // Prevent playing on load
                 audioSource.loop = false;
                 // Set AudioSource to ignore pause state if needed
                 audioSource.ignoreListenerPause = true;
                 audioSource.ignoreListenerVolume = true; // Use its own volume settings maybe
             }
        }
        else if (Instance != this)
        {
             Debug.LogWarning("[TransitionManager] Уничтожен дубликат.");
            Destroy(gameObject); // Destroy duplicate instances
        }
    }

    /// <summary>
    /// Public method to initiate a scene transition.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    /// <returns>Coroutine handle for the transition.</returns>
    public Coroutine TransitionToScene(string sceneName)
    {
        // Check if a transition is already in progress (optional)
        // if (IsTransitioning) {
        //     Debug.LogWarning("Transition already in progress.");
        //     return null;
        // }
        return StartCoroutine(TransitionRoutine(sceneName));
    }

    /// <summary>
    /// The main coroutine managing the entire transition sequence.
    /// </summary>
    private IEnumerator TransitionRoutine(string sceneName)
    {
        // isTransitioning = true; // Optional flag to prevent double transitions

        // 1. Spawn leaves and play sound
        SpawnLeaves();

        // 2. Fade screen to black
        Debug.Log($"[TransitionManager] Начало затемнения (FadeToBlack) к сцене {sceneName}...");
        yield return StartCoroutine(Fade(1f, 0f, fadeToBlackDuration)); // Fade GlobalFadeValue from 1 (visible) to 0 (black)
        Debug.Log("[TransitionManager] Затемнение завершено.");


        // 3. Start loading the new scene asynchronously
        Debug.Log($"[TransitionManager] Загрузка сцены {sceneName}...");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = true; // Allow immediate activation once loaded

         // Wait until scene loading is mostly done (optional, can just wait for hold duration)
         // while (!asyncLoad.isDone) {
         //     // You could update a loading bar here using asyncLoad.progress
         //     yield return null;
         // }
         Debug.Log($"[TransitionManager] Сцена {sceneName} загружена.");

        // 4. Hold the black screen for a moment
        yield return new WaitForSecondsRealtime(blackScreenHoldDuration); // Use Realtime to ignore Time.timeScale

        // 5. Fade screen back to visible
         Debug.Log("[TransitionManager] Начало проявления (FadeToVisible)...");
        yield return StartCoroutine(Fade(0f, 1f, fadeToVisibleDuration)); // Fade GlobalFadeValue from 0 (black) to 1 (visible)
        Debug.Log("[TransitionManager] Проявление завершено.");


        // 6. Trigger leaves to fly off screen
        TriggerLeavesExit();

        // isTransitioning = false; // Reset flag
        Debug.Log($"[TransitionManager] Переход к сцене {sceneName} завершен.");
    }

    /// <summary>
    /// Coroutine to smoothly change the GlobalFadeValue and update the blackout canvas alpha.
    /// </summary>
    private IEnumerator Fade(float startValue, float endValue, float duration)
    {
         // Ensure duration is positive
        if (duration <= 0) duration = 0.5f; // Default small duration if invalid

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // Use unscaledDeltaTime so fades work even when Time.timeScale is 0 (paused)
            elapsedTime += Time.unscaledDeltaTime;
            // Calculate current fade value using Lerp
            GlobalFadeValue = Mathf.Lerp(startValue, endValue, elapsedTime / duration);
            // Update the alpha of the blackout canvas (inverted value)
            if (blackoutCanvasGroup != null)
            {
                blackoutCanvasGroup.alpha = 1f - GlobalFadeValue;
            }
            yield return null; // Wait for the next frame
        }

        // Ensure the final value is set correctly
        GlobalFadeValue = endValue;
        if (blackoutCanvasGroup != null)
        {
            blackoutCanvasGroup.alpha = 1f - GlobalFadeValue;
            // Optionally disable interaction when fully faded in/out
            // blackoutCanvasGroup.interactable = (GlobalFadeValue < 1f);
            // blackoutCanvasGroup.blocksRaycasts = (GlobalFadeValue < 1f);
        }
    }

    /// <summary>
    /// Spawns the leaf objects and starts their animation towards landing points.
    /// Implements logic to prioritize unused landing points.
    /// </summary>
    private void SpawnLeaves()
    {
        ClearLeaves(); // Remove any old leaves first

        // Validate necessary references
        if (leafPrefab == null || leavesContainer == null || startPoints == null || landingPoints == null || startPoints.Count == 0 || landingPoints.Count == 0)
        {
             Debug.LogError("[TransitionManager] Не назначен префаб листа, контейнер или списки точек старта/приземления пусты! Невозможно создать листья.");
             return;
        }

        // --- Logic for selecting landing points ---
        // Create a copy of landing points to track available ones
        List<Transform> availableLandingPoints = new List<Transform>(landingPoints.Where(p => p != null)); // Filter nulls immediately
         if (availableLandingPoints.Count == 0) {
              Debug.LogError("[TransitionManager] Список landingPoints не содержит валидных точек!");
              return; // Cannot proceed without landing points
         }
        // List to store points already assigned once (used if leaves > points)
        List<Transform> assignedLandingPoints = new List<Transform>();
        // --- End landing point logic setup ---

        // Play the spawn sound
        if (audioSource != null && leavesSound != null) audioSource.PlayOneShot(leavesSound);

        // Spawn the leaves
        for (int i = 0; i < numberOfLeaves; i++)
        {
            GameObject leafGO = Instantiate(leafPrefab, leavesContainer);
            FallingLeaf leaf = leafGO.GetComponent<FallingLeaf>();
            if (leaf == null) {
                Debug.LogError($"Префаб листа '{leafPrefab.name}' не содержит компонент FallingLeaf!", leafPrefab);
                Destroy(leafGO);
                continue; // Skip this leaf
            }
            activeLeaves.Add(leafGO); // Add to tracking list

            // --- Select Start and Landing Points ---
            // 1. Random Start Point
            Transform startTransform = startPoints[Random.Range(0, startPoints.Count)];
            if (startTransform == null) { // Safety check for start point
                 Debug.LogWarning($"Выбрана null точка старта (индекс {i}). Используется позиция контейнера.");
                 startTransform = leavesContainer; // Fallback
            }


            // 2. Select Landing Point (prioritize available)
            Transform landingTransform = null;
            if (availableLandingPoints.Count > 0) // If there are still unique, unused points
            {
                int randomIndex = Random.Range(0, availableLandingPoints.Count);
                landingTransform = availableLandingPoints[randomIndex];
                availableLandingPoints.RemoveAt(randomIndex); // Remove from available
                assignedLandingPoints.Add(landingTransform); // Add to assigned list
            }
            else // If all unique points have been used at least once
            {
                // Choose randomly from the points already assigned (allowing duplicates)
                if (assignedLandingPoints.Count > 0) { // Should always be true if initial list wasn't empty
                    landingTransform = assignedLandingPoints[Random.Range(0, assignedLandingPoints.Count)];
                } else {
                     // Should not happen if initial check passed, but as a fallback:
                     landingTransform = landingPoints[Random.Range(0, landingPoints.Count)];
                }
            }

            // 3. Calculate Flight Duration
            float distance = Vector3.Distance(startTransform.position, landingTransform.position);
            // Prevent division by zero or near-zero speeds, ensure minimum duration
            float duration = (leafSpeed > 0.01f) ? distance / leafSpeed : 1.0f;
            duration = Mathf.Max(duration, 0.1f); // Minimum duration of 0.1s

            // 4. Start Leaf Animation Coroutine
            StartCoroutine(leaf.AnimateMovement(startTransform.position, landingTransform.position, duration, true)); // useEaseIn = true (default)
            // --- End Point Selection ---
        }
         Debug.Log($"[TransitionManager] Создано {activeLeaves.Count} листьев для перехода.");
    }


    /// <summary>
    /// Initiates the exit animation for all active leaves.
    /// </summary>
    private void TriggerLeavesExit()
    {
		// Play the exit sound
        if (audioSource != null && leavesExitSound != null) audioSource.PlayOneShot(leavesExitSound);

        // Create a copy of the list because AnimateExit modifies the original indirectly by destroying GOs
        List<GameObject> leavesToExit = new List<GameObject>(activeLeaves);
        activeLeaves.Clear(); // Clear the main tracking list immediately

         int exitCount = 0;
        foreach (var leafGO in leavesToExit)
        {
            if (leafGO != null) // Check if the leaf wasn't destroyed prematurely
            {
                FallingLeaf leaf = leafGO.GetComponent<FallingLeaf>();
                if (leaf != null)
                {
                    // Start the exit coroutine for each leaf
                    StartCoroutine(leaf.AnimateExit());
                    exitCount++;
                }
                else {
                    // If component is missing, just destroy the object
                    Destroy(leafGO);
                }
            }
        }
         Debug.Log($"[TransitionManager] Запущено улетание для {exitCount} листьев.");
    }

    /// <summary>
    /// Immediately destroys all active leaf GameObjects.
    /// Used for cleanup before spawning new leaves or if transition is interrupted.
    /// </summary>
    private void ClearLeaves()
    {
         int clearCount = 0;
        // Iterate backwards to safely remove while iterating
        for (int i = activeLeaves.Count - 1; i >= 0; i--)
        {
            if (activeLeaves[i] != null)
            {
                Destroy(activeLeaves[i]);
                clearCount++;
            }
        }
        activeLeaves.Clear(); // Clear the list
         if (clearCount > 0) Debug.Log($"[TransitionManager] Очищено {clearCount} старых листьев.");
    }

} // End of TransitionManager class