// Файл: ClientNotification.cs
using UnityEngine;
using TMPro;
using System.Linq;
using Characters;

public class ClientNotification : MonoBehaviour
{
    private ClientPathfinding parent;

    [Tooltip("Опционально: текстовое поле для старых иконок-эмодзи.")]
    public TextMeshPro notificationText;
    [Tooltip("Обязательно: текстовое поле для номера в очереди.")]
    public TextMeshPro queueNumberText;

    private int queueNumber = -1;
    private Color swampGreen = new Color(0.3f, 0.4f, 0.2f);

    void Awake()
    {
        parent = GetComponent<ClientPathfinding>();

        if (queueNumberText != null && transform.parent != null)
        {
            var parentTransform = transform.parent;
            queueNumberText.transform.SetParent(parentTransform, true);
            queueNumberText.transform.SetAsLastSibling();
        }
    }

    public void UpdateNotification()
    {
        if (parent == null || parent.stateMachine == null) return;

        if (parent.GetVisuals() != null)
        {
            if (notificationText != null)
            {
                notificationText.enabled = false;
            }
        }

        if (queueNumberText != null)
        {
            ClientState cs = parent.stateMachine.GetCurrentState();
            ClientPathfinding.LeaveReason lr = parent.reasonForLeaving;

            bool isLeavingBadly = cs == ClientState.LeavingUpset || (cs == ClientState.Leaving && (lr == ClientPathfinding.LeaveReason.Angry || lr == ClientPathfinding.LeaveReason.Upset || lr == ClientPathfinding.LeaveReason.Theft));
            bool isConfusedOrEnraged = (cs == ClientState.Confused || cs == ClientState.Enraged);

            bool showQueue = queueNumber >= 0 && !isLeavingBadly && !isConfusedOrEnraged;
            queueNumberText.text = showQueue ? queueNumber.ToString() : "";
        }
    }

    public void SetQueueNumber(int number) => queueNumber = number;
}