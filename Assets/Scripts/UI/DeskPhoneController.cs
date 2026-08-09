using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Managers;
using Scriptables.Audio;

public class DeskPhoneController : DeskInteractiveItem
{
    [Header("Настройки мигания")]
    [SerializeField] private Image notificationImage;
    
    [Tooltip("Длительность разгорания (0.4с)")]
    [SerializeField] private float blinkDuration = 0.4f;
    
    [Tooltip("Пауза между вспышками (минимум 0.8с)")]
    [SerializeField] private float pauseBetweenBlinks = 0.8f;
    
    [SerializeField] private float minAlpha = 0.0f;
    
    [Header("Звук")]
    [SerializeField] private SoundID blinkSound = SoundID.RedLight;

    [Header("Определение стейта вкл/выкл")]
    [SerializeField]
    private UIWindowAnimator _startOfDaypanelUiWindowAnimator;

    private Sequence _blinkSequence;

    protected override void Start()
    {
        base.Start();
        
        var startColor = notificationImage.color;
        startColor.a = minAlpha;
        notificationImage.color = startColor;
    }

    private void SetupAnimation()
    {
        // animation is playing
        if (_blinkSequence.IsActive())
            return;
        
        var defaultColor = notificationImage.color;
        var startColor = new Color(defaultColor.r, defaultColor.g, defaultColor.b, minAlpha);
        var endColor = new Color(defaultColor.r, defaultColor.g, defaultColor.b, 1.0f);
        
        _blinkSequence = DOTween.Sequence();
        _blinkSequence
            .SetLoops(-1, LoopType.Restart)                         // -1 == infinite
            .SetUpdate(UpdateType.Late, isIndependentUpdate: true) // independent == unscaled delta time
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)       // if gameObject is destroyed must kill sequence
            .AppendCallback(PlayBlinkSound)
            .Append(notificationImage.DOColor(startColor, 0f))
            .Append(notificationImage.DOColor(endColor, blinkDuration))
            .AppendInterval(pauseBetweenBlinks)
            .Play();
    }

    private void CancelAnimation()
    {
        if (_blinkSequence.IsActive())
            _blinkSequence.Kill();
    }

    private void Update()
    {
        if (PhoneManager.Instance == null)
            return;

        var isVisible = _startOfDaypanelUiWindowAnimator.IsVisible();
        var hasCalls = PhoneManager.Instance.HasActiveCalls;
        
        var shouldShow = isVisible && hasCalls;
        notificationOverlay.SetActive(shouldShow);

        if (shouldShow)
            SetupAnimation();
        else
            CancelAnimation();
    }

    private void PlayBlinkSound()
    {
        // Проверяем, что стол открыт (лампочка видима)
        if (notificationOverlay == null || !notificationOverlay.activeSelf)
        {
            Debug.Log("[DeskPhone] Лампочка не видима, звук не играем");
            return;
        }

        if (AudioManager.Instance != null && blinkSound != SoundID.None)
        {
            // Используем 2D звук для UI
            AudioManager.Instance.PlaySound(blinkSound);
        }
        else
        {
            Debug.LogWarning($"[DeskPhone] Невозможно.playSound: AudioManager={AudioManager.Instance != null}, blinkSound={blinkSound}");
        }
    }
}
