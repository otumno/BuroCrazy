using System.Collections.Generic;
using UnityEngine;
using Gameplay;
using Managers;
using Scriptables.Audio;

namespace Clinch
{

/// <summary>
/// Система клинчей - позволяет персонажам становиться мишенями для клика
/// и запускает таймер с визуальным предупреждением.
/// </summary>
public class ClinchTarget : MonoBehaviour
{
    /// <summary>Показывает, активен ли клинч в данный момент.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Массив из 3 кадров предупреждения (спрайты для анимации над головой).</summary>
    [Header("Warning Animation")]
    public Sprite[] warningFrames;

    /// <summary>SpriteRenderer для отображения предупреждения над головой персонажа.</summary>
    public SpriteRenderer warningRenderer;

    /// <summary>Материал для подсветки при наведении мыши.</summary>
    public Material highlightMaterial;

    /// <summary>Локальный UI для мини-игры клинча.</summary>
    public ClinchLocalUI localMiniGameUI;

    /// <summary>Корень индикатора (для скрытия при клике).</summary>
    public GameObject indicatorRoot;

    [Header("Tooltip")]
    public string tooltipText = "Разобраться с проблемой";
    public float displayDelay = 0.5f;

    [Header("Звуки и Интеракция")]
    public Scriptables.Audio.SoundID hoverSound = Scriptables.Audio.SoundID.UI_Hover;
    public Scriptables.Audio.SoundID clickSound = Scriptables.Audio.SoundID.UI_Click_Default;
    public Scriptables.Audio.SoundID alarmSound = Scriptables.Audio.SoundID.None;

    private float _clinchTimer;
    private float _clinchTimeout;
    private Dictionary<SpriteRenderer, Material> _originalMaterials = new Dictionary<SpriteRenderer, Material>();
    private bool _isHovered;
    private int _currentWarningFrame;
    private float _warningFrameTimer;
    private int _requiredColor;
    
    /// <summary>Флаг, что UI мини-игры открыт (таймер и звуки замирают)</summary>
    public bool isUIOpen = false;
    
    // Заготовленные фразы для реакций
    private readonly string[] staffSuccess = { "ААА вот как оно делается!", "Спасибо Шеф, поддержали!", "Век живи - век учись!", "Фух, пронесло..." };
    private readonly string[] staffFail = { "Я так и знал, это невозможно.", "Мне никто не поможет...", "Как же я устал...", "Всё пропало!" };
    private readonly string[] clientSuccess = { "Сразу надо было к вам!", "Вот это я понимаю руководство!", "Отличный сервис!", "Премного благодарен!" };
    private readonly string[] clientFail = { "Полная некомпетентность!!", "Я буду жаловаться!!", "Безобразие!", "Вы все здесь бездари!" };

    private void Awake()
    {
        // НЕ кэшируем _staff и _client - они могут меняться при смене ролей!
        if (warningRenderer != null)
        {
            warningRenderer.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsActive || isUIOpen || Time.timeScale == 0f) return;

        _clinchTimer -= Time.deltaTime;

        // Обновление анимации предупреждения
        UpdateWarningAnimation();

        // Проверка таймаута
        if (_clinchTimer <= 0f)
        {
            ResolveClinch(success: false, isTimeout: true);
        }
    }

    /// <summary>
    /// Запускает клинч: устанавливает IsActive=true, получает таймаут из AIBalanceConfig,
    /// запускает показ warningRenderer.
    /// </summary>
    public void TriggerClinch()
    {
        if (IsActive) return;

        IsActive = true;
        _clinchTimer = AIBalanceConfig.Instance.clinchTimeoutDuration;
        _clinchTimeout = _clinchTimer;
        _currentWarningFrame = 0;
        _warningFrameTimer = 0f;
        
        // Генерируем случайный индекс для замка (0, 1 или 2)
        _requiredColor = UnityEngine.Random.Range(0, 3);

        Debug.Log($"<color=cyan>[ClinchTarget]</color> TriggerClinch вызван на {gameObject.name}. Таймер: {_clinchTimer}с. Цвет: {_requiredColor}");

        if (indicatorRoot != null) indicatorRoot.SetActive(true);
        else Debug.LogWarning($"<color=yellow>[ClinchTarget]</color> indicatorRoot не назначен на {gameObject.name}! Клинч будет невидим.");

        if (warningRenderer != null && warningFrames != null && warningFrames.Length > 0)
        {
            warningRenderer.sprite = warningFrames[0];
            warningRenderer.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[ClinchTarget]</color> warningRenderer или warningFrames не назначены на {gameObject.name}!");
        }
    }

