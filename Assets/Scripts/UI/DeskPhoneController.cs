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

    private bool _isBlinking;
    private float _timer;
    private bool _wasRinging;
    private bool _soundPlayedInCycle;

    protected override void Start()
    {
        base.Start();
        if (notificationImage == null && notificationOverlay != null)
        {
            notificationImage = notificationOverlay.GetComponent<Image>();
        }
        SetAlpha(minAlpha);
    }

    private void Update()
    {
        if (PhoneManager.Instance == null) return;

        bool isRinging = PhoneManager.Instance.HasActiveCalls;

        if (isRinging && !_wasRinging)
        {
            // Начало звонка - включаем мигание
            _isBlinking = true;
            _timer = 0f;
            _soundPlayedInCycle = false;
        }
        else if (!isRinging && _wasRinging)
        {
            // Конец звонка
            _isBlinking = false;
            SetAlpha(minAlpha);
        }
        _wasRinging = isRinging;

        // Визуальная обводка (базовая логика)
        SetNotificationState(isRinging);

        // Анимация мигания + звук в начале каждого цикла
        if (_isBlinking && notificationImage != null)
        {
            HandleBlinkCycle();
        }
    }

    private void HandleBlinkCycle()
    {
        _timer += Time.unscaledDeltaTime;
        float totalCycle = blinkDuration + pauseBetweenBlinks;

        // Проверяем начало нового цикла (лампочка начинает разгораться)
        bool isStartOfCycle = _timer < Time.unscaledDeltaTime;

        if (_timer <= blinkDuration)
        {
            // ФАЗА 1: Разгорание (0.0 -> 1.0)
            float t = _timer / blinkDuration;
            SetAlpha(Mathf.Lerp(minAlpha, 1f, t));
            
            // ЗВУК: играем в начале разгорания, если лампочка видима
            if (isStartOfCycle && !_soundPlayedInCycle && notificationOverlay != null && notificationOverlay.activeSelf)
            {
                PlayBlinkSound();
                _soundPlayedInCycle = true;
            }
        }
        else if (_timer <= totalCycle)
        {
            // ФАЗА 2: Пауза (лампа выключена)
            SetAlpha(minAlpha);
        }
        else
        {
            // Сброс цикла для следующего повтора
            _timer = 0f;
            _soundPlayedInCycle = false;
        }
    }

    private void SetAlpha(float a)
    {
        if (notificationImage == null) return;
        Color c = notificationImage.color;
        c.a = a;
        notificationImage.color = c;
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
            Debug.Log($"[DeskPhone] Играем звук: {blinkSound}");
            // Используем 2D звук для UI
            AudioManager.Instance.PlaySound(blinkSound);
        }
        else
        {
            Debug.LogWarning($"[DeskPhone] Невозможно.playSound: AudioManager={AudioManager.Instance != null}, blinkSound={blinkSound}");
        }
    }

    public void OpenPhoneInterface()
    {
        if (PhoneManager.Instance?.phonePanelUI != null)
        {
            PhoneManager.Instance.phonePanelUI.Show();
        }
    }
}
