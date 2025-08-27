using UnityEngine;
using TMPro;
using System.Linq;

public class ClientPathfinding : MonoBehaviour
{
    public enum LeaveReason { Normal, Processed, Angry, CalmedDown, Upset }
    public ClientStateMachine stateMachine;
    public ClientMovement movement;
    public ClientNotification notification;
    public DocumentHolder docHolder;

    public bool isLeavingSuccessfully = false;
    public LeaveReason reasonForLeaving = LeaveReason.Normal;
    
    [Header("Касса")]
    public int billToPay = 0;
    public GameObject moneyPrefab; 

    [Header("Звуки")] 
    public AudioClip spawnSound, exitSound, confusedSound, toiletSound;
    public AudioClip successfulExitSound, dissatisfiedExitSound, helpedByInternSound, paymentSound;

    [Header("Терпение")] public float totalPatienceTime;
    public static int totalClients, clientsExited, clientsInWaiting, clientsToToilet, clientsToRegistration, clientsConfused;
    public static int clientsExitedAngry = 0, clientsExitedProcessed = 0;

    public void Initialize(GameObject wZ, Waypoint eW) 
    { 
        totalClients++; 
        stateMachine = gameObject.GetComponent<ClientStateMachine>(); 
        movement = gameObject.GetComponent<ClientMovement>(); 
        notification = gameObject.GetComponent<ClientNotification>(); 
        docHolder = gameObject.GetComponent<DocumentHolder>();

        if (stateMachine == null || movement == null || notification == null || docHolder == null) 
        {
            Debug.LogError($"Критическая ошибка инициализации на клиенте {gameObject.name}!");
            enabled = false;
            return; 
        }
        
        stateMachine.Initialize(this); 
        movement.Initialize(this); 
        
        TextMeshPro tmp = GetComponentInChildren<TextMeshPro>(); 
        if (tmp != null) notification.Initialize(this, tmp); 
        
        totalPatienceTime = Random.Range(60f, 120f); 
        if (spawnSound != null) AudioSource.PlayClipAtPoint(spawnSound, transform.position);

        // --- НОВАЯ ЛОГИКА: Выдаём случайный документ при спавне ---
        float choice = Random.value;
        if (choice < 0.4f) // 40% шанс появиться без всего
        {
            docHolder.SetDocument(DocumentType.None);
        }
        else if (choice < 0.7f) // 30% шанс на Бланк 1
        {
            docHolder.SetDocument(DocumentType.Form1);
        }
        else // 30% шанс на Бланк 2
        {
            docHolder.SetDocument(DocumentType.Form2);
        }
    }

    public void OnClientExit() { if (reasonForLeaving == LeaveReason.Angry || reasonForLeaving == LeaveReason.CalmedDown || reasonForLeaving == LeaveReason.Upset) { clientsExitedAngry++; } else if(isLeavingSuccessfully) { clientsExitedProcessed++; } clientsExited++; totalClients--; if (exitSound != null) AudioSource.PlayClipAtPoint(exitSound, transform.position); Destroy(gameObject); }
    public void UnfreezeAndRestartAI() { if(stateMachine != null) stateMachine.StartCoroutine(stateMachine.MainLogicLoop()); }
    public void CalmDownAndLeave() { if (stateMachine != null && stateMachine.GetCurrentState() == ClientState.Enraged) { reasonForLeaving = LeaveReason.CalmedDown; stateMachine.SetState(ClientState.Leaving); } }
    
    public void CalmDownAndReturnToQueue() 
    {
        if (stateMachine != null && stateMachine.GetCurrentState() == ClientState.Enraged) 
        {
            stateMachine.SetGoal(ClientQueueManager.Instance.ChooseNewGoal(this)); 
            stateMachine.SetState(ClientState.ReturningToWait);
        }
    }

    public void Freeze() { if(stateMachine != null) stateMachine.StopAllActionCoroutines(); GetComponent<AgentMover>()?.Stop(); }
    
    public void ForceLeave(LeaveReason reason = LeaveReason.CalmedDown) 
    {
        if (stateMachine.GetCurrentState() == ClientState.Leaving || stateMachine.GetCurrentState() == ClientState.LeavingUpset) return;
        
        LimitedCapacityZone currentZone = stateMachine.GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>();
        if(currentZone != null) { currentZone.ReleaseWaypoint(stateMachine.GetCurrentGoal()); }

        stateMachine.StopAllActionCoroutines();
        ClientQueueManager.Instance.RemoveClientFromQueue(this);
        
        if(ClientQueueManager.Instance.dissatisfiedClients.Contains(this)) { ClientQueueManager.Instance.dissatisfiedClients.Remove(this); }
        reasonForLeaving = reason; 
        
        if (reason == LeaveReason.Angry && !ClientQueueManager.Instance.dissatisfiedClients.Contains(this)) { ClientQueueManager.Instance.dissatisfiedClients.Add(this); }

        if (reason == LeaveReason.Upset) { stateMachine.SetState(ClientState.LeavingUpset); }
        else { stateMachine.SetState(ClientState.Leaving); }
    }

    public static ClientPathfinding FindClosestConfusedClient(Vector3 position) { return FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Where(c => c != null && c.stateMachine != null && c.stateMachine.GetCurrentState() == ClientState.Confused).OrderBy(c => Vector3.Distance(c.transform.position, position)).FirstOrDefault(); }
}