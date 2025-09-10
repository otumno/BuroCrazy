// Файл: SecurityBarrier.cs
using UnityEngine;

public class SecurityBarrier : MonoBehaviour
{
    public static SecurityBarrier Instance { get; private set; }

    [Header("Настройки")]
    [Tooltip("Коллайдер, который будет блокировать путь")]
    public Collider2D barrierCollider;
    [Tooltip("Точка, к которой должен подойти охранник для взаимодействия")]
    public Transform guardInteractionPoint;

    [Header("Звуки")]
    public AudioClip activateSound;
    public AudioClip deactivateSound;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    /// <summary>
    /// Включает барьер (делает его непроходимым).
    /// </summary>
    public void ActivateBarrier()
    {
        if (barrierCollider != null)
        {
            barrierCollider.enabled = true;
            if (activateSound != null)
            {
                AudioSource.PlayClipAtPoint(activateSound, transform.position);
            }
            Debug.Log("Защитный барьер АКТИВИРОВАН.");
        }
    }

    /// <summary>
    /// Выключает барьер (делает его проходимым).
    /// </summary>
    public void DeactivateBarrier()
    {
        if (barrierCollider != null)
        {
            barrierCollider.enabled = false;
            if (deactivateSound != null)
            {
                AudioSource.PlayClipAtPoint(deactivateSound, transform.position);
            }
            Debug.Log("Защитный барьер ДЕАКТИВИРОВАН.");
        }
    }

    /// <summary>
    /// Проверяет, активен ли барьер в данный момент.
    /// </summary>
    public bool IsActive()
    {
        return barrierCollider != null && barrierCollider.enabled;
    }
}