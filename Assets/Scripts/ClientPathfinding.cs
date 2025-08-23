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
    
    public void OnClientExit()
    {
        if (reasonForLeaving == LeaveReason.Angry || reasonForLeaving == LeaveReason.CalmedDown) {
            clientsExitedAngry++;
        } else if(isLeavingSuccessfully) {
            clientsExitedProcessed++;
        }
        clientsExited++;
        totalClients--;
        if (exitSound != null) AudioSource.PlayClipAtPoint(exitSound, transform.position);
        Destroy(gameObject);
    }
    
    public void CalmDownAndLeave() { if (stateMachine.GetCurrentState() == ClientState.Enraged) { reasonForLeaving = LeaveReason.CalmedDown; stateMachine.SetState(ClientState.Leaving); stateMachine.StartCoroutine(stateMachine.MainLogicLoop()); } }
    
    public void CalmDownAndReturnToQueue() { if (stateMachine.GetCurrentState() == ClientState.Enraged) { stateMachine.SetGoal(queueManager.GetWaitingWaypoint()); stateMachine.SetState(ClientState.ReturningToWait); stateMachine.StartCoroutine(stateMachine.MainLogicLoop()); } }

    public void Freeze() { if(stateMachine != null) stateMachine.StopAllCoroutines(); if(movement != null) movement.SetVelocity(Vector2.zero); }
    
    public void ForceLeave()
    {
        // Проверяем, чтобы не вызывать логику ухода для тех, кто уже уходит
        if (stateMachine.GetCurrentState() == ClientState.Leaving) return;
        
        // Останавливаем все текущие действия
        stateMachine.StopAllCoroutines();
        
        // Убираем клиента из всех очередей и списков
        queueManager.RemoveClientFromQueue(this);
        if(ClientQueueManager.dissatisfiedClients.Contains(this))
        {
            ClientQueueManager.dissatisfiedClients.Remove(this);
        }
        
        // ИЗМЕНЕНО ЗДЕСЬ: Устанавливаем причину, которая соответствует символу ":("
        reasonForLeaving = LeaveReason.CalmedDown; 
        
        // Запускаем процесс ухода
        stateMachine.SetState(ClientState.Leaving);
        stateMachine.StartCoroutine(stateMachine.MainLogicLoop());
    }
}