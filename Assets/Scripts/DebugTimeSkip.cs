// Файл: DebugTimeSkip.cs

using Managers;
using UnityEngine;

public class DebugTimeSkip : MonoBehaviour
{
#if DEBUG_ENABLED
    [Header("Skip Day")]
    public KeyCode skipDay = KeyCode.F10;
    
    [Header("Skip Day")]
    public KeyCode skipPeriod = KeyCode.F11;

    private void Update()
    {
        if (CalendarManager.Instance == null || DayPeriodManager.Instance == null)
            return;

        if (Input.GetKeyDown(skipDay))
        {
            Debug.Log("Debug skip day++");
            CalendarManager.Instance.AdvanceDay();
            return;
        }

        if (Input.GetKeyDown(skipPeriod))
        {
            Debug.Log("Debug Skip period ++");
            DayPeriodManager.Instance.SkipCurrentPeriod();
        }
    }
#endif
}