// Файл: ClientNotification.cs
using UnityEngine;
using TMPro;
using System.Linq;

public class ClientNotification : MonoBehaviour
{
    private ClientPathfinding parent;
    
    [Tooltip("Опционально: текстовое поле для старых иконок-эмодзи.")]
    public TextMeshPro notificationText;
    [Tooltip("Обязательно: текстовое поле для номера в очереди.")]
    public TextMeshPro queueNumberText;

    private int queueNumber = -1;
    private Color swampGreen = new Color(0.3f, 0.4f, 0.2f);

    // --- ИЗМЕНЕНИЕ: Используем Awake для надежного получения ссылки на родителя ---
    void Awake()
    {
        parent = GetComponent<ClientPathfinding>();
    }
    
    public void UpdateNotification()
    {
        if (parent == null || parent.stateMachine == null) return;
        
        // Если используется новая система эмоций (спрайты лиц), отключаем старые иконки
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
        
            // --- ИЗМЕНЕНИЕ: Улучшаем логику, чтобы номер скрывался во всех негативных состояниях ---
            bool isLeavingBadly = cs == ClientState.LeavingUpset || (cs == ClientState.Leaving && (lr == ClientPathfinding.LeaveReason.Angry || lr == ClientPathfinding.LeaveReason.Upset || lr == ClientPathfinding.LeaveReason.Theft));
            bool isConfusedOrEnraged = (cs == ClientState.Confused || cs == ClientState.Enraged);
            
            bool showQueue = queueNumber >= 0 && !isLeavingBadly && !isConfusedOrEnraged;
            queueNumberText.text = showQueue ? queueNumber.ToString() : "";
        }
    }

    private string GetStateText()
    {
        // ... (эта часть кода остается без изменений, но больше не используется, если у вас есть CharacterVisuals) ...
        return "";
    }
    
    private Color GetStateColor()
    {
        // ... (эта часть кода остается без изменений) ...
        return Color.white;
    }
    
    public void SetQueueNumber(int number) => queueNumber = number;
}