using UnityEngine;
using TMPro;

public class ClientNotification : MonoBehaviour
{
    private ClientPathfinding parent;
    private TextMeshPro notificationText;
    public TextMeshPro queueNumberText;
    private int queueNumber = -1;
    private Color swampGreen = new Color(0.3f, 0.4f, 0.2f);

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
            bool isGoingToReg = (cs == ClientState.MovingToGoal && parent.stateMachine.GetCurrentGoal()?.gameObject == parent.queueManager.GetRegistrationZone());
            bool showQueue = (cs == ClientState.AtWaitingArea || cs == ClientState.SittingInWaitingArea || cs == ClientState.MovingToSeat || cs == ClientState.AtRegistration || isGoingToReg) && queueNumber >= 0;
            queueNumberText.text = showQueue ? queueNumber.ToString() : "";
        }
    }

    private string GetStateText()
    {
        ClientState state = parent.stateMachine.GetCurrentState();
        bool useEmoji = NotificationStyleManager.useEmojiStyle;

        if (state == ClientState.MovingToGoal)
        {
            Waypoint goal = parent.stateMachine.GetCurrentGoal();
            if (goal != null)
            {
                if (goal.gameObject == parent.queueManager.GetWaitingZone()) return useEmoji ? "🙄" : "W";
                if (goal.gameObject == parent.queueManager.GetRegistrationZone()) return useEmoji ? "🙂" : "R";
                if (goal == parent.queueManager.GetDesk1Waypoint()) return useEmoji ? "📄" : "1";
                if (goal == parent.queueManager.GetDesk2Waypoint()) return useEmoji ? "📜" : "2";
                if (goal.CompareTag("Cashier")) return useEmoji ? "💵" : "$";

                LimitedCapacityZone lcz = goal.GetComponentInParent<LimitedCapacityZone>();
                if (lcz != null && goal == lcz.waitingWaypoint) return useEmoji ? "😯" : "!";
            }
        }
        
        switch (state)
        {
            case ClientState.AtWaitingArea:
            case ClientState.SittingInWaitingArea:
            case ClientState.MovingToSeat:
            case ClientState.AtLimitedZoneEntrance:
                return useEmoji ? "😴" : "Zz";
            case ClientState.MovingToRegistrarImpolite: return useEmoji ? "😏" : "R";
            case ClientState.AtToilet:
            case ClientState.InsideLimitedZone:
                return useEmoji ? "😯" : "!";
            case ClientState.Confused: return useEmoji ? "🤔" : "?";
            case ClientState.AtRegistration: return useEmoji ? "🙂" : "R";
            case ClientState.PassedRegistration: return useEmoji ? "🤪" : "O";
            case ClientState.ReturningToWait: return useEmoji ? "😕" : "...";
            case ClientState.AtDesk1: return useEmoji ? "📄" : "1";
            case ClientState.AtDesk2: return useEmoji ? "📜" : "2";
            case ClientState.AtCashier:
            case ClientState.GoingToCashier:
                 return useEmoji ? "💵" : "$";
            case ClientState.Enraged: return useEmoji ? "😡" : "@";
            
            case ClientState.LeavingUpset: 
                return useEmoji ? "😞" : ":-(";

            case ClientState.Leaving: 
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Angry) return useEmoji ? "😡" : "@";
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Upset) return useEmoji ? "😞" : ":-(";
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.CalmedDown) return useEmoji ? "😠" : "☹";
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Processed) return useEmoji ? "🤪" : "O";
                return useEmoji ? "😐" : "o";
            default: return "";
        }
    }
    
    private Color GetStateColor()
    {
        ClientState state = parent.stateMachine.GetCurrentState();
        if (state == ClientState.MovingToRegistrarImpolite) return swampGreen;
        if (state == ClientState.MovingToGoal) { Waypoint goal = parent.stateMachine.GetCurrentGoal(); if (goal != null && goal.gameObject == parent.queueManager.GetRegistrationZone()) return new Color(0.5f, 0.7f, 1f); }
        switch (state)
        {
            case ClientState.AtWaitingArea: case ClientState.SittingInWaitingArea: case ClientState.MovingToSeat: case ClientState.AtLimitedZoneEntrance: return Color.grey;
            case ClientState.AtToilet: case ClientState.InsideLimitedZone: return Color.yellow;
            case ClientState.AtRegistration: return new Color(0.5f, 0.7f, 1f);
            case ClientState.ReturningToWait: return Color.yellow;
            case ClientState.AtDesk1: return Color.magenta;
            case ClientState.AtDesk2: return Color.cyan;
            case ClientState.AtCashier: case ClientState.GoingToCashier: return new Color(1f, 0.84f, 0f);
            case ClientState.PassedRegistration: return Color.green;
            case ClientState.Enraged: return Color.red;
            
            case ClientState.LeavingUpset:
                return new Color(1f, 0.5f, 0f); // Orange

            case ClientState.Leaving: 
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Angry) return Color.red; 
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Upset) return new Color(1f, 0.5f, 0f);
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.CalmedDown) return Color.magenta; 
                if(parent.reasonForLeaving == ClientPathfinding.LeaveReason.Processed) return Color.green; 
                return Color.white;
            default: return Color.white;
        }
    }
    
    public void SetQueueNumber(int number) => queueNumber = number;
}