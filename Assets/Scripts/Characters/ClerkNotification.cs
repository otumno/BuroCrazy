// Файл: ClerkNotification.cs
using UnityEngine;
using TMPro;

[RequireComponent(typeof(ClerkController))]
public class ClerkNotification : MonoBehaviour
{
    private ClerkController parent;
    private TextMeshPro notificationText;
    
    void Start()
    {
        parent = GetComponent<ClerkController>();
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

    private string GetStateText(ClerkController.ClerkState state)
    {
        bool useEmoji = NotificationStyleManager.useEmojiStyle;
        switch (state)
        {
            case ClerkController.ClerkState.Working:
            case ClerkController.ClerkState.ReturningToWork:
                if (parent.clerkRole == ClerkController.ClerkRole.Cashier) return useEmoji ? "😑" : "$";
                return useEmoji ? "😑" : "§";
            case ClerkController.ClerkState.GoingToToilet:
            case ClerkController.ClerkState.AtToilet:
                return useEmoji ? "😖" : "!";
            case ClerkController.ClerkState.GoingToBreak:
            case ClerkController.ClerkState.OnBreak:
                return useEmoji ? "😖" : "L";
            case ClerkController.ClerkState.Inactive:
                return useEmoji ? "😔" : "*";
            default:
                return "...";
        }
    }
    
    private Color GetStateColor(ClerkController.ClerkState state)
    {
        switch (state)
        {
            case ClerkController.ClerkState.Working:
            case ClerkController.ClerkState.ReturningToWork:
                return Color.green;
            case ClerkController.ClerkState.GoingToToilet:
            case ClerkController.ClerkState.AtToilet:
                return Color.yellow;
            case ClerkController.ClerkState.GoingToBreak:
            case ClerkController.ClerkState.OnBreak:
                return Color.cyan;
            default:
                return Color.grey;
        }
    }
}