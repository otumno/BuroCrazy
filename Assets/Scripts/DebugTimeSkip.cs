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
            // --- ИСПРАВЛЕНИЕ: Обращаемся к TimeManager вместо ClientSpawner ---
            if (TimeManager.Instance != null)
            {
                Debug.Log($"<color=orange>DEBUG: Принудительный переход на следующий период...</color>");
                TimeManager.Instance.GoToNextPeriod();
            }
        }
    }
}