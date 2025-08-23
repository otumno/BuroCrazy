using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Для работы с List

public class ClientBehavior : MonoBehaviour
{
    [Header("Настройки движения")]
    public float moveSpeed = 2f;
    public List<Transform> targetPoints;
    public float minWaitTime = 1f;
    public float maxWaitTime = 3f;
    
    private Rigidbody2D rb;
    private Transform currentTarget;
    private List<Transform> availablePoints = new List<Transform>();

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        availablePoints = new List<Transform>(targetPoints);
        SelectRandomTarget();
    }

    void FixedUpdate()
    {
        if (currentTarget == null) return;
        
        Vector2 direction = (currentTarget.position - transform.position).normalized;
        rb.AddForce(direction * moveSpeed * 10f);
        
        // Поворот спрайта
        if (direction.x > 0.1f) transform.localScale = new Vector3(1, 1, 1);
        else if (direction.x < -0.1f) transform.localScale = new Vector3(-1, 1, 1);
    }

    void SelectRandomTarget()
    {
        if (availablePoints.Count == 0)
            availablePoints = new List<Transform>(targetPoints);
        
        // Выбираем случайную точку из доступных
        int randomIndex = Random.Range(0, availablePoints.Count);
        currentTarget = availablePoints[randomIndex];
        availablePoints.RemoveAt(randomIndex);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.transform == currentTarget)
        {
            // Случайная задержка перед новым выбором цели
            float waitTime = Random.Range(minWaitTime, maxWaitTime);
            Invoke("SelectRandomTarget", waitTime);
            
            // Останавливаемся на точке
            rb.linearVelocity = Vector2.zero;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Смягчение столкновений между клиентами
        if (collision.gameObject.CompareTag("Client"))
        {
            rb.linearVelocity *= 0.7f;
        }
    }
}