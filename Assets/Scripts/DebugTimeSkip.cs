// Файл: DebugTimeSkip.cs

using Managers;
using UnityEngine;

public class DebugTimeSkip : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private SingleDaySystem singleDaySystem;

    [Header("Keys")]
    [SerializeField]
    public KeyCode _skipPeriodKey = KeyCode.F10;
    [SerializeField]
    public KeyCode _skipDayKey = KeyCode.F11;

#if DEBUG_ENABLED
    private void Update()
    {
        if (!singleDaySystem || !singleDaySystem.isRunning)
            return;

        if (Input.GetKeyDown(_skipDayKey))
        {
            singleDaySystem.ForceFinishDay();
            return;
        }

        if (Input.GetKeyDown(_skipPeriodKey))
        {
            singleDaySystem.ForceFinishPeriod();
            return;
        }
    }
#endif
}