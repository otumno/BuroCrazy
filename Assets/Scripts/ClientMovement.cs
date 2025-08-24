using UnityEngine;
using System.Collections;

public class ClientMovement : MonoBehaviour
{
    private ClientPathfinding parent;
    private Rigidbody2D rb;
    private CircleCollider2D _collider2D;
    private SpriteRenderer spriteRenderer;

    [Header("Базовые параметры")]
    [SerializeField] public float moveSpeed = 2f;
    [SerializeField] public float stoppingDistance = 0.8f;
    [Header("Физика и Трение")]
    [SerializeField] public float normalDrag = 1f;
    [SerializeField] public float congestedDrag = 20f;
    [SerializeField] public float stumbleDrag = 50f; // <-- Новое поле
    [SerializeField] public float stumbleSpeedThreshold = 4f; // <-- Новое поле
    [Header("Детектор застревания")]
    [SerializeField] private float stuckVelocityThreshold = 0.1f;
    [SerializeField] private float stuckTimeThreshold = 3f;
    [Header("Случайные параметры (Разброс)")]
    [SerializeField] private float minMoveSpeed = 1.2f;
    [SerializeField] private float maxMoveSpeed = 3.0f;
    [SerializeField] private float minMass = 0.8f;
    [SerializeField] private float maxMass = 1.2f;
    [SerializeField] private float minScale = 0.9f;
    [SerializeField] private float maxScale = 1.1f;
    [Header("Ограничения")]
    [SerializeField] public float maxVelocity = 10f;
    
    private float baseColliderRadius = 0.2f;
    private Coroutine stuckCheckCoroutine;

    public void Initialize(ClientPathfinding parent) { this.parent = parent; rb = GetComponent<Rigidbody2D>(); _collider2D = GetComponent<CircleCollider2D>(); spriteRenderer = GetComponentInChildren<SpriteRenderer>(); if (rb == null) { enabled = false; return; } RandomizeParameters(); rb.linearDamping = normalDrag; }
    public void StartStuckCheck() { if (stuckCheckCoroutine != null) StopCoroutine(stuckCheckCoroutine); stuckCheckCoroutine = StartCoroutine(CheckIfStuck()); }
    public void StopStuckCheck() { if (stuckCheckCoroutine != null) StopCoroutine(stuckCheckCoroutine); stuckCheckCoroutine = null; }
    private IEnumerator CheckIfStuck() { float timeStuck = 0f; while(true) { yield return new WaitForSeconds(0.5f); if (rb.linearVelocity.magnitude < stuckVelocityThreshold) { timeStuck += 0.5f; } else { timeStuck = 0f; } if (timeStuck >= stuckTimeThreshold) { parent.stateMachine.SetState(ClientState.Confused); yield break; } } }
    public void RandomizeParameters() { moveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed); if (rb != null) { rb.mass = Random.Range(minMass, maxMass); } float scale = Random.Range(minScale, maxScale); transform.localScale = new Vector3(scale, scale, 1); if (_collider2D != null) { _collider2D.radius = baseColliderRadius * scale; } }
    
    void FixedUpdate()
    {
        if (rb != null)
        {
            if (rb.linearVelocity.magnitude > stumbleSpeedThreshold)
            {
                rb.linearDamping = stumbleDrag;
            }
            else if (rb.linearVelocity.magnitude < stuckVelocityThreshold)
            {
                rb.linearDamping = congestedDrag;
            }
            else
            {
                rb.linearDamping = normalDrag;
            }

            if (rb.linearVelocity.magnitude > maxVelocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxVelocity;
            }
            
            if (spriteRenderer != null)
            {
                if (rb.linearVelocity.x > 0.1f) { spriteRenderer.flipX = false; }
                else if (rb.linearVelocity.x < -0.1f) { spriteRenderer.flipX = true; }
            }
        }
    }
    
    public void SetLinearDrag(float value) { if (rb != null) rb.linearDamping = value; }
    public void SetColliderRadius(float radius) { if (_collider2D != null) _collider2D.radius = radius; }
    public void SetVelocity(Vector2 velocity) { if (rb != null) rb.linearVelocity = velocity; }
    public void SetMass(float mass) { if (rb != null) rb.mass = mass; }
    public float GetMass() { return (rb != null) ? rb.mass : 1f; }
    public float GetBaseColliderRadius() { return baseColliderRadius; }
}