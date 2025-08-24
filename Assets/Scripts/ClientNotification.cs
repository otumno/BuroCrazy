using UnityEngine;
using TMPro;

public class ClientNotification : MonoBehaviour
{
    private ClientPathfinding parent;
    private TextMeshPro notificationText;
    public TextMeshPro queueNumberText;
    private int queueNumber = -1;
    private Color swampGreen = new Color(0.3f, 0.4f, 0.2f); // Болотный цвет для наглецов

    public void Initialize(ClientPathfinding p, TextMeshPro t) { parent = p; notificationText = t; }
    
    public void UpdateNotification()
    {
        if (notificationText == null || parent == null || parent.stateMachine == null) return;
        
        string stateText = GetStateText();
        notificationText.text = stateText;
        notificationText.color = GetStateColor();
        
        if (queueNumberText != null)
        {
            ClientState cs = parent.stateMachine.GetCurrentState();
            Waypoint goal = parent.stateMachine.GetCurrentGoal();
            bool isGoingToReg = (cs == ClientState.MovingToGoal && goal != null && goal.gameObject == parent.queueManager.GetRegistrationZone());
            
            bool showQueue = (cs == ClientState.AtWaitingArea || cs == ClientState.SittingInWaitingArea || cs == ClientState.MovingToSeat || cs == ClientState.AtRegistration || isGoingToReg) && queueNumber >= 0;
            
            queueNumberText.text = showQueue ? queueNumber.ToString() : "";
        }
    }

    private string GetStateText()
    {
        ClientState state = parent.stateMachine.GetCurrentState();
        if (state == ClientState.MovingToGoal || state == ClientState.ReturningToWait) { Waypoint goal = parent.stateMachine.GetCurrentGoal(); if (goal != null && parent.queueManager != null) { if (goal.gameObject == parent.queueManager.GetWaitingZone()) return "W"; if (goal.gameObject == parent.queueManager.GetRegistrationZone()) return "R"; if (goal == parent.queueManager.GetDesk1Waypoint()) return "1"; if (goal == parent.queueManager.GetDesk2Waypoint()) return "2"; if (goal.gameObject == parent.queueManager.GetToiletZone()) return "!"; LimitedCapacityZone lz = goal.GetComponentInParent<LimitedCapacityZone>(); if (lz != null && goal == lz.waitingWaypoint) return "?"; } }
        
        switch (state)
        {
            case ClientState.AtWaitingArea:
            case ClientState.SittingInWaitingArea:
            case ClientState.MovingToSeat:
                return "Zz";

            case ClientState.MovingToRegistrarImpolite:
                return "R";
                
            case ClientState.Spawning: return "?";
            case ClientState.AtToilet: return "!";
            case ClientState.Confused: return "?";
            case ClientState.AtRegistration: return "R";
            case ClientState.PassedRegistration: return "O";
            case ClientState.ReturningToWait: return "...";
            case ClientState.AtDesk1: return "1";
            case ClientState.AtDesk2: return "2";
            case ClientState.AtLimitedZoneEntrance: return "...";
            case ClientState.InsideLimitedZone: return "!";
            case ClientState.Enraged: return "@";
            case ClientState.Leaving: 
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Angry) return "@";
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.CalmedDown) return "☹";
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Processed) return "O";
                return "o";
            default: return "";
        }
    }
    
    private Color GetStateColor()
    {
        ClientState state = parent.stateMachine.GetCurrentState();
        Waypoint goal = parent.stateMachine.GetCurrentGoal();
        if (state == ClientState.MovingToGoal && goal != null && goal.gameObject == parent.queueManager.GetRegistrationZone()) return new Color(0.5f, 0.7f, 1f);
        
        switch (state)
        {
            case ClientState.Spawning:
            case ClientState.MovingToGoal:
                return Color.white;
            
            case ClientState.MovingToRegistrarImpolite:
                return swampGreen;

            case ClientState.AtWaitingArea:
            case ClientState.SittingInWaitingArea:
            case ClientState.MovingToSeat:
                return Color.grey;

            case ClientState.AtToilet: return Color.yellow;
            case ClientState.Confused: return Color.white;
            case ClientState.AtRegistration: return new Color(0.5f, 0.7f, 1f);
            case ClientState.ReturningToWait: return Color.yellow;
            case ClientState.AtDesk1: return Color.magenta;
            case ClientState.AtDesk2: return Color.cyan;
            case ClientState.AtLimitedZoneEntrance: return Color.grey;
            case ClientState.InsideLimitedZone: return Color.yellow;
            case ClientState.PassedRegistration: return Color.green;
            case ClientState.Enraged: return Color.red;
            case ClientState.Leaving: 
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Angry) return Color.red;
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.CalmedDown) return Color.magenta;
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Processed) return Color.green;
                return Color.white;
            default:
                return Color.white;
        }
    }
    
    public void SetQueueNumber(int number) => queueNumber = number;
}