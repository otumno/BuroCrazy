using UnityEngine;
using TMPro;

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
            Debug.LogWarning("ClerkNotification не нашел компоненты ClerkController или TextMeshPro", gameObject);
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
            case ClerkController.ClerkState.Working:
            case ClerkController.ClerkState.ReturningToWork:
                return "§";
            case ClerkController.ClerkState.GoingToToilet:
            case ClerkController.ClerkState.AtToilet:
                return "!";
            case ClerkController.ClerkState.GoingToBreak:
            case ClerkController.ClerkState.OnBreak:
                string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
                if (period == "ночь") return "*";
                if (period == "обед") return "L";
                return ""; // На всякий случай
            default:
                return "";
        }
    }
    
    private Color GetStateColor()
    {
        var state = parent.GetCurrentState();
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
                return Color.white;
        }
    }
}