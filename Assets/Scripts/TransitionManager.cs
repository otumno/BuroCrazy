// Файл: TransitionManager.cs - ОБНОВЛЕННАЯ ВЕРСИЯ
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
    [SerializeField] private int numberOfLeaves = 30;
    [SerializeField] private float leafSpeed = 500f;
    // --- НОВЫЕ ПОЛЯ ДЛЯ НАСТРОЙКИ ---
    [Tooltip("Как долго листья лежат на земле после падения (в секундах)")]
    [SerializeField] private float leafDwellDuration = 1.5f;
    [Tooltip("Как долго листья исчезают после задержки (в секундах)")]
    [SerializeField] private float leafFadeOutDuration = 0.5f;
    // ------------------------------------
    [SerializeField] private List<Transform> startPoints;
    [SerializeField] private List<Transform> landingPoints;

    [Header("Настройки Затемнения")]
    [SerializeField] private float fadeDuration = 1.0f;
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

    public IEnumerator AnimateTransition(bool isFadeIn)
    {
        if (transitionCanvasGroup == null || leafPrefab == null || startPoints.Count == 0 || landingPoints.Count == 0)
        {
            Debug.LogWarning("TransitionManager не настроен, анимация пропускается.");
            yield break;
        }

        if (isFadeIn)
        {
            if (audioSource != null && leavesSound != null)
                audioSource.PlayOneShot(leavesSound);

            for (int i = 0; i < numberOfLeaves; i++)
            {
                GameObject leafGO = Instantiate(leafPrefab, transitionPanelObject.transform);
                FallingLeaf leaf = leafGO.GetComponent<FallingLeaf>();

                if (leaf != null)
                {
                    Transform startTransform = startPoints[Random.Range(0, startPoints.Count)];
                    Transform landingTransform = landingPoints[Random.Range(0, landingPoints.Count)];

                    Vector3 startPos = isFadeIn ? startTransform.position : landingTransform.position;
                    Vector3 endPos = isFadeIn ? landingTransform.position : startTransform.position;

                    float distance = Vector3.Distance(startPos, endPos);
                    float duration = distance / leafSpeed;
                    
                    // --- ИЗМЕНЕНИЕ: Передаем новые параметры в корутину Animate ---
                    StartCoroutine(leaf.Animate(startPos, endPos, duration, isFadeIn, isFadeIn, leafDwellDuration, leafFadeOutDuration));
                }
            }
        }
        
        // Фон анимируется параллельно
        yield return StartCoroutine(FadeCanvasGroup(transitionCanvasGroup, isFadeIn ? 0f : 1f, isFadeIn ? 1f : 0f, fadeDuration));
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