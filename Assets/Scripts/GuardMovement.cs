using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
public class GuardMovement : MonoBehaviour
{
    public enum GuardState { Patrolling, WaitingAtWaypoint, Chasing, Talking, OnPost, GoingToToilet, AtToilet }
    private GuardState currentState = GuardState.Patrolling;

    [Header("Настройки патрулирования")]
    public List<Waypoint> patrolRoute;
    public float moveSpeed = 1.5f;
    public float stoppingDistance = 0.2f;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;
    
    [Header("Пост Охраны")]
    public Transform postWaypoint;

    [Header("Прочее поведение")]
    public Transform staffToiletPoint;
    public float chanceToGoToToilet = 0.01f;
    public float timeInToilet = 5f;

    [Header("Настройки преследования")]
    public float chaseSpeedMultiplier = 2f;
    public AudioClip chaseShoutClip;
    public float catchDistance = 1.0f;
    public float talkTime = 3f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private CircleCollider2D _collider;
    
    private Waypoint[] allWaypoints;
    private Waypoint currentChaseWaypoint;
    
    private Waypoint currentPatrolTarget;
    private ClientPathfinding currentChaseTarget;
    private Coroutine shoutCoroutine;
    private Coroutine actionCoroutine;
    
    private int guardLayer;
    private int clientLayer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<CircleCollider2D>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        guardLayer = LayerMask.NameToLayer("Guard");
        clientLayer = LayerMask.NameToLayer("Client");
        if (rb.bodyType != RigidbodyType2D.Kinematic) Debug.LogWarning("Для корректной работы охранника установите Body Type его Rigidbody2D в Kinematic!", gameObject);
        if (patrolRoute == null || patrolRoute.Count == 0) { Debug.LogError("Охраннику не назначен маршрут!", gameObject); enabled = false; return; }
        SelectNewRandomWaypoint();
    }

    void FixedUpdate()
    {
        UpdateState();
        switch (currentState)
        {
            case GuardState.Patrolling: HandlePatrolling(); break;
            case GuardState.WaitingAtWaypoint: rb.linearVelocity = Vector2.zero; break;
            case GuardState.Chasing: HandleChasing(); break;
            case GuardState.Talking: rb.linearVelocity = Vector2.zero; break;
            case GuardState.OnPost: HandleOnPost(); break;
            case GuardState.GoingToToilet: MoveTowards(staffToiletPoint.position, moveSpeed); break;
            case GuardState.AtToilet: rb.linearVelocity = Vector2.zero; break;
        }
        UpdateSpriteDirection();
    }

    public GuardState GetCurrentState() => currentState;
    public bool IsAvailable() => currentState != GuardState.Chasing && currentState != GuardState.Talking;

    public void AssignToChase(ClientPathfinding target)
    {
        if (!IsAvailable()) return;
        currentChaseTarget = target;
        Physics2D.IgnoreLayerCollision(guardLayer, clientLayer, true);
        currentState = GuardState.Chasing;
        StartShouting();
    }

    private void UpdateState()
    {
        if (!IsAvailable()) return;

        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
        bool isPostTime = (period == "ночь" || period == "обед");

        bool isIdle = (currentState == GuardState.WaitingAtWaypoint) || (currentState == GuardState.OnPost && Vector2.Distance(transform.position, postWaypoint.position) < stoppingDistance);
        if (isIdle && Random.value < chanceToGoToToilet * Time.deltaTime)
        {
            if (actionCoroutine == null)
            {
                actionCoroutine = StartCoroutine(ToiletBreakRoutine());
            }
        }
        
        if (currentState == GuardState.GoingToToilet || currentState == GuardState.AtToilet) return;

        if (isPostTime) { if (currentState != GuardState.OnPost) { currentState = GuardState.OnPost; } }
        else { if (currentState == GuardState.OnPost) { currentState = GuardState.Patrolling; } }
    }
    
    private IEnumerator ToiletBreakRoutine()
    {
        GuardState stateBeforeBreak = currentState;
        currentState = GuardState.GoingToToilet;
        yield return new WaitUntil(() => Vector2.Distance(transform.position, staffToiletPoint.position) < stoppingDistance);
        currentState = GuardState.AtToilet;
        yield return new WaitForSeconds(timeInToilet);
        currentState = stateBeforeBreak;
        actionCoroutine = null;
    }

    private IEnumerator TalkToClient()
    {
        currentState = GuardState.Talking;
        StopShouting();
        ClientPathfinding clientToCalm = currentChaseTarget;
        if (clientToCalm == null) { GoBackToPostOrPatrol(); yield break; }
        clientToCalm.Freeze();
        yield return new WaitForSeconds(talkTime);
        if(clientToCalm != null)
        {
            clientToCalm.UnfreezeAndRestartAI();
            if (Random.value < 0.5f) { clientToCalm.CalmDownAndReturnToQueue(); }
            else { clientToCalm.CalmDownAndLeave(); }
            ClientQueueManager.dissatisfiedClients.Remove(clientToCalm);
        }
        GoBackToPostOrPatrol();
    }
    
    private void GoBackToPostOrPatrol()
    {
        if (GuardManager.Instance != null && currentChaseTarget != null)
        {
            GuardManager.Instance.ReportTaskFinished(currentChaseTarget);
        }
        Physics2D.IgnoreLayerCollision(guardLayer, clientLayer, false);
        StopShouting();
        currentChaseTarget = null;
        currentChaseWaypoint = null;
        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
        if (period == "ночь" || period == "обед") { currentState = GuardState.OnPost; }
        else { StartCoroutine(WaitAtWaypoint()); }
    }
    
    public string GetStatusInfo() { switch (currentState) { case GuardState.Patrolling: return $"Патрулирует. Цель: {currentPatrolTarget?.name}"; case GuardState.OnPost: return $"На посту: {postWaypoint.name}"; case GuardState.Chasing: return $"Преследует: {currentChaseTarget?.name}"; case GuardState.Talking: return $"Разговаривает с: {currentChaseTarget?.name}"; case GuardState.WaitingAtWaypoint: return $"Ожидает на точке: {currentPatrolTarget?.name}"; default: return currentState.ToString(); } }
    private void HandleOnPost() { if (postWaypoint == null) return; if (Vector2.Distance(transform.position, postWaypoint.position) > stoppingDistance) { MoveTowards(postWaypoint.position, moveSpeed); } }
    private void HandlePatrolling() { if (currentPatrolTarget == null) SelectNewRandomWaypoint(); MoveTowards(currentPatrolTarget.transform.position, moveSpeed); if (Vector2.Distance(transform.position, currentPatrolTarget.transform.position) < stoppingDistance) StartCoroutine(WaitAtWaypoint()); }
    private void HandleChasing() { if (currentChaseTarget == null) { GoBackToPostOrPatrol(); return; } if (Vector2.Distance(transform.position, currentChaseTarget.transform.position) < catchDistance) { StartCoroutine(TalkToClient()); return; } if (currentChaseWaypoint == null || Vector2.Distance(transform.position, currentChaseWaypoint.transform.position) < stoppingDistance) { Waypoint nearestNodeToUs = FindNearestVisibleWaypoint(allWaypoints); if (nearestNodeToUs != null && nearestNodeToUs.neighbors != null && nearestNodeToUs.neighbors.Count > 0) { currentChaseWaypoint = nearestNodeToUs.neighbors.OrderBy(n => Vector2.Distance(n.transform.position, currentChaseTarget.transform.position)).FirstOrDefault(); } } if (currentChaseWaypoint != null) MoveTowards(currentChaseWaypoint.transform.position, moveSpeed * chaseSpeedMultiplier); else MoveTowards(currentChaseTarget.transform.position, moveSpeed * chaseSpeedMultiplier); }
    private void MoveTowards(Vector2 target, float speed) { Vector2 direction = (target - (Vector2)transform.position).normalized; Vector2 newPosition = rb.position + direction * speed * Time.fixedDeltaTime; rb.MovePosition(newPosition); }
    private void UpdateSpriteDirection() { Vector2 targetDirection = Vector2.zero; if (currentState == GuardState.Chasing && currentChaseTarget != null) { targetDirection = (Vector2)currentChaseTarget.transform.position - rb.position; } else if (currentState == GuardState.Patrolling && currentPatrolTarget != null) { targetDirection = (Vector2)currentPatrolTarget.transform.position - rb.position; } else if (currentState == GuardState.OnPost && postWaypoint != null) { targetDirection = (Vector2)postWaypoint.position - (Vector2)transform.position; } else if (currentState == GuardState.GoingToToilet && staffToiletPoint != null) { targetDirection = (Vector2)staffToiletPoint.position - rb.position; } if (targetDirection.x > 0.01f) { spriteRenderer.flipX = false; } else if (targetDirection.x < -0.01f) { spriteRenderer.flipX = true; } }
    private void SelectNewRandomWaypoint() { if (patrolRoute.Count <= 1 && currentPatrolTarget != null) return; Waypoint newWaypoint; do { newWaypoint = patrolRoute[Random.Range(0, patrolRoute.Count)]; } while (newWaypoint == currentPatrolTarget); currentPatrolTarget = newWaypoint; }
    private IEnumerator WaitAtWaypoint() { currentState = GuardState.WaitingAtWaypoint; yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime)); SelectNewRandomWaypoint(); if (currentState != GuardState.Chasing && currentState != GuardState.Talking) currentState = GuardState.Patrolling; }
    private void StartShouting() { if (shoutCoroutine == null && chaseShoutClip != null && audioSource != null) { shoutCoroutine = StartCoroutine(ShoutRoutine()); } }
    private void StopShouting() { if (shoutCoroutine != null) { StopCoroutine(shoutCoroutine); shoutCoroutine = null; } }
    private IEnumerator ShoutRoutine() { while (true) { audioSource.PlayOneShot(chaseShoutClip); yield return new WaitForSeconds(Random.Range(3f, 5f)); } }
    private Waypoint FindNearestVisibleWaypoint(Waypoint[] wps) { if(wps == null || wps.Length == 0) return null; Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in wps) { float distance = Vector2.Distance(transform.position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(transform.position, wp.transform.position, LayerMask.GetMask("Obstacles")); if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint; }
}