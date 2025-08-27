using UnityEngine;
using System.Collections;

public class ClientMovement : MonoBehaviour
{
    private ClientPathfinding parent;
    
    [Header("Базовые параметры")]
    [SerializeField] public float stoppingDistance = 0.8f;
    
    [Header("Детектор застревания")]
    [SerializeField] private float stuckTimeThreshold = 3f;

    [Header("Случайные параметры (Разброс)")]
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
        
        RandomizeParameters(); 
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

    public void RandomizeParameters() 
    { 
        var rb = GetComponent<Rigidbody2D>();
        var _collider2D = GetComponent<CircleCollider2D>();

        if (agentMover != null)
        {
            agentMover.moveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
        }
        if (rb != null) 
        { 
            rb.mass = Random.Range(minMass, maxMass); 
        } 
        float scale = Random.Range(minScale, maxScale); 
        transform.localScale = new Vector3(scale, scale, 1); 
        if (_collider2D != null) 
        { 
            _collider2D.radius = baseColliderRadius * scale; 
        } 
    }
    
    // Старые методы управления скоростью и трением больше не нужны
    public float GetBaseColliderRadius() { return baseColliderRadius; }
}