    /// <summary>
    /// Обновление анимации предупреждения - частота смены кадров ускоряется
    /// по мере истечения таймера (от 0.8сек в начале до 0.1сек к концу).
    /// Кадры выбираются случайно для большей хаотичности.
    /// </summary>
    private void UpdateWarningAnimation()
    {
        if (warningRenderer == null || warningFrames == null || warningFrames.Length == 0)
            return;

        // Прогресс от 0 (начало) до 1 (конец таймера)
        float progress = 1f - (_clinchTimer / _clinchTimeout);
        progress = Mathf.Clamp01(progress);

        // Интервал смены кадров: от 0.8сек в начале до 0.1сек к концу (более заметное ускорение)
        float frameInterval = Mathf.Lerp(0.8f, 0.1f, progress);

        _warningFrameTimer += Time.deltaTime;
        if (_warningFrameTimer >= frameInterval)
        {
            _warningFrameTimer = 0f;
            
            // Выбираем случайный кадр (не повторяя предыдущий)
            int newFrame;
            do
            {
                newFrame = UnityEngine.Random.Range(0, warningFrames.Length);
            } while (newFrame == _currentWarningFrame && warningFrames.Length > 1);
            
            _currentWarningFrame = newFrame;
            warningRenderer.sprite = warningFrames[_currentWarningFrame];
            
            // Проигрываем звук аларма (SoundID сам может содержать массив случайных вариаций)
            if (Managers.AudioManager.Instance != null && alarmSound != Scriptables.Audio.SoundID.None)
            {
                Managers.AudioManager.Instance.PlaySound(alarmSound, transform.position);
            }
        }
    }

    /// <summary>
    /// Устанавливает состояние наведения (hover) для подсветки персонажа.
    /// Вызывается из PlayerInputController.
    /// </summary>
    public void SetHover(bool state)
    {
        if (!IsActive) return;
        if (_isHovered == state) return;
        _isHovered = state;

        if (state)
        {
            _originalMaterials.Clear();
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer r in renderers)
            {
                _originalMaterials[r] = r.sharedMaterial;
                r.sharedMaterial = highlightMaterial;
            }
            if (Managers.AudioManager.Instance != null && hoverSound != Scriptables.Audio.SoundID.None)
                Managers.AudioManager.Instance.PlaySound(hoverSound, transform.position);
        }
        else
        {
            RestoreOriginalMaterials();
        }
    }

