// Файл: ClientPathfinding.cs
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;

public class ClientPathfinding : MonoBehaviour
{
    public enum LeaveReason { Normal, Processed, Angry, CalmedDown, Upset, Theft }
    public ClientStateMachine stateMachine;
    public ClientMovement movement;
    public ClientNotification notification;
    public DocumentHolder docHolder;

    [Header("Цели и Характер")]
    public ClientGoal mainGoal;
    [Range(0f, 1f)] public float babushkaFactor;
    [Range(0f, 1f)] public float suetunFactor;
    [Range(0f, 1f)] public float prolazaFactor;
    
    [Header("Внешний вид")]
    public Gender gender;
    private CharacterVisuals visuals;
	[Tooltip("Набор спрайтов (одежда) для клиентов")]
	public EmotionSpriteCollection spriteCollection;

    [Header("Настройки Терпения")]
    public float totalPatienceTime;
    public float minPatienceTime = 60f;
    public float maxPatienceTime = 120f;

    public bool isLeavingSuccessfully = false;
    public LeaveReason reasonForLeaving = LeaveReason.Normal;
    
    [Header("Касса")]
    public int billToPay = 0;
    public GameObject moneyPrefab;
    
    [Header("Создание беспорядка")]
    public List<GameObject> trashPrefabs;
    public List<GameObject> puddlePrefabs;

    [Header("Звуки")]
    public AudioClip spawnSound, exitSound, confusedSound, toiletSound;
    public AudioClip successfulExitSound, dissatisfiedExitSound, helpedByInternSound, paymentSound;
    public AudioClip stampSound;
    public AudioClip impoliteSound;
    public AudioClip theftAttemptSound;
    
    public static int totalClients, clientsExited, clientsInWaiting, clientsToToilet, clientsToRegistration, clientsConfused;
    public static int clientsExitedAngry = 0, clientsExitedProcessed = 0;

    [Header("Документы")]
    [Range(0f, 1f)] public float documentQuality;
    
    [Header("Данные документа Директора")]
    public int directorDocumentFee;
    public int directorDocumentBribe;
    public bool hasBeenSentForRevision = false;
	public DirectorDocumentLayout directorDocumentLayout;
    
    public CharacterVisuals GetVisuals() => visuals;
    
    public void Initialize(GameObject wZ, Waypoint eW)
{
    totalClients++;
    stateMachine = gameObject.GetComponent<ClientStateMachine>();
    movement = gameObject.GetComponent<ClientMovement>();
    notification = gameObject.GetComponent<ClientNotification>();
    docHolder = gameObject.GetComponent<DocumentHolder>();
    visuals = gameObject.GetComponent<CharacterVisuals>();
    if (stateMachine == null || movement == null || notification == null || docHolder == null || visuals == null)
    {
        Debug.LogError($"Критическая ошибка инициализации на клиенте {gameObject.name}!", gameObject);
        enabled = false;
        return;
    }

    gender = (Random.value > 0.5f) ? Gender.Male : Gender.Female;
    
    // --- ИЗМЕНЕНИЕ ЗДЕСЬ ---
    // У клиента нет карты эмоций для состояний, поэтому передаем null
    visuals.Setup(gender, this.spriteCollection, null);
    // ----------------------
    
    babushkaFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        suetunFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        prolazaFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        documentQuality = 1.0f - (suetunFactor * 0.5f);
        
        if (mainGoal == default(ClientGoal))
        {
            var goals = System.Enum.GetValues(typeof(ClientGoal));
            mainGoal = (ClientGoal)goals.GetValue(Random.Range(0, goals.Length));
        }

        DocumentType startingDoc = DocumentType.None;
        if (Random.value < 0.2f)
        {
            if (mainGoal == ClientGoal.GetCertificate1) { startingDoc = DocumentType.Form2; }
            else if (mainGoal == ClientGoal.GetCertificate2) { startingDoc = DocumentType.Form1; }
        }
        docHolder.SetDocument(startingDoc);
        
        if (mainGoal == ClientGoal.DirectorApproval)
        {
            directorDocumentFee = Random.Range(250, 751);
            if (Random.value < 0.33f)
            {
                directorDocumentBribe = Random.Range(50, 151);
            }
        }

        stateMachine.Initialize(this);
        movement.Initialize(this);
        float basePatience = Random.Range(minPatienceTime, maxPatienceTime);
        totalPatienceTime = basePatience * (1 + babushkaFactor);

        if (spawnSound != null) AudioSource.PlayClipAtPoint(spawnSound, transform.position);
    }

    void OnDestroy()
    {
        if (ClientQueueManager.Instance != null)
        {
            ClientQueueManager.Instance.RemoveClientFromQueue(this);
        }
        
        // --- ИЗМЕНЕНИЕ: Сообщаем столу директора, что мы ушли и нашу иконку нужно убрать ---
        StartOfDayPanel.Instance?.RemoveDocumentIcon(this);

        if (stateMachine != null)
        {
            Waypoint lastGoal = stateMachine.GetCurrentGoal();
            if (lastGoal != null)
            {
                var zone = lastGoal.GetComponentInParent<LimitedCapacityZone>();
                if (zone != null)
                {
                    zone.ReleaseWaypoint(lastGoal);
                }
            }
        }
    }

    public void OnClientExit()
    {
        if (reasonForLeaving == LeaveReason.Angry || reasonForLeaving == LeaveReason.CalmedDown || reasonForLeaving == LeaveReason.Upset || reasonForLeaving == LeaveReason.Theft)
        {
            clientsExitedAngry++;
        }
        else if(isLeavingSuccessfully)
        {
            clientsExitedProcessed++;
        }
        clientsExited++;
        totalClients--;
        if (exitSound != null) AudioSource.PlayClipAtPoint(exitSound, transform.position);
        Destroy(gameObject);
    }

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
        if (stateMachine.GetCurrentState() == ClientState.Leaving || stateMachine.GetCurrentState() == ClientState.LeavingUpset)
        {
            reasonForLeaving = reason;
            return;
        }

        LimitedCapacityZone currentZone = stateMachine.GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>();
        if(currentZone != null)
        {
            currentZone.LeaveQueue(gameObject);
            currentZone.ReleaseWaypoint(stateMachine.GetCurrentGoal());
        }
        ClientQueueManager.Instance.RemoveClientFromQueue(this);
        if(ClientQueueManager.Instance.dissatisfiedClients.Contains(this))
        {
            ClientQueueManager.Instance.dissatisfiedClients.Remove(this);
        }

        reasonForLeaving = reason;
        if (reason == LeaveReason.Angry && !ClientQueueManager.Instance.dissatisfiedClients.Contains(this))
        {
            ClientQueueManager.Instance.dissatisfiedClients.Add(this);
        }
        
        stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
        if (reason == LeaveReason.Upset)
        {
            stateMachine.SetState(ClientState.LeavingUpset);
        }
        else
        {
            stateMachine.SetState(ClientState.Leaving);
        }
    }

    public static ClientPathfinding FindClosestConfusedClient(Vector3 position) 
    { 
        return FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
            .Where(c => c != null && c.stateMachine != null && c.stateMachine.GetCurrentState() == ClientState.Confused)
            .OrderBy(c => Vector3.Distance(c.transform.position, position))
            .FirstOrDefault();
    }
}