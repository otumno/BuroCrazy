// Файл: ClientMovement.cs
using UnityEngine;
using System.Collections;

public class ClientMovement : MonoBehaviour
{
    private ClientPathfinding parent;
    [Header("Базовые параметры")]
    [SerializeField] public float stoppingDistance = 0.8f;
    [Header("Детектор застревания")]
    [SerializeField] private float stuckTimeThreshold = 3f;
    [Header("Параметры для расчета на основе характера")]
    [SerializeField] private float minMoveSpeed = 1.2f;
    [SerializeField] private float maxMoveSpeed = 3.0f;
    [SerializeField] private float minMass = 0.8f;
    [SerializeField] private float maxMass = 1.2f;
    [SerializeField] private float minScale = 0.9f;
    [SerializeField] private float maxScale = 1.1f;
    
    private float baseColliderRadius = 0.2f;
    private Coroutine stuckCheckCoroutine;
    private AgentMover agentMover;
    public void Initialize(ClientPathfinding parent) 
    { 
        this.parent = parent;
        var rb = GetComponent<Rigidbody2D>();
        var _collider2D = GetComponent<CircleCollider2D>();
        agentMover = GetComponent<AgentMover>();
        if (rb == null || agentMover == null) 
        { 
            enabled = false;
            return; 
        } 
        
        // --- МЕТОД ЗАМЕНЕН ---
        CalculateParametersFromTraits();
    }

    public void StartStuckCheck() 
    { 
        if (stuckCheckCoroutine != null) StopCoroutine(stuckCheckCoroutine);
        stuckCheckCoroutine = StartCoroutine(CheckIfStuck()); 
    }

    public void StopStuckCheck() 
    { 
        if (stuckCheckCoroutine != null) StopCoroutine(stuckCheckCoroutine);
        stuckCheckCoroutine = null; 
    }
    
    private IEnumerator CheckIfStuck()
    {
        float timeStuck = 0f;
        Vector2 lastPosition = transform.position;

        while(true)
        {
            yield return new WaitForSeconds(0.5f);
            float distanceMoved = Vector2.Distance(transform.position, lastPosition);
            
            if (distanceMoved < 0.1f) 
            {
                timeStuck += 0.5f;
            }
            else
            {
                timeStuck = 0f;
                lastPosition = transform.position;
            }

            if (timeStuck >= stuckTimeThreshold)
            {
                Debug.LogWarning($"Клиент {gameObject.name} застрял! Принудительно перевожу в состояние Confused.");
                parent.stateMachine.SetState(ClientState.Confused);
                yield break;
            }
        }
    }

    // --- СТАРЫЙ МЕТОД RandomizeParameters() УДАЛЕН ---
    // --- НОВЫЙ МЕТОД ---
    public void CalculateParametersFromTraits() 
    { 
        var rb = GetComponent<Rigidbody2D>();
        var _collider2D = GetComponent<CircleCollider2D>();

        // Скорость зависит от фактора "Суетуна" (быстрее) и "Бабушки" (медленнее)
        float baseSpeed = Mathf.Lerp(minMoveSpeed, maxMoveSpeed, parent.suetunFactor);
        float finalSpeed = baseSpeed * (1f - parent.babushkaFactor * 0.4f); // Бабушка на 40% медленнее при факторе 1.0

        // Масса и размер зависят от "Суетуна" (более легкие и мелкие)
        float finalMass = Mathf.Lerp(maxMass, minMass, parent.suetunFactor);
        float finalScale = Mathf.Lerp(maxScale, minScale, parent.suetunFactor);

        if (agentMover != null)
        {
            agentMover.moveSpeed = finalSpeed;
        }
        if (rb != null) 
        { 
            rb.mass = finalMass;
        } 

        transform.localScale = new Vector3(finalScale, finalScale, 1);
        if (_collider2D != null) 
        { 
            _collider2D.radius = baseColliderRadius * finalScale;
        } 
    }
    
    public float GetBaseColliderRadius() { return baseColliderRadius; }
}