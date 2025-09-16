using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private GameObject transitionPanelObject;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject leafPrefab;

    [Header("Настройки Анимации Листьев")]
    [Tooltip("Сколько всего листьев будет участвовать в анимации")]
    [SerializeField] private int numberOfLeaves = 30;
    [Tooltip("Базовая скорость полета листьев")]
    [SerializeField] private float leafSpeed = 500f;
    [Tooltip("Список точек, откуда вылетают листья (разместите их за экраном)")]
    [SerializeField] private List<Transform> startPoints;
    [Tooltip("Список точек, куда прилетают листья (разместите их в центре экрана)")]
    [SerializeField] private List<Transform> landingPoints;

    [Header("Настройки Затемнения")]
    [SerializeField] private float fadeDuration = 0.7f;
    [SerializeField] private AudioClip leavesSound;

    private CanvasGroup transitionCanvasGroup;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (transitionPanelObject != null)
                transitionCanvasGroup = transitionPanelObject.GetComponent<CanvasGroup>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Coroutine StartTransition(bool isFadeIn)
    {
        if (audioSource != null && leavesSound != null)
            audioSource.PlayOneShot(leavesSound);
        
        return StartCoroutine(TransitionRoutine(isFadeIn));
    }

    private IEnumerator TransitionRoutine(bool isFadeIn)
    {
        if (transitionCanvasGroup == null) yield break;

        AnimateAllLeaves(isFadeIn);
        yield return StartCoroutine(FadeCanvasGroup(transitionCanvasGroup, isFadeIn ? 0f : 1f, isFadeIn ? 1f : 0f, fadeDuration));
    }

    private void AnimateAllLeaves(bool isFadeIn)
    {
        if (leafPrefab == null || startPoints.Count == 0 || landingPoints.Count == 0)
        {
            Debug.LogWarning("Анимация листьев не может быть запущена: не настроены точки старта/приземления или префаб.");
            return;
        }

        for (int i = 0; i < numberOfLeaves; i++)
        {
            GameObject leafGO = Instantiate(leafPrefab, transitionPanelObject.transform);
            FallingLeaf leaf = leafGO.GetComponent<FallingLeaf>();

            if (leaf != null)
            {
                // Выбираем случайные точки старта и приземления из списков
                Transform startTransform = startPoints[Random.Range(0, startPoints.Count)];
                Transform landingTransform = landingPoints[Random.Range(0, landingPoints.Count)];

                // Определяем направление полета
                Vector3 startPos = isFadeIn ? startTransform.position : landingTransform.position;
                Vector3 endPos = isFadeIn ? landingTransform.position : startTransform.position;

                // Рассчитываем длительность полета на основе скорости
                float distance = Vector3.Distance(startPos, endPos);
                float duration = distance / leafSpeed;
                
                StartCoroutine(leaf.Animate(startPos, endPos, duration, isFadeIn, isFadeIn));
            }
        }
    }
    
    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            cg.alpha = Mathf.Lerp(start, end, elapsedTime / duration);
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }
        cg.alpha = end;
    }
}