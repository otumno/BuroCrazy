// Файл: ClerkController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class ClerkController : StaffController
{
    public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet, Inactive, StressedOut, GoingToArchive, AtArchive }
    public enum ClerkRole { Regular, Cashier, Registrar, Archivist }

    [Header("Настройки клерка")]
    private ClerkState currentState = ClerkState.Inactive;
    public ClerkRole role = ClerkRole.Regular;
    [Tooltip("Рабочее место для ролей Registrar, Regular, Cashier. Для Archivist должно быть пустым.")]
    public ServicePoint assignedServicePoint;
    
    [Header("Настройки для ролей")]
    [Tooltip("Точка, куда нужно приносить документы в архив (используется ТОЛЬКО для роли 'Registrar')")]
    public Transform archiveDropOffPoint;
    [Tooltip("Рабочее место для архивариуса, где он ожидает появления документов. (Только для роли 'Archivist')")]
    public Transform archivistWaitingPoint;

    [Header("Внешний вид")]
    [Tooltip("Укажите пол для выбора правильных спрайтов")]
    public Gender gender;
    private CharacterVisuals visuals;

    [Header("Поведение")]
    public float timeInToilet = 10f;
    public float chanceToGoToToilet = 0.005f;
    public float callClientCooldownDuration = 1f;
    public float clientArrivalTimeout = 16f;
    
    [Header("Стресс")]
    public float maxStress = 100f;
    public float stressGainRate = 0.5f;
    public float stressGainPerClient = 5f;
    public float stressReliefRate = 10f;
    public float stressedOutDuration = 20f;
    private float currentStress = 0f;

    private bool isWaitingForClient = false;
    private bool isCarryingDocumentToArchive = false;
    private Waypoint[] allWaypoints;
    private float callClientCooldown = 0f;
    
    private StackHolder stackHolder;
    public static ClerkController RegistrarInstance { get; private set; }
    private Coroutine waitingForClientCoroutine;
    
    protected override void Awake()
    {
        base.Awake(); 
        visuals = GetComponent<CharacterVisuals>();
        stackHolder = GetComponent<StackHolder>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        if (role == ClerkRole.Cashier && assignedServicePoint != null) assignedServicePoint.deskId = -1;
        
        if (role == ClerkRole.Registrar)
        {
            RegistrarInstance = this;
        }
    }

    protected override void Start()
    {
        base.Start();
        visuals?.Setup(gender);
        currentState = ClerkState.Inactive;
        LogCurrentState();
    }

    void Update()
    {
        if (Time.timeScale == 0f) { agentMover?.Stop(); return; }

        UpdateStress();
        
        if (role == ClerkRole.Archivist && isOnDuty && currentAction == null)
        {
            if (!isCarryingDocumentToArchive && ArchiveManager.Instance != null && ArchiveManager.Instance.GetStackToProcess().CurrentSize > 0)
            {
                currentAction = StartCoroutine(PickupDocumentFromArchiveStack());
            }
        }
        
        if (role == ClerkRole.Registrar && isOnDuty && currentState == ClerkState.Working && currentAction == null && !isWaitingForClient && waitingForClientCoroutine == null)
        {
            if (Time.time > callClientCooldown)
            {
                if (assignedServicePoint != null && Vector2.Distance(transform.position, assignedServicePoint.clerkStandPoint.position) < 0.5f)
                {
                    bool clientCalled = ClientQueueManager.Instance.CallNextClient(this);
                    if (clientCalled)
                    {
                        waitingForClientCoroutine = StartCoroutine(WaitForClientRoutine());
                    }
                    else
                    {
                        callClientCooldown = Time.time + callClientCooldownDuration;
                    }
                }
            }
        }

        if (role == ClerkRole.Regular && currentState == ClerkState.Working && Random.value < chanceToGoToToilet * Time.deltaTime)
        {
            if (currentAction == null) { currentAction = StartCoroutine(ToiletBreakRoutine()); }
        }
    }
    
    public override void StartShift()
    {
        if (isOnDuty) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(StartShiftRoutine());
    }

    public override void EndShift()
    {
        if (!isOnDuty) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(EndShiftRoutine());
    }

    private void SetState(ClerkState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        LogCurrentState();

        // --- ИЗМЕНЕНО: Весь switch-блок заменен одной строкой! ---
        visuals?.SetEmotionForState(newState);
    }

    private IEnumerator StartShiftRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0f, 5f));
        isOnDuty = true;
        if (startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position);
        
        if (role == ClerkRole.Archivist)
        {
            Debug.Log($"Архивариус {name} начинает смену.");
            if (archivistWaitingPoint != null)
            {
                yield return StartCoroutine(MoveToTarget(archivistWaitingPoint.position, ClerkState.Working));
            }
            else
            {
                SetState(ClerkState.Working);
            }
            currentAction = null; 
        }
        else 
        {
            yield return StartCoroutine(ReturnToWorkRoutine());
        }
    }
    
    private IEnumerator EndShiftRoutine()
    {
        isOnDuty = false;
        if(assignedServicePoint != null) ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null);
        if (homePoint != null)
        {
            yield return StartCoroutine(MoveToTarget(homePoint.position, ClerkState.Inactive));
            if (endShiftSound != null) AudioSource.PlayClipAtPoint(endShiftSound, homePoint.position);
        }
        currentAction = null;
    }
    
    private IEnumerator ReturnToWorkRoutine()
    {
        if (assignedServicePoint == null)
        {
            Debug.LogError($"У клерка {name} (роль: {role}) не назначен ServicePoint, ему некуда возвращаться! Смена прервана.", gameObject);
            isOnDuty = false; 
            SetState(ClerkState.Inactive);
            yield break; 
        }
        SetState(ClerkState.ReturningToWork);
        yield return StartCoroutine(MoveToTarget(assignedServicePoint.clerkStandPoint.position, ClerkState.Working));
        ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, this);
        currentAction = null;
    }

    private IEnumerator WaitForClientRoutine()
    {
        isWaitingForClient = true;
        yield return new WaitForSeconds(clientArrivalTimeout);
        Debug.LogWarning($"[{name}] Клиент не прибыл за {clientArrivalTimeout} сек. Возобновляю вызовы.");
        isWaitingForClient = false;
        waitingForClientCoroutine = null;
    }

    private void UpdateStress()
    {
        if (currentState == ClerkState.StressedOut) return;
        bool isResting = currentState == ClerkState.OnBreak || currentState == ClerkState.AtToilet || currentState == ClerkState.Inactive;
        if (isOnDuty && !isResting)
        {
            currentStress += stressGainRate * Time.deltaTime;
        }
        else
        {
            currentStress -= stressReliefRate * Time.deltaTime;
        }
        currentStress = Mathf.Clamp(currentStress, 0, maxStress);
        if (currentStress >= maxStress)
        {
            if (currentAction != null) StopCoroutine(currentAction);
            currentAction = StartCoroutine(StressedOutRoutine());
        }
    }

    public void ServiceComplete()
    {
        if (waitingForClientCoroutine != null)
        {
            StopCoroutine(waitingForClientCoroutine);
            waitingForClientCoroutine = null;
        }
        isWaitingForClient = false;
        currentStress += stressGainPerClient;

        if (role == ClerkRole.Regular && assignedServicePoint != null && assignedServicePoint.documentStack != null)
        {
            DocumentStack myStack = assignedServicePoint.documentStack;
            myStack.AddDocumentToStack();

            if (myStack.IsFull && currentAction == null)
            {
                currentAction = StartCoroutine(TakeStackToArchiveRoutine(myStack));
            }
        }
    }

    private IEnumerator StressedOutRoutine()
    {
        SetState(ClerkState.StressedOut);
        if(assignedServicePoint != null) ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null);
        isWaitingForClient = false;
        yield return StartCoroutine(MoveToTarget(kitchenPoint.position, ClerkState.StressedOut));
        yield return new WaitForSeconds(stressedOutDuration);
        currentStress = maxStress * 0.7f;
        
        if (isOnDuty && role != ClerkRole.Archivist)
        {
             yield return StartCoroutine(ReturnToWorkRoutine());
        }
        else
        {
            currentAction = null;
        }
    }

    public float GetStressPercent() => currentStress / maxStress;

    public void GoOnBreak(float duration)
    {
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(BreakRoutine(duration));
    }

    public ClerkState GetCurrentState() => currentState;
    public bool IsOnBreak() => currentState != ClerkState.Working && currentState != ClerkState.ReturningToWork && currentState != ClerkState.GoingToArchive;
    public string GetStatusInfo()
    {
        switch (currentState)
        {
            case ClerkState.Working: 
                if (role == ClerkRole.Archivist) return "Работает в архиве";
                return $"Работает: {assignedServicePoint.name}";
            case ClerkState.OnBreak: return "На перерыве (кухня)";
            case ClerkState.AtToilet: return "На перерыве (туалет)";
            case ClerkState.ReturningToWork: return $"Возвращается на работу: {assignedServicePoint.name}";
            case ClerkState.GoingToBreak: return $"Идет на перерыв: {kitchenPoint.name}";
            case ClerkState.GoingToToilet: return $"Идет в туалет: {staffToiletPoint.name}";
            case ClerkState.StressedOut: return "СОРВАЛСЯ!";
            case ClerkState.GoingToArchive: return "Несет документы в архив";
            case ClerkState.AtArchive: return "Сдает документы в архив";
            case ClerkState.Inactive: return "Вне смены";
            default: return currentState.ToString();
        }
    }

    private void LogCurrentState()
    {
        logger?.LogState(GetStatusInfo());
    }
    
    private IEnumerator BreakRoutine(float duration)
    {
        SetState(ClerkState.GoingToBreak);
        if(assignedServicePoint != null) ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null);
        isWaitingForClient = false;
        yield return StartCoroutine(MoveToTarget(kitchenPoint.position, ClerkState.OnBreak));
        yield return new WaitForSeconds(duration);
        yield return StartCoroutine(ReturnToWorkRoutine());
    }

    private IEnumerator ToiletBreakRoutine()
    {
        SetState(ClerkState.GoingToToilet);
        if(assignedServicePoint != null) ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null);
        isWaitingForClient = false;
        yield return StartCoroutine(MoveToTarget(staffToiletPoint.position, ClerkState.AtToilet));
        yield return new WaitForSeconds(timeInToilet);
        yield return StartCoroutine(ReturnToWorkRoutine());
    }

    private IEnumerator TakeStackToArchiveRoutine(DocumentStack stack)
    {
        SetState(ClerkState.GoingToArchive);
        isWaitingForClient = false;
        ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null);

        Transform dropOffPoint = ArchiveManager.Instance.RequestDropOffPoint();

        if (dropOffPoint != null)
        {
            yield return StartCoroutine(MoveToTarget(dropOffPoint.position, ClerkState.AtArchive));
            
            int takenDocs = stack.TakeEntireStack();
            for (int i = 0; i < takenDocs; i++)
            {
                ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack();
            }
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogWarning($"{name} не может отнести документы, архив переполнен!");
            yield return new WaitForSeconds(5f); 
            currentAction = StartCoroutine(TakeStackToArchiveRoutine(stack));
            yield break;
        }

        yield return StartCoroutine(ReturnToWorkRoutine());
    }

    private IEnumerator PickupDocumentFromArchiveStack()
    {
        DocumentStack stack = ArchiveManager.Instance.GetStackToProcess();
        yield return StartCoroutine(MoveToTarget(stack.transform.position, ClerkState.Working));

        if (stack.TakeOneDocument()) 
        {
            isCarryingDocumentToArchive = true;
            stackHolder.ShowStack(1, 1);
            currentAction = StartCoroutine(DeliverDocumentToCabinet());
        }
        else
        {
            currentAction = null;
        }
    }

    private IEnumerator DeliverDocumentToCabinet()
    {
        ArchiveCabinet cabinet = ArchiveManager.Instance.GetRandomCabinet();
        if (cabinet != null)
        {
            yield return StartCoroutine(MoveToTarget(cabinet.transform.position, ClerkState.Working));
            
            stackHolder.HideStack();
            isCarryingDocumentToArchive = false;
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogError("Архивариус не нашел ни одного шкафа!");
            isCarryingDocumentToArchive = false; 
        }
        currentAction = null;
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, ClerkState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    private Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        var path = new Queue<Waypoint>();
        Waypoint startNode = FindNearestVisibleWaypoint();
        Waypoint endNode = FindNearestVisibleWaypoint(targetPos);
        if (startNode == null || endNode == null) return path;
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>();
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        var queue = new PriorityQueue<Waypoint>();
        foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; }
        distances[startNode] = 0;
        queue.Enqueue(startNode, 0);
        while (queue.Count > 0)
        {
            Waypoint current = queue.Dequeue();
            if (current == endNode) { ReconstructPath(previous, endNode, path); return path; }

            foreach (var neighbor in current.neighbors)
            {
                if (neighbor == null) continue;
                if (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(gameObject.tag)) continue;

                float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                if (distances.ContainsKey(neighbor) && newDist < distances[neighbor])
                {
                    distances[neighbor] = newDist;
                    previous[neighbor] = current;
                    queue.Enqueue(neighbor, newDist);
                }
            }
        }
        return path;
    }

    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path)
    {
        List<Waypoint> pathList = new List<Waypoint>();
        for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); }
        pathList.Reverse();
        path.Clear();
        foreach (var wp in pathList) { path.Enqueue(wp); }
    }

    private Waypoint FindNearestVisibleWaypoint(Vector2? position = null)
    {
        Vector2 pos = position ?? (Vector2)transform.position;
        if (allWaypoints == null) return null;
        Waypoint bestWaypoint = null;
        float minDistance = float.MaxValue;
        foreach (var wp in allWaypoints)
        {
            if (wp == null) continue;
            float distance = Vector2.Distance(pos, wp.transform.position);
            if (distance < minDistance)
            {
                RaycastHit2D hit = Physics2D.Linecast(pos, wp.transform.position, LayerMask.GetMask("Obstacles"));
                if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; }
            }
        }
        return bestWaypoint;
    }

    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>();
        public int Count => elements.Count;
        public void Enqueue(T item, float priority) { elements.Add(new KeyValuePair<T, float>(item, priority)); }
        public T Dequeue()
        {
            int bestIndex = 0;
            for (int i = 0; i < elements.Count; i++) { if (elements[i].Value < elements[bestIndex].Value) { bestIndex = i; } }
            T bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }
}