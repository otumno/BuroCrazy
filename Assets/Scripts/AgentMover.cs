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
    private Vector2 pathAnchor; // "Желаемая" позиция на пути
    private bool isYielding = false;
    private Coroutine yieldingCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pathAnchor = transform.position;
    }

    void FixedUpdate()
    {
        if (path == null || path.Count == 0)
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * 10f);
            return;
        }

        Waypoint targetWaypoint = path.Peek();

        pathAnchor = Vector2.MoveTowards(pathAnchor, targetWaypoint.transform.position, moveSpeed * Time.fixedDeltaTime);

        float currentStrength = isYielding ? rubberBandStrength / 4f : rubberBandStrength;
        Vector2 desiredVelocity = (pathAnchor - (Vector2)transform.position) * currentStrength;
        
        // --- НОВАЯ СТРОКА: Ограничиваем максимальную скорость ---
        // Это не даёт "резиночке" растягиваться до бесконечности и вызывать взрыв.
        desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, moveSpeed * 2f);

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.fixedDeltaTime * 10f);
        
        UpdateSpriteDirection(rb.linearVelocity);

        if (Vector2.Distance(transform.position, targetWaypoint.transform.position) < stoppingDistance)
        {
            pathAnchor = targetWaypoint.transform.position;
            path.Dequeue();
        }
    }

    // --- (Остальной код скрипта без изменений) ---

    public void SetPath(Queue<Waypoint> newPath)
    {
        this.path = newPath;
        if (this.path != null && this.path.Count > 0)
        {
            pathAnchor = transform.position;
        }
    }

    public bool IsMoving()
    {
        return path != null && path.Count > 0;
    }

    public void Stop()
    {
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