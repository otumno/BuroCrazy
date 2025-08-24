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
        
        var state = parent.GetCurrentState();
        notificationText.text = GetStateText(state);
        notificationText.color = GetStateColor(state);
    }

    private string GetStateText(InternController.InternState state)
    {
        switch (state)
        {
            case InternController.InternState.Patrolling: return "P";
            case InternController.InternState.HelpingConfused:
            case InternController.InternState.TalkingToConfused:
                return "?";
            case InternController.InternState.ServingFromQueue: return "!";
            case InternController.InternState.CoveringDesk:
            case InternController.InternState.Working:
                return "§";
            case InternController.InternState.OnBreak: return "L";
            case InternController.InternState.AtToilet: return "!";
            case InternController.InternState.Inactive: return "*";
            default: return "...";
        }
    }

    private Color GetStateColor(InternController.InternState state)
    {
        switch (state)
        {
            case InternController.InternState.Patrolling: return Color.white;
            
            case InternController.InternState.HelpingConfused:
                return Color.yellow; // Желтый, когда идет к потеряшке
            case InternController.InternState.TalkingToConfused:
                return Color.magenta; // Фиолетовый, когда "разговаривает"

            case InternController.InternState.ServingFromQueue:
                return Color.yellow;
            case InternController.InternState.CoveringDesk:
            case InternController.InternState.Working:
                return Color.green;
            case InternController.InternState.OnBreak:
            case InternController.InternState.Inactive:
                return Color.cyan;
            case InternController.InternState.AtToilet:
                return Color.yellow;
            default:
                return Color.grey;
        }
    }
}