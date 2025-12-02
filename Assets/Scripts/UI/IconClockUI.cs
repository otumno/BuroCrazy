using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using Managers;

[System.Serializable]
public class PeriodVisual
{
    // ИСПРАВЛЕНИЕ: Убрали string periodName. 
    // Теперь в Инспекторе будет выпадающий список из Enum.
    public CalendarDayPeriodType periodType; 
    public Sprite icon;
    public AudioClip transitionSound;
}

[RequireComponent(typeof(Image), typeof(AudioSource))]
public class IconClockUI : MonoBehaviour
{
    [Header("Визуальные элементы периодов")]
    public List<PeriodVisual> periodVisuals;

    [Header("Ссылки")]
    [SerializeField] private AudioSource audioSource;
    private Image clockImage;

    private CalendarDayPeriodType _currentType;

    private void Awake()
    {
        clockImage = GetComponent<Image>();
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnPeriodChanged += UpdateClock;
            // Обновляем сразу при включении
            UpdateClock(TimeManager.Instance.GetCurrentPeriodSettings());
        }
    }

    private void OnDisable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnPeriodChanged -= UpdateClock;
        }
    }

    private void UpdateClock(PeriodSettings settings)
    {
        if (settings == null) return;

        var type = settings.PeriodType;
        
        // Если период не изменился, ничего не делаем (оптимизация)
        if (type == _currentType) return;

        // Ищем настройку в списке.
        // Так как это [Flags], используем проверку HasFlag или точное совпадение.
        // В данном случае лучше точное совпадение для иконки.
        PeriodVisual visual = periodVisuals.FirstOrDefault(v => v.periodType == type);

        if (visual != null)
        {
            if (visual.icon != null) 
                clockImage.sprite = visual.icon;
            
            // Звук играем только если это не старт игры
            if (visual.transitionSound != null && audioSource != null && _currentType != CalendarDayPeriodType.None)
                audioSource.PlayOneShot(visual.transitionSound);
        }

        _currentType = type;
    }
}