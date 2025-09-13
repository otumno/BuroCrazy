// Файл: SecurityBarrier.cs
using UnityEngine;

public class SecurityBarrier : MonoBehaviour
{
    public static SecurityBarrier Instance { get; private set; }

    [Header("Настройки")]
    [Tooltip("Коллайдер, который будет блокировать путь")]
    public Collider2D barrierCollider;

    // --- НАЧАЛО ИЗМЕНЕНИЙ ---
    [Header("Визуальные элементы")]
    [Tooltip("Иконка замка, которая отображается, когда барьер активен")]
    public GameObject lockIcon;
    // --- КОНЕЦ ИЗМЕНЕНИЙ ---

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

    // --- НОВЫЙ МЕТОД ---
    void Start()
    {
        // Устанавливаем правильное состояние замка при запуске игры
        if (lockIcon != null)
        {
            lockIcon.SetActive(IsActive());
        }
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
            // --- ДОБАВЛЕНО ---
            if (lockIcon != null) lockIcon.SetActive(true);
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
            // --- ДОБАВЛЕНО ---
            if (lockIcon != null) lockIcon.SetActive(false);
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