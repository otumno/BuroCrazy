using UnityEngine;
using TMPro;

public class ClientPathfinding : MonoBehaviour
{
    public enum LeaveReason { Normal, Processed, Angry, CalmedDown }
    public ClientStateMachine stateMachine;
    public ClientMovement movement;
    public ClientNotification notification;
    public ClientQueueManager queueManager;
    public bool isLeavingSuccessfully = false;
    public LeaveReason reasonForLeaving = LeaveReason.Normal;
    [Header("Звуки")] public AudioClip spawnSound, exitSound, confusedSound, toiletSound, successfulExitSound, dissatisfiedExitSound;
    [Header("Терпение")] public float totalPatienceTime;
    public static int totalClients, clientsExited, clientsInWaiting, clientsToToilet, clientsToRegistration, clientsConfused;
    public static int clientsExitedAngry = 0, clientsExitedProcessed = 0;

    public void Initialize(GameObject rZ, GameObject wZ, GameObject tZ, GameObject d1, GameObject d2, Waypoint eW) { totalClients++; stateMachine = gameObject.GetComponent<ClientStateMachine>(); movement = gameObject.GetComponent<ClientMovement>(); notification = gameObject.GetComponent<ClientNotification>(); queueManager = gameObject.GetComponent<ClientQueueManager>(); if (stateMachine == null || movement == null || notification == null || queueManager == null) { Debug.LogError("Ошибка инициализации компонентов клиента " + gameObject.name); return; } stateMachine.Initialize(this); movement.Initialize(this); TextMeshPro tmp = GetComponentInChildren<TextMeshPro>(); if (tmp != null) notification.Initialize(this, tmp); queueManager.Initialize(this, rZ, wZ, tZ, d1, d2, eW); totalPatienceTime = Random.Range(60f, 120f); if (spawnSound != null) AudioSource.PlayClipAtPoint(spawnSound, transform.position); }
    
    public void OnClientExit() { if (reasonForLeaving == LeaveReason.Angry || reasonForLeaving == LeaveReason.CalmedDown) { clientsExitedAngry++; } else if(isLeavingSuccessfully) { clientsExitedProcessed++; } clientsExited++; totalClients--; if (exitSound != null) AudioSource.PlayClipAtPoint(exitSound, transform.position); Destroy(gameObject); }
    
    // --- МЕТОДЫ УСПОКОЕНИЯ ИЗМЕНЕНЫ ---
    public void UnfreezeAndRestartAI()
    {
        if(stateMachine != null) stateMachine.StartCoroutine(stateMachine.MainLogicLoop());
    }

    public void CalmDownAndLeave()
    {
        if (stateMachine != null && stateMachine.GetCurrentState() == ClientState.Enraged)
        {
            reasonForLeaving = LeaveReason.CalmedDown;
            stateMachine.SetState(ClientState.Leaving);
        }
    }
    
    public void CalmDownAndReturnToQueue()
    {
        if (stateMachine != null && stateMachine.GetCurrentState() == ClientState.Enraged)
        {
            stateMachine.SetGoal(queueManager.GetWaitingWaypoint());
            stateMachine.SetState(ClientState.ReturningToWait);
        }
    }

    public void Freeze() { if(stateMachine != null) stateMachine.StopAllCoroutines(); if(movement != null) movement.SetVelocity(Vector2.zero); }
    
    public void ForceLeave() { if (stateMachine.GetCurrentState() == ClientState.Leaving) return; stateMachine.StopAllCoroutines(); queueManager.RemoveClientFromQueue(this); if(ClientQueueManager.dissatisfiedClients.Contains(this)) { ClientQueueManager.dissatisfiedClients.Remove(this); } reasonForLeaving = LeaveReason.CalmedDown; stateMachine.SetState(ClientState.Leaving); stateMachine.StartCoroutine(stateMachine.MainLogicLoop()); }
}