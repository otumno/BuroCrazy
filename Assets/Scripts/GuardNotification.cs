using UnityEngine;
using TMPro;

public class GuardNotification : MonoBehaviour
{
    private GuardMovement parent;
    private TextMeshPro notificationText;

    void Start()
    {
        parent = GetComponent<GuardMovement>();
        notificationText = GetComponentInChildren<TextMeshPro>();
        if (parent == null || notificationText == null)
        {
            Debug.LogWarning("GuardNotification не нашел компоненты GuardMovement или TextMeshPro", gameObject);
            enabled = false;
        }
    }
    
    void Update()
    {
        if (notificationText == null || parent == null) return;
        notificationText.text = GetStateText();
        notificationText.color = GetStateColor();
    }

    private string GetStateText()
    {
        var state = parent.GetCurrentState();
        switch (state)
        {
            case GuardMovement.GuardState.Patrolling: return "P";
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
                return "#";
            case GuardMovement.GuardState.OnPost:
                string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
                if (period == "ночь") return "*";
                if (period == "обед") return "L";
                return "S"; // Стандартный символ для поста, если период не определен
            case GuardMovement.GuardState.WaitingAtWaypoint:
                return "...";
            default: return "";
        }
    }
    
    private Color GetStateColor()
    {
        var state = parent.GetCurrentState();
        switch (state)
        {
            case GuardMovement.GuardState.Patrolling: return Color.white;
            case GuardMovement.GuardState.OnPost: return Color.cyan;
            case GuardMovement.GuardState.Chasing:
            case GuardMovement.GuardState.Talking:
                return Color.blue;
            case GuardMovement.GuardState.WaitingAtWaypoint: return Color.grey;
            default: return Color.white;
        }
    }
}