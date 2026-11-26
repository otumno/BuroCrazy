using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Data.Calendar;
using Managers;
using Scriptables.Audio;

public class StaffScheduleRowUI : MonoBehaviour
{
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private SoundID hatchSoundId = SoundID.UI_Click_Default;
	
	[Tooltip("Задержка между закрашиванием ячеек (в секундах). Чем больше - тем медленнее.")]
    [SerializeField] private float animationStepDelay = 0.2f; // Попробуй 0.15f или 0.2f
    
    private List<ScheduleCellUI> _cells = new List<ScheduleCellUI>();
    private List<CalendarDayPeriodType> _periodTypes = new List<CalendarDayPeriodType>();
    private StaffController _staff;
    private bool _isEditable;

    public void Setup(StaffController staff, List<PeriodSettings> periods, bool isEditable)
    {
        _staff = staff;
        _isEditable = isEditable;
        _cells.Clear();
        _periodTypes.Clear();
        
        foreach (Transform child in transform) Destroy(child.gameObject);

        foreach (var period in periods) _periodTypes.Add(period.PeriodType);
        foreach (var period in periods)
        {

            GameObject cellObj = Instantiate(cellPrefab, transform);
            ScheduleCellUI cellUI = cellObj.GetComponent<ScheduleCellUI>();
            
            if (cellUI != null)
            {
                cellUI.Setup(staff, period.PeriodType, this, isEditable);
                _cells.Add(cellUI);
            }
        }
    }

    public IReadOnlyList<CalendarDayPeriodType> PeriodTypes => _periodTypes;

    public void RefreshAllCells()
    {
        foreach (var cell in _cells) cell.UpdateVisuals();
    }

    // ИЗМЕНЕНИЕ: Принимаем объект ячейки
    public void StartShiftAnimation(ScheduleCellUI startCell)
    {
        if (_staff == null || !_isEditable) return;
        StopAllCoroutines();
        StartCoroutine(AnimateShiftFill(startCell));
    }

    private IEnumerator AnimateShiftFill(ScheduleCellUI startCell)
    {
        int startIndex = _cells.IndexOf(startCell);
        if (startIndex < 0 || !ShiftPlanning.Apply(_staff, startIndex)) yield break;
        int shiftDuration = HiringManager.GetShiftPeriodsCount(_staff.currentRank);
        RefreshAllCells();
        for (int i = 0; i < Mathf.Min(shiftDuration, _periodTypes.Count); i++)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(hatchSoundId); 
            }

            // ИСПРАВЛЕНИЕ: Используем Realtime, чтобы работало на паузе
            yield return new WaitForSecondsRealtime(animationStepDelay);
        }
    }
}
