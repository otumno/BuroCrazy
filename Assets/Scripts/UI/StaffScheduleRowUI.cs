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

    public void Setup(StaffController staff, List<PeriodSettings> periods)
    {
        _staff = staff;
        _cells.Clear();
        _periodTypes.Clear();
        
        foreach (Transform child in transform) Destroy(child.gameObject);

        foreach (var period in periods)
        {
            _periodTypes.Add(period.PeriodType);

            GameObject cellObj = Instantiate(cellPrefab, transform);
            ScheduleCellUI cellUI = cellObj.GetComponent<ScheduleCellUI>();
            
            if (cellUI != null)
            {
                cellUI.Setup(staff, period.PeriodType, this);
                _cells.Add(cellUI);
            }
        }
    }

    public void RefreshAllCells()
    {
        foreach (var cell in _cells) cell.UpdateVisuals();
    }

    // ИЗМЕНЕНИЕ: Принимаем объект ячейки
    public void StartShiftAnimation(ScheduleCellUI startCell)
    {
        if (_staff == null) return;
        StopAllCoroutines();
        StartCoroutine(AnimateShiftFill(startCell));
    }

    private IEnumerator AnimateShiftFill(ScheduleCellUI startCell)
    {
        // 1. Стираем старое
        _staff.WorkShiftMask = 0;
        RefreshAllCells();

        // 2. Ищем индекс
        int startIndex = _cells.IndexOf(startCell);
        if (startIndex == -1) yield break;

        int shiftDuration = _staff.currentRank != null ? _staff.currentRank.workPeriodsCount : 3;

        // 3. Рисуем
        for (int i = 0; i < shiftDuration; i++)
        {
            int currentIndex = (startIndex + i) % _periodTypes.Count;
            var targetPeriod = _periodTypes[currentIndex];

            _staff.WorkShiftMask |= targetPeriod;
            RefreshAllCells();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(hatchSoundId); 
            }

            // ИСПРАВЛЕНИЕ: Используем Realtime, чтобы работало на паузе
            yield return new WaitForSecondsRealtime(animationStepDelay);
        }
    }
}