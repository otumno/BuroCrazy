using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Managers;
using Scriptables.Audio;

public class BootFadeEffect : MonoBehaviour
{
    [Header("Кадры")]
    public List<Image> openSequence;  // 1 (Черный) -> 2 -> 3 -> 4 (Прозрачный)
    public List<Image> closeSequence; // 5 (Прозрачный) -> 6 -> 7 -> 8 (Черный)

    [Header("Настройки")]
    [SerializeField] private float frameDuration = 0.08f;
    [SerializeField] private SoundID frameSound = SoundID.BootFade;
    [SerializeField] private bool playOpenAtStart = true;

    private void Awake() 
    {
        DisableAll();
        if (MainUIManager.Instance != null) MainUIManager.Instance.RegisterSceneFade(this);
    }

    private void Start() 
    {
        if (playOpenAtStart) StartCoroutine(PlayOpenRoutine());
    }

    public void DisableAll() 
    {
        foreach (var img in openSequence) if (img) img.gameObject.SetActive(false);
        foreach (var img in closeSequence) if (img) img.gameObject.SetActive(false);
    }

    public IEnumerator PlayCloseRoutine() 
    {
        for (int i = 0; i < closeSequence.Count; i++) 
        {
            DisableAll();
            if (closeSequence[i]) closeSequence[i].gameObject.SetActive(true);
            PlaySound();
            yield return new WaitForSecondsRealtime(frameDuration);
        }
    }

    public IEnumerator PlayOpenRoutine() 
    {
        for (int i = 0; i < openSequence.Count; i++) 
        {
            DisableAll(); 
            if (openSequence[i]) openSequence[i].gameObject.SetActive(true);
            PlaySound();
            yield return new WaitForSecondsRealtime(frameDuration);
        }
        DisableAll();
    }

    private void PlaySound() 
    {
        if (frameSound != SoundID.None && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(frameSound);
    }
}
