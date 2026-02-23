using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Managers;
using Scriptables.Audio;

    public class BootFadeEffect : MonoBehaviour
    {
        [Header("Панели для затемнения (верхняя → нижняя)")]
        [SerializeField] private List<Image> fadePanels;

        [Header("Настройки")]
        [SerializeField] private float fadeSpeed = 0.6f;

        [Header("Звуки")]
        [SerializeField] private SoundID fadeSound = SoundID.None;

        [Header("Inverse Fade (появление)")]
        [SerializeField] private List<Image> inversePanels;
        [SerializeField] private SoundID inverseFadeSound = SoundID.None;

        private const string BOOT_FADE_PLAYED_KEY = "BootFadeEffect_Played";
        
        private bool isFadingOut = false;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (gameObject.activeSelf)
        {
            StartCoroutine(PlayFadeSequence());
        }
    }

    public void PlayFade()
    {
        StartCoroutine(PlayFadeSequence());
    }

    private IEnumerator PlayFadeSequence()
    {
        PlayFadeSound();

        foreach (Image panel in fadePanels)
        {
            if (panel != null)
            {
                Color startColor = panel.color;
                Color targetColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / fadeSpeed;
                    panel.color = Color.Lerp(startColor, targetColor, t);
                    yield return null;
                }

                panel.color = targetColor;
                panel.gameObject.SetActive(false);
            }
        }

        gameObject.SetActive(false);
    }

    private void PlayFadeSound()
    {
        if (fadeSound != SoundID.None && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(fadeSound);
        }
    }

    public void PlayInverseFade(System.Action onComplete = null)
    {
        foreach (Image panel in inversePanels)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                panel.raycastTarget = true;
            }
        }
        StartCoroutine(PlayInverseFadeSequence(onComplete));
    }
    
    public void DisableRaycastOnInversePanels()
    {
        foreach (Image panel in inversePanels)
        {
            if (panel != null)
            {
                panel.raycastTarget = false;
            }
        }
        foreach (Image panel in fadePanels)
        {
            if (panel != null)
            {
                panel.raycastTarget = false;
            }
        }
    }

    private IEnumerator PlayInverseFadeSequence(System.Action onComplete)
    {
        foreach (Image panel in inversePanels)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                Color startColor = panel.color;
                Color targetColor = new Color(startColor.r, startColor.g, startColor.b, 1f);

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / fadeSpeed;
                    panel.color = Color.Lerp(startColor, targetColor, t);
                    yield return null;
                }

                panel.color = targetColor;
            }
        }

        if (inverseFadeSound != SoundID.None && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(inverseFadeSound);
        }

        onComplete?.Invoke();
    }

    [ContextMenu("Сбросить флаг запуска")]
    public void ResetBootFlag()
    {
        PlayerPrefs.DeleteKey(BOOT_FADE_PLAYED_KEY);
        Debug.Log("[BootFadeEffect] Флаг сброшен. Эффект будет при следующем запуске.");
    }
}
