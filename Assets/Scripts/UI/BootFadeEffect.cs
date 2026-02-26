// Assets/Scripts/UI/BootFadeEffect.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Managers;
using Scriptables.Audio;

public class BootFadeEffect : MonoBehaviour
{
    [Header("Последовательность ОТКРЫТИЯ (Fade)")]
    [Tooltip("Кадры: 4(Черный) -> 3 -> 2 -> 1 -> ВЫКЛ")]
    [SerializeField] private List<Image> fadePanels;

    [Header("Последовательность ЗАКРЫТИЯ (Inverse)")]
    [Tooltip("Кадры: 5 -> 6 -> 7 -> 8(Черный)")]
    [SerializeField] private List<Image> inversePanels;

    [Header("Настройки")]
    [SerializeField] private float frameDuration = 0.08f;
    [Tooltip("ID звука переключения кадра (короткий звук с рандомным питчем)")]
    [SerializeField] private SoundID frameSwitchSound = SoundID.BootFade;

    public bool IsFading { get; private set; }

    private void Awake() 
    {
        DisableAllPanels();
    }

    private void Start()
    {
        if (!IsFading)
        {
            StartCoroutine(FadeSequence(null));
        }
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ---

    public IEnumerator PlayFadeRoutine() 
    {
        yield return StartCoroutine(FadeSequence(null));
    }

    public IEnumerator PlayInverseFadeRoutine() 
    {
        yield return StartCoroutine(InverseFadeSequence());
    }

    // --- ЛОГИКА ПОСЛЕДОВАТЕЛЬНОСТЕЙ ---

    private IEnumerator FadeSequence(System.Action onComplete)
    {
        IsFading = true;
        Debug.Log("[BootFade] Начинаю ОТКРЫТИЕ (Fade)");

        for (int i = 0; i < fadePanels.Count; i++)
        {
            ApplyFrame(fadePanels, i);
            yield return new WaitForSecondsRealtime(frameDuration);
        }

        DisableAllPanels();
        onComplete?.Invoke();
        IsFading = false;
        Debug.Log("[BootFade] ОТКРЫТИЕ завершено");
    }

    private IEnumerator InverseFadeSequence()
    {
        IsFading = true;
        Debug.Log("[BootFade] Начинаю ЗАКРЫТИЕ (Inverse)");

        for (int i = 0; i < inversePanels.Count; i++)
        {
            ApplyFrame(inversePanels, i);
            yield return new WaitForSecondsRealtime(frameDuration);
        }
        
        // В конце Inverse экран остается ЧЕРНЫМ (последний кадр списка активен)
        IsFading = false;
        Debug.Log("[BootFade] ЗАКРЫТИЕ завершено (экран черный)");
    }

    // --- Вспомогательные методы ---

    private void ApplyFrame(List<Image> currentList, int index)
    {
        DisableAllPanels();
        
        if (currentList[index] != null)
        {
            currentList[index].gameObject.SetActive(true);
            PlayFrameSound();
        }
    }

    private void PlayFrameSound()
    {
        if (frameSwitchSound != SoundID.None && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(frameSwitchSound);
        }
    }

    private void DisableAllPanels() 
    {
        if (fadePanels != null) 
            foreach (var p in fadePanels) if (p) p.gameObject.SetActive(false);
            
        if (inversePanels != null) 
            foreach (var p in inversePanels) if (p) p.gameObject.SetActive(false);
    }
}
