// Файл: ServiceWorkerNotification.cs
using UnityEngine;
using TMPro;

[RequireComponent(typeof(ServiceWorkerController))]
public class ServiceWorkerNotification : MonoBehaviour
{
    private ServiceWorkerController parent;
    private TextMeshPro notificationText;

    void Start()
    {
        parent = GetComponent<ServiceWorkerController>();
        notificationText = GetComponentInChildren<TextMeshPro>();
        if (parent == null || notificationText == null) 
        { 
            enabled = false;
        }
    }

    void Update()
    {
        if (notificationText == null || parent == null) return;
        
        // --- ИСПРАВЛЕНО: Теперь мы снова вызываем GetCurrentState(), который возвращает правильный enum ---
        var state = parent.GetCurrentState();
        notificationText.text = GetStateText(state);
        notificationText.color = GetStateColor(state);
    }

    private string GetStateText(ServiceWorkerController.WorkerState state)
    {
        bool useEmoji = NotificationStyleManager.useEmojiStyle;
        switch (state)
        {
            case ServiceWorkerController.WorkerState.Idle:
                return useEmoji ? "👀" : "S"; // Ищет работу
            case ServiceWorkerController.WorkerState.GoingToMess:
            case ServiceWorkerController.WorkerState.Cleaning:
                return useEmoji ? "🧹" : "C"; // Убирает
            case ServiceWorkerController.WorkerState.GoingToToilet:
            case ServiceWorkerController.WorkerState.AtToilet:
                return useEmoji ? "😖" : "!"; // В туалете
            case ServiceWorkerController.WorkerState.StressedOut:
                return useEmoji ? "😡" : "!!!"; // В срыве
            case ServiceWorkerController.WorkerState.OffDuty:
                return useEmoji ? "😔" : "*"; // Смена окончена
            default:
                return "...";
        }
    }
    
    private Color GetStateColor(ServiceWorkerController.WorkerState state)
    {
        switch (state)
        {
            case ServiceWorkerController.WorkerState.Cleaning:
            case ServiceWorkerController.WorkerState.GoingToMess:
                return Color.green;
            case ServiceWorkerController.WorkerState.AtToilet:
            case ServiceWorkerController.WorkerState.GoingToToilet:
                return Color.yellow;
            case ServiceWorkerController.WorkerState.StressedOut:
                return Color.red;
            default:
                return Color.white;
        }
    }
}