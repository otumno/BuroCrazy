using Managers;
using UnityEngine;

public class DebugTimeSkip : MonoBehaviour
{
#if DEBUG_ENABLED
    [Header("Skip Day")]
    public KeyCode skipDay = KeyCode.F10;

    [Header("Skip Period")]
    public KeyCode skipPeriod = KeyCode.F11;

    void Update()
    {
        if (CalendarManager.Instance == null || TimeManager.Instance == null)
            return;

        if (Input.GetKeyDown(skipDay))
        {
            Debug.Log("[DebugTimeSkip] Пропуск до следующего дня");
            SkipToNextDay();
            return;
        }

        if (Input.GetKeyDown(skipPeriod))
        {
            Debug.Log("[DebugTimeSkip] Пропуск периода");
            // Через RequestNextPeriod, чтобы пропуск ночи тоже запускал экраны конца дня.
            TimeManager.Instance.RequestNextPeriod();
        }
    }

    // Почему не CalendarManager.AdvanceDay: он меняет только номер дня, а периоды, смены сотрудников
    // и экраны конца дня остаются как были. Здесь честно проходим все периоды до смены дня.
    private static void SkipToNextDay()
    {
        var timeManager = TimeManager.Instance;
        int startDay = timeManager.GetCurrentDay();
        int periodsCount = timeManager.mainCalendarDay.periodSettings.Count;

        // Останавливаемся, когда день сменился или его смену задержали экраны конца дня.
        for (int i = 0; i < periodsCount; i++)
        {
            if (timeManager.GetCurrentDay() != startDay || timeManager.IsWaitingForDayEnd) return;

            timeManager.RequestNextPeriod();
        }
    }
#endif
}
