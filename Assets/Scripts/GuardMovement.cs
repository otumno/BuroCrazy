using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
public class GuardMovement : MonoBehaviour
{
    public enum GuardState { Patrolling, WaitingAtWaypoint, Chasing, Talking, OnPost }
    private GuardState currentState = GuardState.Patrolling;

    [Header("Настройки патрулирования")]
    public List<Waypoint> patrolRoute;
    public float moveSpeed = 1.5f;
    public float stoppingDistance = 0.2f;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;
    
    [Header("Пост Охраны")]
    [Tooltip("Точка, на которой охранник стоит ночью и в обед")]
    public Transform postWaypoint;

    [Header("Настройки преследования")]
    public float chaseSpeedMultiplier = 2f;
    public AudioClip chaseShoutClip;
    [Tooltip("Расстояние, на котором охранник начинает 'беседу'")]
    public float catchDistance = 1.0f;
    [Tooltip("Время 'беседы' с нарушителем")]
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

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<CircleCollider2D>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);

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
        }
        UpdateSpriteDirection();
    }

    public GuardState GetCurrentState() => currentState;

    private void UpdateState()
    {
        if (ClientQueueManager.dissatisfiedClients.Count > 0 && currentState != GuardState.Chasing && currentState != GuardState.Talking)
        {
            currentChaseTarget = ClientQueueManager.dissatisfiedClients.FirstOrDefault();
            if(currentChaseTarget != null)
            {
                currentState = GuardState.Chasing;
                StartShouting();
                return;
            }
        }
        
        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
        bool isPostTime = (period == "ночь" || period == "обед");

        if (isPostTime && currentState != GuardState.OnPost && currentState != GuardState.Chasing && currentState != GuardState.Talking)
        {
            currentState = GuardState.OnPost;
        }
        else if (!isPostTime && currentState == GuardState.OnPost)
        {
            currentState = GuardState.Patrolling;
        }
    }

    private void HandleOnPost()
    {
        if (postWaypoint == null) return;
        
        float distanceToPost = Vector2.Distance(transform.position, postWaypoint.position);
        if (distanceToPost > stoppingDistance)
        {
            MoveTowards(postWaypoint.position, moveSpeed);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
    
    private void HandlePatrolling() { if (currentPatrolTarget == null) SelectNewRandomWaypoint(); MoveTowards(currentPatrolTarget.transform.position, moveSpeed); if (Vector2.Distance(transform.position, currentPatrolTarget.transform.position) < stoppingDistance) StartCoroutine(WaitAtWaypoint()); }
    
    private void HandleChasing()
    {
        if (currentChaseTarget == null) { GoBackToPostOrPatrol(); return; }
        if (Vector2.Distance(transform.position, currentChaseTarget.transform.position) < catchDistance) { StartCoroutine(TalkToClient()); return; }
        
        if (currentChaseWaypoint == null || Vector2.Distance(transform.position, currentChaseWaypoint.transform.position) < stoppingDistance)
        {
            Waypoint nearestNodeToUs = FindNearestWaypoint(transform.position);
            if (nearestNodeToUs != null && nearestNodeToUs.neighbors != null && nearestNodeToUs.neighbors.Count > 0)
            {
                currentChaseWaypoint = nearestNodeToUs.neighbors.OrderBy(n => Vector2.Distance(n.transform.position, currentChaseTarget.transform.position)).FirstOrDefault();
            }
        }
        
        if (currentChaseWaypoint != null) MoveTowards(currentChaseWaypoint.transform.position, moveSpeed * chaseSpeedMultiplier);
        else MoveTowards(currentChaseTarget.transform.position, moveSpeed * chaseSpeedMultiplier);
    }
    
    private IEnumerator TalkToClient()
    {
        currentState = GuardState.Talking;
        StopShouting();
        
        ClientPathfinding clientToCalm = currentChaseTarget;
        if (clientToCalm == null) { GoBackToPostOrPatrol(); yield break; }
        
        CircleCollider2D clientCollider = clientToCalm.GetComponent<CircleCollider2D>();

        try
        {
            if (_collider != null) _collider.enabled = false;
            if (clientCollider != null) clientCollider.enabled = false;
            clientToCalm.Freeze();
            yield return new WaitForSeconds(talkTime);
        }
        finally
        {
            if (_collider != null) _collider.enabled = true;
            if (clientCollider != null) clientCollider.enabled = true;
            if(clientToCalm != null)
            {
                if (Random.value < 0.5f) clientToCalm.CalmDownAndReturnToQueue();
                else clientToCalm.CalmDownAndLeave();
                ClientQueueManager.dissatisfiedClients.Remove(clientToCalm);
            }
            GoBackToPostOrPatrol();
        }
    }
    
    private void GoBackToPostOrPatrol()
    {
        StopShouting();
        currentChaseTarget = null;
        currentChaseWaypoint = null;
        
        string period = ClientSpawner.CurrentPeriodName?.ToLower().Trim();
        if (period == "ночь" || period == "обед")
        {
            currentState = GuardState.OnPost;
        }
        else
        {
            StartCoroutine(WaitAtWaypoint());
        }
    }

    private void MoveTowards(Vector2 target, float speed) { Vector2 direction = (target - (Vector2)transform.position).normalized; rb.linearVelocity = direction * speed; }
    private void UpdateSpriteDirection() { Vector2 targetDirection = Vector2.zero; if (currentState == GuardState.Chasing && currentChaseTarget != null) { targetDirection = (Vector2)currentChaseTarget.transform.position - rb.position; } else if (currentState == GuardState.Patrolling && currentPatrolTarget != null) { targetDirection = (Vector2)currentPatrolTarget.transform.position - rb.position; } else if (currentState == GuardState.OnPost && postWaypoint != null) { targetDirection = (Vector2)postWaypoint.position - (Vector2)transform.position; } if (targetDirection.x > 0.01f) { spriteRenderer.flipX = false; } else if (targetDirection.x < -0.01f) { spriteRenderer.flipX = true; } }
    private void SelectNewRandomWaypoint() { if (patrolRoute.Count <= 1 && currentPatrolTarget != null) return; Waypoint newWaypoint; do { newWaypoint = patrolRoute[Random.Range(0, patrolRoute.Count)]; } while (newWaypoint == currentPatrolTarget); currentPatrolTarget = newWaypoint; }
    private IEnumerator WaitAtWaypoint() { currentState = GuardState.WaitingAtWaypoint; yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime)); SelectNewRandomWaypoint(); if (currentState != GuardState.Chasing && currentState != GuardState.Talking) currentState = GuardState.Patrolling; }
    private void StartShouting() { if (shoutCoroutine == null && chaseShoutClip != null && audioSource != null) { shoutCoroutine = StartCoroutine(ShoutRoutine()); } }
    private void StopShouting() { if (shoutCoroutine != null) { StopCoroutine(shoutCoroutine); shoutCoroutine = null; } }
    private IEnumerator ShoutRoutine() { while (true) { audioSource.PlayOneShot(chaseShoutClip); yield return new WaitForSeconds(Random.Range(3f, 5f)); } }
    private Waypoint FindNearestWaypoint(Vector2 position) { return allWaypoints.OrderBy(wp => Vector2.Distance(position, wp.transform.position)).FirstOrDefault(); }
}