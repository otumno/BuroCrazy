// Файл: AgentMover.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class AgentMover : MonoBehaviour
{
    [Header("Параметры Движения")]
    public float moveSpeed = 2f;
    public float stoppingDistance = 0.2f;

    [Header("Система 'Резиночки'")]
    [Tooltip("Насколько сильно персонаж стремится вернуться на свой путь. Выше значение - жестче 'резинка'.")]
    public float rubberBandStrength = 5f;
    [Tooltip("Приоритет персонажа. Охранник > Клерк > Клиент. Решает, кто кого продавливает.")]
    public int priority = 1;
    private Rigidbody2D rb;
    private Queue<Waypoint> path;
    private Vector2 pathAnchor; 
    private bool isYielding = false;
    private Coroutine yieldingCoroutine;
    private float dirtTimer = 0f;
    private float dirtInterval = 0.25f;

    private bool isDirectChasing = false;
    private Vector2 directChaseTarget;
    
    // --- НОВОЕ: Параметры для плавного торможения ---
    [Header("Настройки преследования")]
    [Tooltip("С какого расстояния персонаж начнет замедляться при прямой погоне.")]
    public float slowingDistance = 2.0f;
    [Tooltip("Насколько плавно персонаж меняет скорость. Меньше значение - более плавное движение.")]
    public float movementSmoothing = 5f;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pathAnchor = transform.position;
    }

    void FixedUpdate()
    {
        Vector2 desiredVelocity;

        if (isDirectChasing)
        {
            Vector2 vectorToTarget = directChaseTarget - (Vector2)transform.position;
            float distanceToTarget = vectorToTarget.magnitude;
            
            // --- УЛУЧШЕНИЕ: Добавлено плавное торможение при приближении к цели ---
            float targetSpeed = moveSpeed;
            if (distanceToTarget < slowingDistance)
            {
                // Чем ближе к цели, тем ниже скорость.
                targetSpeed = moveSpeed * (distanceToTarget / slowingDistance);
            }

            if (distanceToTarget > 0.01f) // Проверка на очень малое расстояние
            {
                Vector2 direction = vectorToTarget.normalized;
                desiredVelocity = direction * targetSpeed;
            }
            else
            {
                desiredVelocity = Vector2.zero;
            }
            
            // --- ИСПРАВЛЕНО: Уменьшен коэффициент для более плавного движения ---
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.fixedDeltaTime * movementSmoothing);
            UpdateSpriteDirection(rb.linearVelocity);
            HandleDirtLogic();
            return;
        }

        if (path == null || path.Count == 0)
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * movementSmoothing);
            return;
        }

        Waypoint targetWaypoint = path.Peek();

        pathAnchor = Vector2.MoveTowards(pathAnchor, targetWaypoint.transform.position, moveSpeed * Time.fixedDeltaTime);
        float currentStrength = isYielding ? rubberBandStrength / 4f : rubberBandStrength;
        desiredVelocity = (pathAnchor - (Vector2)transform.position) * currentStrength;
        desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, moveSpeed * 2f);

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.fixedDeltaTime * movementSmoothing);
        
        UpdateSpriteDirection(rb.linearVelocity);
        if (Vector2.Distance(transform.position, targetWaypoint.transform.position) < stoppingDistance)
        {
            pathAnchor = targetWaypoint.transform.position;
            path.Dequeue();
        }

        HandleDirtLogic();
    }

    public void StartDirectChase(Vector2 targetPosition)
    {
        isDirectChasing = true;
        directChaseTarget = targetPosition;
        path?.Clear();
    }

    public void UpdateDirectChase(Vector2 targetPosition)
    {
        if (isDirectChasing)
        {
            directChaseTarget = targetPosition;
        }
    }

    public void StopDirectChase()
    {
        isDirectChasing = false;
    }
    
    private void HandleDirtLogic()
    {
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            dirtTimer += Time.fixedDeltaTime;
            if (dirtTimer >= dirtInterval)
            {
                dirtTimer = 0f;
                DirtGridManager.Instance?.AddTraffic(transform.position);
            }
        }
    }

    public void SetPath(Queue<Waypoint> newPath)
    {
        isDirectChasing = false;
        this.path = newPath;
        if (this.path != null && this.path.Count > 0)
        {
            pathAnchor = transform.position;
        }
    }

    public bool IsMoving()
    {
        if (isDirectChasing)
        {
            return rb.linearVelocity.magnitude > 0.1f;
        }
        return path != null && path.Count > 0;
    }

    public void Stop()
    {
        StopDirectChase();
        path?.Clear();
        pathAnchor = transform.position;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (isYielding) return;
        AgentMover otherMover = collision.gameObject.GetComponent<AgentMover>();
        if (otherMover != null)
        {
            if (otherMover.priority > this.priority)
            {
                StartYielding();
            }
            else if (otherMover.priority == this.priority)
            {
                if(this.gameObject.GetInstanceID() > collision.gameObject.GetInstanceID())
                {
                    StartYielding();
                }
            }
        }
    }

    private void StartYielding()
    {
        if (yieldingCoroutine != null) StopCoroutine(yieldingCoroutine);
        yieldingCoroutine = StartCoroutine(YieldRoutine());
    }

    private IEnumerator YieldRoutine()
    {
        isYielding = true;
        yield return new WaitForSeconds(0.5f);
        isYielding = false;
        yieldingCoroutine = null;
    }
    
    private void UpdateSpriteDirection(Vector2 velocity)
    {
        var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            if (velocity.x > 0.1f) spriteRenderer.flipX = false;
            else if (velocity.x < -0.1f) spriteRenderer.flipX = true;
        }
    }
}