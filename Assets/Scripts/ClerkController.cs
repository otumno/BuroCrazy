using UnityEngine;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
public class ClerkController : MonoBehaviour
{
    public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet }
    private ClerkState currentState = ClerkState.Working;

    [Header("Назначение клерка")]
    public Transform workPoint;
    public Transform kitchenPoint;
    public Transform staffToiletPoint;
    public int assignedServicePoint;
    
    [Header("Параметры поведения")]
    public float moveSpeed = 1.8f;
    public float chanceToGoToToilet = 0.005f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 targetPosition;
    private Coroutine currentActionCoroutine;
    private Waypoint[] allWaypoints;

    // --- ИЗМЕНЕНИЕ ЗДЕСЬ: Инициализация перенесена в Awake() ---
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
    }

    void Start()
    {
        targetPosition = workPoint.position;
        ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false);
        GoToWorkStation();
    }
    
    void FixedUpdate()
    {
        if (currentState == ClerkState.Working || currentState == ClerkState.OnBreak || currentState == ClerkState.AtToilet)
        {
            rb.linearVelocity = Vector2.zero;
            if (currentState == ClerkState.Working && Random.value < chanceToGoToToilet * Time.deltaTime)
            {
                if (currentActionCoroutine == null)
                {
                    currentActionCoroutine = StartCoroutine(ToiletBreakRoutine());
                }
            }
        }
        else
        {
            MoveTowardsTarget();
        }
        UpdateSpriteDirection();
    }

    public void GoOnBreak(float duration)
    {
        if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
        currentActionCoroutine = StartCoroutine(BreakRoutine(duration, kitchenPoint));
    }

    public void GoToNightPost()
    {
        if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
        currentActionCoroutine = StartCoroutine(NightPostRoutine());
    }
    
    public void GoToWorkStation()
    {
        if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
        currentActionCoroutine = StartCoroutine(ReturnToWorkRoutine());
    }

    public ClerkState GetCurrentState() => currentState;

    private IEnumerator NightPostRoutine()
    {
        currentState = ClerkState.GoingToBreak;
        ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false);
        SetPathTo(kitchenPoint.position);
        yield return new WaitUntil(() => Vector2.Distance(transform.position, targetPosition) < 0.2f);
        currentState = ClerkState.OnBreak;
    }

    private IEnumerator ReturnToWorkRoutine()
    {
        currentState = ClerkState.ReturningToWork;
        SetPathTo(workPoint.position);
        yield return new WaitUntil(() => Vector2.Distance(transform.position, targetPosition) < 0.2f);
        currentState = ClerkState.Working;
        ClientQueueManager.SetServicePointAvailability(assignedServicePoint, true);
        currentActionCoroutine = null;
    }
    
    private IEnumerator BreakRoutine(float duration, Transform breakPoint)
    {
        currentState = ClerkState.GoingToBreak;
        ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false);
        SetPathTo(breakPoint.position);
        yield return new WaitUntil(() => Vector2.Distance(transform.position, targetPosition) < 0.2f);
        currentState = ClerkState.OnBreak;
        yield return new WaitForSeconds(duration);
        yield return StartCoroutine(ReturnToWorkRoutine());
    }

    private IEnumerator ToiletBreakRoutine()
    {
        currentState = ClerkState.GoingToToilet;
        ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false);
        SetPathTo(staffToiletPoint.position);
        yield return new WaitUntil(() => Vector2.Distance(transform.position, targetPosition) < 0.2f);
        currentState = ClerkState.AtToilet;
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(ReturnToWorkRoutine());
    }

    private Waypoint currentTargetWaypoint;
    void SetPathTo(Vector2 finalDestination)
    {
        targetPosition = finalDestination;
        currentTargetWaypoint = FindNearestWaypoint(transform.position);
    }
    
    void MoveTowardsTarget()
    {
        if (Vector2.Distance(transform.position, targetPosition) < 0.1f) return;

        if (currentTargetWaypoint == null || Vector2.Distance(transform.position, currentTargetWaypoint.transform.position) < 0.2f)
        {
            Waypoint nearestNodeToUs = FindNearestWaypoint(transform.position);
            if (nearestNodeToUs != null && nearestNodeToUs.neighbors != null && nearestNodeToUs.neighbors.Count > 0)
            {
                currentTargetWaypoint = nearestNodeToUs.neighbors.OrderBy(n => Vector2.Distance(n.transform.position, targetPosition)).FirstOrDefault();
            }
        }

        if (currentTargetWaypoint != null)
        {
            Vector2 direction = ((Vector2)currentTargetWaypoint.transform.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = direction * moveSpeed;
        }
        else
        {
            rb.linearVelocity = (targetPosition - (Vector2)transform.position).normalized * moveSpeed;
        }
    }

    void UpdateSpriteDirection()
    {
        if (rb.linearVelocity.x > 0.1f) spriteRenderer.flipX = false;
        else if (rb.linearVelocity.x < -0.1f) spriteRenderer.flipX = true;
    }

    private Waypoint FindNearestWaypoint(Vector2 position)
    {
        if (allWaypoints == null) return null; // Добавим проверку на всякий случай
        return allWaypoints.OrderBy(wp => Vector2.Distance(position, wp.transform.position)).FirstOrDefault();
    }
}