using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;
using Managers;
using UnityEngine.SceneManagement;
using Scriptables.Audio;

[RequireComponent(typeof(CanvasGroup))]
public class UIWindowAnimator : MonoBehaviour
{
    [Header("Настройки Анимации")]
    public bool useGlobalSettings = true;
    public float localDuration = 0.25f;
    public float localStartScale = 0.9f;
    public Ease localShowEase = Ease.OutCubic;
    public Ease localHideEase = Ease.InCubic;

    [Header("Настройки Звука")]
    public bool playSounds = true;
    public bool useGlobalSounds = true;
    public SoundID localOpenSound = SoundID.None;
    public SoundID localCloseSound = SoundID.None;

    [Header("События")]
    public UnityEvent OnOpen;
    public UnityEvent OnClose;

    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    private bool isClosing = false;

    public bool IsVisible() => canvasGroup is {alpha: > 0};

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        originalScale = transform.localScale;
        if (canvasGroup.alpha == 0) HideInstant();
    }

    public void Open()
    {
        if (isClosing) return;
        
        transform.DOKill();
        canvasGroup.DOKill();

        // Логика звука открытия
        if (playSounds && AudioManager.Instance != null)
        {
            SoundID soundToPlay = (useGlobalSounds && UIGlobalSettingsManager.Instance != null) 
                ? UIGlobalSettingsManager.Instance.defaultOpenSound 
                : localOpenSound;
                
            if (soundToPlay != SoundID.None)
                AudioManager.Instance.PlaySound(soundToPlay);
        }

        float d = useGlobalSettings && UIGlobalSettingsManager.Instance ? UIGlobalSettingsManager.Instance.globalDuration : localDuration;
        float s = useGlobalSettings && UIGlobalSettingsManager.Instance ? UIGlobalSettingsManager.Instance.globalStartScale : localStartScale;
        Ease ease = useGlobalSettings && UIGlobalSettingsManager.Instance ? UIGlobalSettingsManager.Instance.globalShowEase : localShowEase;

        transform.localScale = originalScale * s;
        canvasGroup.alpha = 0f;
        
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        transform.DOScale(originalScale, d).SetEase(ease).SetUpdate(true);
        canvasGroup.DOFade(1f, d).SetUpdate(true);

        OnOpen?.Invoke();
    }

    public void Close()
    {
        if (isClosing || canvasGroup.alpha == 0) return;
        isClosing = true;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        transform.DOKill();
        canvasGroup.DOKill();

        // Логика звука закрытия
        if (playSounds && AudioManager.Instance != null)
        {
            SoundID soundToPlay = (useGlobalSounds && UIGlobalSettingsManager.Instance != null) 
                ? UIGlobalSettingsManager.Instance.defaultCloseSound 
                : localCloseSound;
                
            if (soundToPlay != SoundID.None)
                AudioManager.Instance.PlaySound(soundToPlay);
        }

        float d = useGlobalSettings && UIGlobalSettingsManager.Instance ? UIGlobalSettingsManager.Instance.globalDuration : localDuration;
        float s = useGlobalSettings && UIGlobalSettingsManager.Instance ? UIGlobalSettingsManager.Instance.globalStartScale : localStartScale;
        Ease ease = useGlobalSettings && UIGlobalSettingsManager.Instance ? UIGlobalSettingsManager.Instance.globalHideEase : localHideEase;

        transform.DOScale(originalScale * s, d).SetEase(ease).SetUpdate(true);
        canvasGroup.DOFade(0f, d).SetUpdate(true).OnComplete(() =>
        {
            isClosing = false;
            OnClose?.Invoke();
            
            if (SceneManager.GetActiveScene().name != "MainMenuScene")
            {
                MainUIManager.Instance?.PopPause();
            }
        });
    }

    public void HideInstant()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        transform.localScale = originalScale * (useGlobalSettings && UIGlobalSettingsManager.Instance ? UIGlobalSettingsManager.Instance.globalStartScale : localStartScale);
    }
    
    public void ShowInstant()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        transform.DOKill();
        canvasGroup.DOKill();
        
        transform.localScale = originalScale;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }
}
