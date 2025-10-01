// Файл: AgentMover.cs - ПОЛНАЯ ИСПРАВЛЕННАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class AgentMover : MonoBehaviour
{
    [Header("Параметры Движения")]
    public float moveSpeed = 2f;
    public float stoppingDistance = 0.2f;

    [Header("Простая анимация ходьбы")]
    public SpriteRenderer characterSpriteRenderer;
    public Sprite idleSprite;
    public Sprite walkSprite1;
    public Sprite walkSprite2;
    public float animationSpeed = 0.3f;
    private float animationTimer = 0f;
    private bool isFirstWalkSprite = true;

    // --- ДОБАВЛЕНО: ДИНАМИЧЕСКИЙ СВЕТ ---
    [Header("Динамический свет")]
    [Tooltip("Перетащите сюда Transform объекта Spotlight")]
    [SerializeField] private Transform dynamicLight; 
    [Tooltip("На какое расстояние свет будет смещаться вперед при движении")]
    [SerializeField] public float lightForwardOffset = 0.5f;
    [Tooltip("Насколько плавно свет возвращается на место и следует за движением")]
    [SerializeField] private float lightSmoothing = 5f;
    
    // --- ВОЗВРАЩЕНО: Недостающие поля из вашей версии ---
    private float baseMoveSpeed;
    public UnityEngine.AI.NavMeshAgent agent;
    [Header("Система 'Резиночки'")]
    [Tooltip("Насколько сильно персонаж стремится вернуться на свой путь.")]
    public float rubberBandStrength = 5f;
    [Tooltip("Приоритет персонажа. Решает, кто кого продавливает.")]
    public int priority = 1;
    // ---------------------------------------------------

    private Rigidbody2D rb;
    private Queue<Waypoint> path;
    private Vector2 pathAnchor; 
    private bool isYielding = false;
    private Coroutine yieldingCoroutine;
    private float dirtTimer = 0f;
    private float dirtInterval = 0.25f;

    private bool isDirectChasing = false;
    private Vector2 directChaseTarget;
    
    [Header("Настройки преследования")]
    [Tooltip("С какого расстояния персонаж начнет замедляться при прямой погоне.")]
    public float slowingDistance = 2.0f;
    [Tooltip("Насколько плавно персонаж меняет скорость.")]
    public float movementSmoothing = 5f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pathAnchor = transform.position;
        baseMoveSpeed = moveSpeed;
    }

    public void ApplySpeedMultiplier(float multiplier)
    {
        moveSpeed = baseMoveSpeed * multiplier;
    }

    void FixedUpdate()
    {
        Vector2 desiredVelocity;
        if (isDirectChasing)
        {
            Vector2 vectorToTarget = directChaseTarget - (Vector2)transform.position;
            float distanceToTarget = vectorToTarget.magnitude;
            
            float targetSpeed = moveSpeed;
            if (distanceToTarget < slowingDistance)
            {
                targetSpeed = moveSpeed * (distanceToTarget / slowingDistance);
            }
            desiredVelocity = (distanceToTarget > 0.01f) ? vectorToTarget.normalized * targetSpeed : Vector2.zero;
        }
        else if (path == null || path.Count == 0)
        {
            desiredVelocity = Vector2.zero;
        }
        else
        {
            Waypoint targetWaypoint = path.Peek();
            pathAnchor = Vector2.MoveTowards(pathAnchor, targetWaypoint.transform.position, moveSpeed * Time.fixedDeltaTime);
            float currentStrength = isYielding ? rubberBandStrength / 4f : rubberBandStrength;
            desiredVelocity = (pathAnchor - (Vector2)transform.position) * currentStrength;
            desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, moveSpeed * 2f);

            if (Vector2.Distance(transform.position, targetWaypoint.transform.position) < stoppingDistance)
            {
                pathAnchor = targetWaypoint.transform.position;
                path.Dequeue();
            }
        }

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.fixedDeltaTime * movementSmoothing);
        UpdateSpriteDirection(rb.linearVelocity);
        HandleDirtLogic();
        HandleWalkAnimation();
        UpdateDynamicLightPosition();
    }

    private void UpdateDynamicLightPosition()
    {
        if (dynamicLight == null) return;

        Vector2 targetPosition;
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            targetPosition = rb.linearVelocity.normalized * lightForwardOffset;
        }
        else
        {
            targetPosition = Vector2.zero;
        }
        
        dynamicLight.localPosition = Vector2.Lerp(dynamicLight.localPosition, targetPosition, Time.fixedDeltaTime * lightSmoothing);
    }
    
    private void HandleWalkAnimation()
    {
        if (characterSpriteRenderer == null || idleSprite == null || walkSprite1 == null || walkSprite2 == null) return;

        if (rb.linearVelocity.magnitude > 0.1f)
        {
            animationTimer += Time.fixedDeltaTime;
            if (animationTimer >= animationSpeed)
            {
                animationTimer = 0f;
                isFirstWalkSprite = !isFirstWalkSprite;
                characterSpriteRenderer.sprite = isFirstWalkSprite ? walkSprite1 : walkSprite2;
            }
        }
        else
        {
            characterSpriteRenderer.sprite = idleSprite;
            animationTimer = 0f;
        }
    }

    public void SetAnimationSprites(Sprite idle, Sprite walk1, Sprite walk2)
    {
        this.idleSprite = idle;
        this.walkSprite1 = walk1;
        this.walkSprite2 = walk2;
    }

    // --- ВОЗВРАЩЕНЫ: Недостающие методы из вашей версии ---
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
    // ------------------------------------------------------
    
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
            Debug.Log($"<color=cyan>[AgentMover]</color> {gameObject.name} получил новый путь из {path.Count} точек.");
            Vector3 previousPoint = transform.position;
            foreach(var waypoint in path)
            {
                Debug.DrawLine(previousPoint, waypoint.transform.position, Color.green, 10f);
                previousPoint = waypoint.transform.position;
            }
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
        if (characterSpriteRenderer != null)
        {
            if (velocity.x > 0.1f) characterSpriteRenderer.flipX = false;
            else if (velocity.x < -0.1f) characterSpriteRenderer.flipX = true;
        }
    }
}