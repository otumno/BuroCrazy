// Файл: GuardNotification.cs
using UnityEngine;
using TMPro;

[RequireComponent(typeof(GuardMovement))]
public class GuardNotification : MonoBehaviour
{
    private GuardMovement parent;
    private TextMeshPro notificationText;

    void Start()
    {
        parent = GetComponent<GuardMovement>();
        notificationText = GetComponentInChildren<TextMeshPro>();
        if (parent == null || notificationText == null) { enabled = false; }
    }
    
    void Update()
    {
        if (notificationText == null || parent == null) return;
        var state = parent.GetCurrentState();
        notificationText.text = GetStateText(state);
        notificationText.color = GetStateColor(state);
    }

    private string GetStateText(GuardMovement.GuardState state)
    {
        bool useEmoji = NotificationStyleManager.useEmojiStyle;
        switch (state)
        {
            case GuardMovement.GuardState.Patrolling: return useEmoji ? "👀" : "P";
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
            case GuardMovement.GuardState.ChasingThief:
            case GuardMovement.GuardState.EscortingThief:
                return useEmoji ? "🚨" : "#";
            case GuardMovement.GuardState.OnPost:
            case GuardMovement.GuardState.OnBreak:
            // --- ИЗМЕНЕНИЕ: Добавлено состояние движения на обед ---
            case GuardMovement.GuardState.GoingToBreak:
                return useEmoji ? "🥪" : "S";
            case GuardMovement.GuardState.GoingToToilet:
            case GuardMovement.GuardState.AtToilet:
                // --- ИЗМЕНЕНИЕ: Установлен эмодзи 😖 в соответствии с запросом ---
                return useEmoji ? "😖" : "!";
            case GuardMovement.GuardState.OffDuty: return useEmoji ? "😔" : "*";
            case GuardMovement.GuardState.WaitingAtWaypoint:
                return useEmoji ? "😐" : "...";
            default: return "...";
        }
    }
    
    private Color GetStateColor(GuardMovement.GuardState state)
    {
        switch (state)
        {
            case GuardMovement.GuardState.Patrolling: return Color.white;
            case GuardMovement.GuardState.OnPost: return Color.cyan;
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
            case GuardMovement.GuardState.ChasingThief:
            case GuardMovement.GuardState.EscortingThief:
                return Color.blue;
            case GuardMovement.GuardState.GoingToToilet:
            case GuardMovement.GuardState.AtToilet:
                return Color.yellow;
            default:
                return Color.grey;
        }
    }
}