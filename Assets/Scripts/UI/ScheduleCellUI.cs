using UnityEngine;
using UnityEngine.UI;
using Data.Calendar;
using System.Collections.Generic;
using System.Linq;
using Managers;

public class ScheduleCellUI : MonoBehaviour
{
    [Header("Компоненты")]
    [Tooltip("Image для отображения смены. RaycastTarget OFF.")]
    [SerializeField] private Image fillImage; 
    [Tooltip("Кнопка самой ячейки.")]
    [SerializeField] private Button button;
    
    [Header("Подсветка времени")]
    [Tooltip("Image поверх ячейки, показывающий текущее время. RaycastTarget OFF.")]
    [SerializeField] private Image currentPeriodOverlay; 

    [Header("Спрайты для соединения")]
    [SerializeField] private Sprite spriteHead;
    [SerializeField] private Sprite spriteBody;
    [SerializeField] private Sprite spriteTail;
    [SerializeField] private Sprite spriteSolo;

    private StaffController _staff;
    private CalendarDayPeriodType _periodType;
    private StaffScheduleRowUI _parentRow;
    
    private static List<CalendarDayPeriodType> _cachedAllPeriods;

    public void Setup(StaffController staff, CalendarDayPeriodType period, StaffScheduleRowUI parentRow)
    {
        _staff = staff;
        _periodType = period;
        _parentRow = parentRow;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnCellClicked);
        }
        
        if (_cachedAllPeriods == null && TimeManager.Instance != null)
        {
            _cachedAllPeriods = TimeManager.Instance.mainCalendarDay.periodSettings
                                .Select(p => p.PeriodType).ToList();
        }

        UpdateVisuals();
    }

    private void Update()
    {
        if (currentPeriodOverlay != null && TimeManager.Instance != null)
        {
            bool isNow = TimeManager.Instance.GetCurrentPeriodType() == _periodType;
            if (currentPeriodOverlay.enabled != isNow) currentPeriodOverlay.enabled = isNow;
        }
    }

    private void OnCellClicked()
    {
        // ИЗМЕНЕНИЕ: Передаем СЕБЯ (this), чтобы строка точно знала, кто нажат
        if (_parentRow != null)
        {
            _parentRow.StartShiftAnimation(this);
        }
    }

    public void UpdateVisuals()
    {
        if (_staff == null || fillImage == null) return;

        bool amIActive = (_staff.WorkShiftMask & _periodType) != 0;
        fillImage.enabled = amIActive;

        if (amIActive && _cachedAllPeriods != null && _cachedAllPeriods.Count > 0)
        {
            int myIndex = _cachedAllPeriods.IndexOf(_periodType);
            
            int prevIndex = (myIndex - 1 + _cachedAllPeriods.Count) % _cachedAllPeriods.Count;
            var prevType = _cachedAllPeriods[prevIndex];
            bool isPrevActive = (_staff.WorkShiftMask & prevType) != 0;

            int nextIndex = (myIndex + 1) % _cachedAllPeriods.Count;
            var nextType = _cachedAllPeriods[nextIndex];
            bool isNextActive = (_staff.WorkShiftMask & nextType) != 0;

            if (!isPrevActive && !isNextActive) fillImage.sprite = spriteSolo;
            else if (!isPrevActive && isNextActive) fillImage.sprite = spriteHead;
            else if (isPrevActive && isNextActive) fillImage.sprite = spriteBody;
            else if (isPrevActive && !isNextActive) fillImage.sprite = spriteTail;
        }
    }
}