using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }
    public static float GlobalFadeValue { get; private set; } = 1f;

    [Header("Ссылки")]
    [SerializeField] private CanvasGroup blackoutCanvasGroup;
    [SerializeField] private RectTransform leavesContainer;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject leafPrefab;
    
    [Header("Настройки Анимации Листьев")]
    [SerializeField] private int numberOfLeaves = 30;
    [SerializeField] private float leafSpeed = 500f;
    [SerializeField] private List<Transform> startPoints;
    [SerializeField] private List<Transform> landingPoints;
    private List<GameObject> activeLeaves = new List<GameObject>();

    [Header("Настройки Затемнения по Времени")]
    public float fadeToBlackDuration = 1.5f;
    public float blackScreenHoldDuration = 1.0f;
    public float fadeToVisibleDuration = 1.5f;
    [SerializeField] private AudioClip leavesSound;
	
	[Tooltip("Звук, который проигрывается, когда листья улетают.")]
    [SerializeField] private AudioClip leavesExitSound; 

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); GlobalFadeValue = 1f; }
        else { Destroy(gameObject); }
    }

    public Coroutine TransitionToScene(string sceneName)
    {
        return StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        SpawnLeaves();
        if (audioSource != null && leavesSound != null) audioSource.PlayOneShot(leavesSound);

        yield return StartCoroutine(Fade(1f, 0f, fadeToBlackDuration));

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        yield return new WaitForSecondsRealtime(blackScreenHoldDuration);

        yield return StartCoroutine(Fade(0f, 1f, fadeToVisibleDuration));
        
        // <<< ИЗМЕНЕНИЕ: Вместо удаления даем команду улетать >>>
        TriggerLeavesExit();
    }
    
    private IEnumerator Fade(float startValue, float endValue, float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            GlobalFadeValue = Mathf.Lerp(startValue, endValue, elapsedTime / duration);
            if (blackoutCanvasGroup != null) { blackoutCanvasGroup.alpha = 1f - GlobalFadeValue; }
            yield return null;
        }
        GlobalFadeValue = endValue;
        if (blackoutCanvasGroup != null) { blackoutCanvasGroup.alpha = 1f - GlobalFadeValue; }
    }
    
    private void SpawnLeaves()
    {
        ClearLeaves();
        if (leafPrefab == null || leavesContainer == null) return;
        for (int i = 0; i < numberOfLeaves; i++)
        {
            GameObject leafGO = Instantiate(leafPrefab, leavesContainer);
            FallingLeaf leaf = leafGO.GetComponent<FallingLeaf>();
            activeLeaves.Add(leafGO);
            if (leaf != null)
            {
                Transform startTransform = startPoints[Random.Range(0, startPoints.Count)];
                Transform landingTransform = landingPoints[Random.Range(0, landingPoints.Count)];
                float distance = Vector3.Distance(startTransform.position, landingTransform.position);
                float duration = distance / leafSpeed;
                StartCoroutine(leaf.AnimateMovement(startTransform.position, landingTransform.position, duration, true));
            }
        }
    }

    // <<< ИЗМЕНЕНИЕ: Этот метод теперь не удаляет листья, а запускает их анимацию >>>
    private void TriggerLeavesExit()
    {
		        if (audioSource != null && leavesExitSound != null) audioSource.PlayOneShot(leavesExitSound);
		
        foreach (var leafGO in activeLeaves)
        {
            if (leafGO != null)
            {
                FallingLeaf leaf = leafGO.GetComponent<FallingLeaf>();
                if (leaf != null)
                {
                    // Запускаем для каждого листа корутину "улетания"
                    StartCoroutine(leaf.AnimateExit());
                }
            }
        }
        // Очищаем список, так как листья теперь сами о себе позаботятся
        activeLeaves.Clear();
    }

    // Этот метод теперь нужен на случай, если переход прервется
    private void ClearLeaves()
    {
        foreach (var leaf in activeLeaves)
        {
            if(leaf != null) Destroy(leaf);
        }
        activeLeaves.Clear();
    }
}