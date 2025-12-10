using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using Managers;

[System.Serializable]
public class PeriodVisual
{
    public CalendarDayPeriodType periodType; 
    public Sprite icon;
    public AudioClip transitionSound;
}

[RequireComponent(typeof(Image), typeof(AudioSource))]
public class IconClockUI : MonoBehaviour
{
    [Header("Визуальные элементы периодов")]
    public List<PeriodVisual> periodVisuals = new();

    [Header("Ссылки")]
    [SerializeField] private AudioSource audioSource;
    private Image clockImage;

    private void Awake()
    {
        clockImage = GetComponent<Image>();
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
        UpdateClock(TimeManager.Instance.GetCurrentPeriodSettings(), false);
    }

    private void OnPeriodChanged(PeriodSettings settings) => UpdateClock(settings, true);

    private void UpdateClock(PeriodSettings settings, bool playSound)
    {
        if (settings == null)
            return;

        var type = settings.PeriodType;

        // Ищем настройку в списке.
        // Так как это [Flags], используем проверку HasFlag или точное совпадение.
        // В данном случае лучше точное совпадение для иконки.
        var visual = periodVisuals.FirstOrDefault(v => v.periodType == type);
        if (visual == null)
            return;
        
        if (visual.icon != null) 
            clockImage.sprite = visual.icon;
            
        // Звук играем только если это не старт игры
        if (visual.transitionSound != null && playSound)
            audioSource.PlayOneShot(visual.transitionSound);
    }
    
    private void OnDestroy()
    {
        if (TimeManager.Instance)
            TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
    }
}