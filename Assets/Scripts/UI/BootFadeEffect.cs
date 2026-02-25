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
    [Tooltip("Кадры: 4(Черный) -> 3(3/4) -> 2(2/4) -> 1(1/4) -> ВЫКЛ")]
    [SerializeField] private List<Image> fadePanels;

    [Header("Последовательность ЗАКРЫТИЯ (Inverse)")]
    [Tooltip("Кадры: 5(Черный) -> 6(открыто 1/4) -> 7(2/4) -> 8(3/4)")]
    [SerializeField] private List<Image> inversePanels;

    [SerializeField] private float frameDuration = 0.08f;
    [SerializeField] private SoundID fadeSound = SoundID.BootFade;

    public bool IsFading { get; private set; }

    private void Awake() {
        DisableAllPanels();
    }

    private void Start()
    {
        if (!IsFading)
        {
            if (fadePanels != null && fadePanels.Count > 0)
                fadePanels[0].gameObject.SetActive(true);

            PlayFade(null);
        }
    }

    public void PlayFade(System.Action onComplete = null)
    {
        if (IsFading) return;
        StartCoroutine(FadeSequence(onComplete));
    }

    private IEnumerator FadeSequence(System.Action onComplete)
    {
        IsFading = true;
        for (int i = 0; i < fadePanels.Count; i++)
        {
            DisableAllPanels();
            if (fadePanels[i] != null) fadePanels[i].gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(frameDuration);
        }
        DisableAllPanels();
        onComplete?.Invoke();
        IsFading = false;
    }

    private void DisableAllPanels() {
        if (fadePanels != null) foreach (var p in fadePanels) if (p) p.gameObject.SetActive(false);
        if (inversePanels != null) foreach (var p in inversePanels) if (p) p.gameObject.SetActive(false);
    }

    // ЛОГИКА: ЧЕРНЫЙ -> ПРОЗРАЧНЫЙ
    public IEnumerator PlayFadeRoutine() {
        IsFading = true;
        Debug.Log("[BootFade] ОТКРЫТИЕ ЭКРАНА (Fade)");
        
        for (int i = 0; i < fadePanels.Count; i++) {
            DisableAllPanels();
            if (fadePanels[i] != null) fadePanels[i].gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(frameDuration);
        }

        DisableAllPanels();
        IsFading = false;
    }

    // ЛОГИКА: ПРОЗРАЧНЫЙ -> ЧЕРНЫЙ
    public IEnumerator PlayInverseFadeRoutine() {
        IsFading = true;
        Debug.Log("[BootFade] ЗАКРЫТИЕ ЭКРАНА (Inverse)");

        if (fadeSound != SoundID.None && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(fadeSound);

        for (int i = 0; i < inversePanels.Count; i++) {
            DisableAllPanels();
            if (inversePanels[i] != null) inversePanels[i].gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(frameDuration);
        }
        
        IsFading = false;
    }
}
