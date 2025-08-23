using UnityEngine;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
public class ClerkController : MonoBehaviour
{
    // Состояния клерка стали публичными для доступа из других скриптов
    public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet }
    private ClerkState currentState = ClerkState.Working;

    [Header("Назначение клерка")]
    [Tooltip("Рабочая точка, где клерк находится большую часть времени")]
    public Transform workPoint;
    [Tooltip("Точка для отдыха (кухня)")]
    public Transform kitchenPoint;
    [Tooltip("Точка туалета для персонала")]
    public Transform staffToiletPoint;
    [Tooltip("Номер стола/регистратуры, за который отвечает клерк (0=Регистратура, 1=Стол1, 2=Стол2)")]
    public int assignedServicePoint;
    
    [Header("Параметры поведения")]
    public float moveSpeed = 1.8f;
    [Tooltip("Шанс (0-1) в секунду пойти в туалет в рабочее время")]
    public float chanceToGoToToilet = 0.005f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 targetPosition;
    private Coroutine currentActionCoroutine;
    private Waypoint[] allWaypoints;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        
        targetPosition = workPoint.position;
        // Изначально точка обслуживания недоступна, пока клерк не дойдет до нее
        ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false);
        
        // При старте даем команду идти на рабочее место
        GoToWorkStation();
    }
    
    void FixedUpdate()
    {
        // Логика движения активна только в "подвижных" состояниях
        if (currentState == ClerkState.GoingToBreak || currentState == ClerkState.ReturningToWork || currentState == ClerkState.GoingToToilet)
        {
            MoveTowardsTarget();
        }
        else // В стационарных состояниях просто стоим на месте
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Шанс пойти в туалет есть только в состоянии 'Working' и если клерк не занят другим действием
        if (currentState == ClerkState.Working && Random.value < chanceToGoToToilet * Time.deltaTime)
        {
            if (currentActionCoroutine == null)
            {
                currentActionCoroutine = StartCoroutine(ToiletBreakRoutine());
            }
        }
        
        UpdateSpriteDirection();
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ УПРАВЛЕНИЯ ИЗВНЕ ---

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

    // Публичный метод для доступа к состоянию из других скриптов (например, нотификаций)
    public ClerkState GetCurrentState() => currentState;

    // --- ВНУТРЕННИЕ КОРУТИНЫ ПОВЕДЕНИЯ ---
    
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

    // --- ЛОГИКА ДВИЖЕНИЯ И НАВИГАЦИИ ---

    private Waypoint currentTargetWaypoint;
    void SetPathTo(Vector2 finalDestination)
    {
        targetPosition = finalDestination;
        currentTargetWaypoint = FindNearestWaypoint(transform.position);
    }
    
    void MoveTowardsTarget()
    {
        if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
        {
            return;
        }

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
        return allWaypoints.OrderBy(wp => Vector2.Distance(position, wp.transform.position)).FirstOrDefault();
    }
}