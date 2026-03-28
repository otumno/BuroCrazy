using UnityEngine;
using System.Collections;

public class ActionFeedbackIcon : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 1.5f; // Уменьшенная скорость для красивого вылета из рук
    public float lifetime = 1.2f;
    public float fadeStartDelay = 0.6f;
    
    [Header("Direction Cone")]
    public float minAngle = -45f;
    public float maxAngle = 45f;

    private Vector3 moveDirection;
    private SpriteRenderer spriteRenderer;
    private float timer = 0f;

    void Start()
    {
        // Направление теперь задается извне через SetFixedDirection.
        // Если никто не задал, летим вверх по умолчанию.
        if (moveDirection == Vector3.zero) moveDirection = Vector3.up;

        spriteRenderer = GetComponent<SpriteRenderer>();
        Destroy(gameObject, lifetime);
    }

    // Вызывается из StaffController сразу после спавна
    public void SetFixedDirection(Vector3 direction)
    {
        moveDirection = direction.normalized;
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Двигаем значок
        transform.position += moveDirection * speed * Time.deltaTime;

        // Замедляем движение со временем (эффект инерции)
        speed = Mathf.Lerp(speed, 0, timer / lifetime);

        // Плавное исчезновение (Fade Out)
        if (timer > fadeStartDelay && spriteRenderer != null)
        {
            float fadeProgress = (timer - fadeStartDelay) / (lifetime - fadeStartDelay);
            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(1f, 0f, fadeProgress);
            spriteRenderer.color = c;
        }
    }
}
