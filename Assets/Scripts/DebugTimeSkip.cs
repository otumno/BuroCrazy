// Файл: DebugTimeSkip.cs

using Managers;
using UnityEngine;

public class DebugTimeSkip : MonoBehaviour
{
    [Tooltip("Клавиша для переключения на следующий период")]
    public KeyCode skipKey = KeyCode.F10;

    void Update()
    {
        if (Input.GetKeyDown(skipKey))
        {
            if (DayPeriodManager.Instance != null)
            {
                Debug.Log($"<color=orange>DEBUG: Принудительный переход на следующий период...</color>");
                DayPeriodManager.Instance.SkipCurrentPeriod();
            }
        }
    }
}