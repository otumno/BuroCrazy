using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ProcessingAnimationUI : MonoBehaviour
{
    [Header("Компоненты")]
    public Image targetImage;
    public Canvas canvas;
    
    [Header("Настройки")]
    [Tooltip("Спрайты анимации (0..5). Последний будет 'замирать'.")]
    public List<Sprite> animationFrames;
    
    [Tooltip("Звуки для каждого кадра (опционально). Если список меньше кадров, звука не будет.")]
    public List<AudioClip> frameSounds;

    [Tooltip("Какую часть общего времени показывать последний кадр (0.2 = 20% времени).")]
    [Range(0.1f, 0.5f)]
    public float finalFrameHoldRatio = 0.25f;

    [Tooltip("Смещение по Y относительно центра")]
    public float verticalOffset = 1.5f;

    private AudioSource audioSource;

    private void Awake()
    {
        // Создаем источник звука на лету, если его нет
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // 2D звук (UI)
        audioSource.playOnAwake = false;
        foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        
        // Настраиваем Canvas программно, если забыли в префабе
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 1000; // Поверх всего
        }
    }

    /// <summary>
    /// Запускает анимацию в точке между двумя позициями.
    /// </summary>
    public void Play(Vector3 posA, Vector3 posB, float totalDuration)
    {
        StopAllCoroutines();
        // 1. Позиционирование в геометрическом центре
        Vector3 centerPos = (posA + posB) / 2f;
        centerPos.y += verticalOffset;
        transform.position = centerPos;

        if (targetImage == null || animationFrames == null || animationFrames.Count == 0 || animationFrames.Exists(frame => frame == null))
        {
            Debug.LogError("[ProcessingAnimationUI] Missing image or animation frames.", this);
            Destroy(gameObject);
            return;
        }

        StartCoroutine(AnimationRoutine(float.IsNaN(totalDuration) || float.IsInfinity(totalDuration) ? 1f : Mathf.Max(.01f, totalDuration)));
    }

    private IEnumerator AnimationRoutine(float totalDuration)
    {
        int totalFrames = animationFrames.Count;
        
        // Расчет времени
        // Время на "замирание" последнего кадра
        float holdTime = totalFrames == 1 ? totalDuration : totalDuration * Mathf.Clamp01(finalFrameHoldRatio);
        // Оставшееся время на активную анимацию
        float activeAnimTime = totalDuration - holdTime;
        
        // Время показа одного кадра (кроме последнего)
        // Если кадров 6, то меняем мы их 5 раз до последнего.
        float timePerFrame = activeAnimTime / Mathf.Max(1, (totalFrames - 1));

        for (int i = 0; i < totalFrames; i++)
        {
            // Установка спрайта
            targetImage.sprite = animationFrames[i];

            // Звук (если назначен для этого кадра)
            if (frameSounds != null && i < frameSounds.Count && frameSounds[i] != null)
            {
                audioSource.PlayOneShot(frameSounds[i]);
            }

            // Ждем
            if (i == totalFrames - 1)
            {
                // Это последний кадр - ждем holdTime
                yield return new WaitForSeconds(holdTime);
            }
            else
            {
                // Это обычный кадр - ждем timePerFrame
                yield return new WaitForSeconds(timePerFrame);
            }
        }

        // Анимация завершена - уничтожаем объект
        Destroy(gameObject);
    }
}