    /// <summary>
    /// Применяет эффекты клинча: вызывает DirectorManager.Instance.ApplyClinchEffect(success),
    /// меняет стресс у персонажа, скрывает warningRenderer, возвращает материалы,
    /// сбрасывает IsActive=false.
    /// </summary>
    /// <param name="success">true если игрок успешно разрешил клинч</param>
    /// <param name="isTimeout">true если клинч завершился по таймауту</param>
    public void ResolveClinch(bool success, bool isTimeout)
    {
        if (!IsActive)
            return;

        isUIOpen = false; // Сбрасываем флаг UI
        IsActive = false;

        // Скрываем локальный UI мини-игры
        if (localMiniGameUI != null)
        {
            localMiniGameUI.gameObject.SetActive(false);
        }

        // Скрываем warningRenderer
        if (warningRenderer != null)
        {
            warningRenderer.gameObject.SetActive(false);
        }

        // Возвращаем оригинальные материалы
        RestoreOriginalMaterials();

        // Получаем персонажей для реакций
        var staff = GetComponent<StaffController>();
        var client = GetComponent<ClientPathfinding>();

        // Реакции персонажей
        if (success)
        {
            // Воспроизводим звук успеха
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(SoundID.Success);
            
            // Применяем эффект через DirectorManager
            DirectorManager.Instance?.ApplyClinchEffect(success, isTimeout);
            
            // Реакции при успехе
            if (staff != null)
            {
                staff.thoughtBubble?.ShowPriorityMessage(staffSuccess[UnityEngine.Random.Range(0, staffSuccess.Length)], 4f, Color.green);
                staff.visuals?.SetEmotion(Emotion.Happy);
            }
            if (client != null)
            {
                client.ShowThoughtBubble(clientSuccess[UnityEngine.Random.Range(0, clientSuccess.Length)], 4f);
                client.GetVisuals()?.SetEmotion(Emotion.Happy);
            }
        }
        else
        {
            // Воспроизводим звук неудачи
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(SoundID.Fail);
            
            // Применяем эффект через DirectorManager
            DirectorManager.Instance?.ApplyClinchEffect(success, isTimeout);
            
            // Меняем стресс у персонажа
            ApplyStressEffect(success);
            
            // Реакции при провале
            if (staff != null)
            {
                staff.thoughtBubble?.ShowPriorityMessage(staffFail[UnityEngine.Random.Range(0, staffFail.Length)], 4f, Color.red);
                staff.visuals?.SetEmotion(Emotion.Sad);
            }
            if (client != null)
            {
                client.ShowThoughtBubble(clientFail[UnityEngine.Random.Range(0, clientFail.Length)], 4f);
                client.GetVisuals()?.SetEmotion(Emotion.Angry);
                // Принудительно отправляем клиента домой
                client.ForceLeave(ClientPathfinding.LeaveReason.Upset);
            }
        }

        _clinchTimer = 0f;
        _currentWarningFrame = 0;
        _warningFrameTimer = 0f;
    }

    private void RestoreOriginalMaterials()
    {
        foreach (var kvp in _originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.sharedMaterial = kvp.Value;
            }
        }
        _originalMaterials.Clear();
    }

    /// <summary>
    /// Применяет изменение стресса в зависимости от типа персонажа и результата клинча.
    /// Получает компоненты динамически (не кэшируем - могут меняться при смене ролей).
    /// </summary>
    private void ApplyStressEffect(bool success)
    {
        float stressChange = success
            ? AIBalanceConfig.Instance.clinchRewardStress
            : AIBalanceConfig.Instance.clinchPenaltyStress;

        var staff = GetComponent<StaffController>();
        var client = GetComponent<ClientPathfinding>();

        if (staff != null)
        {
            staff.ChangeStress(stressChange);
        }
        else if (client != null)
        {
            if (success)
            {
                client.RelieveStress(Mathf.Abs(stressChange));
            }
            else
            {
                client.ApplyStressJump(Mathf.Abs(stressChange));
            }
        }
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterials();
    }

    /// <summary>
    /// Открывает локальный UI мини-игры клинча. Вызывается из PlayerInputController
    /// при клике на активную мишень.
    /// </summary>
    /// <summary>
    /// Скрывает индикатор проблемы (например, при открытии мини-игры).
    /// </summary>
    public void HideIndicator()
    {
        if (indicatorRoot != null) indicatorRoot.SetActive(false);
    }
    
    /// <summary>
    /// Показывает индикатор проблемы (если клинч ещё активен).
    /// </summary>
    public void ShowIndicator()
    {
        if (indicatorRoot != null && IsActive) indicatorRoot.SetActive(true);
    }
    
    public void OpenUI()
    {
        if (!IsActive) return;
        isUIOpen = true;
        HideIndicator();
        
        // Проигрываем звук клика
        if (Managers.AudioManager.Instance != null && clickSound != Scriptables.Audio.SoundID.None)
            Managers.AudioManager.Instance.PlaySound(clickSound, transform.position);
        
        if (localMiniGameUI != null)
        {
            localMiniGameUI.Show(this, _requiredColor);
        }
    }
    
    /// <summary>
    /// Отменяет открытый UI мини-игры (например, при паузе или отмене).
    /// </summary>
    public void CancelClinchUI()
    {
        isUIOpen = false;
        ShowIndicator();
    }
}

}
