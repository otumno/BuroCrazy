// Файл: InternNotification.cs
using UnityEngine;
using TMPro;

[RequireComponent(typeof(InternController))]
public class InternNotification : MonoBehaviour
{
    private InternController parent;
    private TextMeshPro notificationText;

    void Start()
    {
        parent = GetComponent<InternController>();
        notificationText = GetComponentInChildren<TextMeshPro>();
        if (parent == null || notificationText == null) 
        { 
            enabled = false;
        }
    }

    void Update()
    {
        if (notificationText == null || parent == null) return;
        
        // Теперь этот вызов будет работать, так как метод GetCurrentState() существует
        var state = parent.GetCurrentState(); 
        
        notificationText.text = GetStateText(state);
        notificationText.color = GetStateColor(state);
    }

    private string GetStateText(InternController.InternState state)
    {
        bool useEmoji = NotificationStyleManager.useEmojiStyle;
        switch (state)
        {
            case InternController.InternState.Patrolling: return useEmoji ? "🚶" : "P";
            case InternController.InternState.HelpingConfused: return useEmoji ? "🧐" : "?";
            case InternController.InternState.TalkingToConfused: return useEmoji ? "💡" : "?";
            case InternController.InternState.ServingFromQueue: return useEmoji ? "📋" : "!";
            case InternController.InternState.CoveringDesk: case InternController.InternState.Working: return useEmoji ? "😑" : "§";
            case InternController.InternState.OnBreak: return useEmoji ? "🍔" : "L";
            case InternController.InternState.AtToilet: return useEmoji ? "😖" : "!";
            case InternController.InternState.Inactive: return useEmoji ? "😔" : "*";
            case InternController.InternState.GoingToBreak:
            case InternController.InternState.GoingToToilet:
            case InternController.InternState.ReturningToPatrol:
                return useEmoji ? "🚶" : "...";
            default: return "...";
        }
    }

    private Color GetStateColor(InternController.InternState state)
    {
        switch (state)
        {
            case InternController.InternState.Patrolling: return Color.white;
            case InternController.InternState.HelpingConfused: return Color.yellow;
            case InternController.InternState.TalkingToConfused: return Color.magenta;
            case InternController.InternState.ServingFromQueue: return Color.yellow;
            case InternController.InternState.CoveringDesk: case InternController.InternState.Working: return Color.green;
            case InternController.InternState.OnBreak: case InternController.InternState.Inactive: return Color.cyan;
            case InternController.InternState.AtToilet: return Color.yellow;
            default: return Color.grey;
        }
    }
}