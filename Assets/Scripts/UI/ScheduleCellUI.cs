using UnityEngine;
using UnityEngine.UI;
using Data.Calendar;
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
    private UI.SchedulePendingHatch pendingHatch;
    
    private CalendarDayPeriodType displayedMask;
    private int periodIndex;
    private bool editable;

    public void Setup(StaffController staff, CalendarDayPeriodType period, StaffScheduleRowUI parentRow, bool isEditable)
    {
        editable = isEditable;
        _staff = staff;
        _periodType = period;
        _parentRow = parentRow;
        if (pendingHatch == null && fillImage != null)
        {
            var hatchObject = new GameObject("PendingShift", typeof(RectTransform), typeof(UI.SchedulePendingHatch));
            hatchObject.layer = gameObject.layer;
            hatchObject.transform.SetParent(fillImage.transform, false);
            var rect = (RectTransform)hatchObject.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            pendingHatch = hatchObject.GetComponent<UI.SchedulePendingHatch>();
            pendingHatch.color = new Color(1, 1, 1, .4f);
            pendingHatch.raycastTarget = false;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnCellClicked);
            button.interactable = isEditable;
        }
        
        periodIndex = -1;
        for (int i = 0; i < parentRow.PeriodTypes.Count; i++)
            if (parentRow.PeriodTypes[i] == period) { periodIndex = i; break; }
        UpdateVisuals();
    }

    private void Update()
    {
        if (_staff != null && displayedMask != _staff.WorkShiftMask) UpdateVisuals();
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
        displayedMask = _staff.WorkShiftMask;
        if (button != null) button.interactable = editable &&
            (!_staff.IsTemporary || ShiftPlanning.GetUpdatedMask(_staff, periodIndex) != displayedMask);
        fillImage.enabled = amIActive;
        pendingHatch.enabled = amIActive;

        if (amIActive && periodIndex >= 0)
        {
            var periods = _parentRow.PeriodTypes;
            var prevType = periods[(periodIndex - 1 + periods.Count) % periods.Count];
            var nextType = periods[(periodIndex + 1) % periods.Count];
            bool isPrevActive = (_staff.WorkShiftMask & prevType) != 0;
            bool isNextActive = (_staff.WorkShiftMask & nextType) != 0;

            if (!isPrevActive && !isNextActive) fillImage.sprite = spriteSolo;
            else if (!isPrevActive && isNextActive) fillImage.sprite = spriteHead;
            else if (isPrevActive && isNextActive) fillImage.sprite = spriteBody;
            else if (isPrevActive && !isNextActive) fillImage.sprite = spriteTail;
        }
    }
}
