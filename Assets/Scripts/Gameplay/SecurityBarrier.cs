using UnityEngine;

public class SecurityBarrier : MonoBehaviour
{
    public static SecurityBarrier Instance { get; private set; }

    [Header("Настройки")]
    public Collider2D barrierCollider;
    [Header("Визуальные элементы")]
    public GameObject lockIcon;
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
    
    void Start()
    {
        if (lockIcon != null)
        {
            lockIcon.SetActive(IsActive());
        }
    }
    
    public void ActivateBarrier()
    {
        if (barrierCollider != null)
        {
            barrierCollider.enabled = true;
            if (activateSound != null) AudioSource.PlayClipAtPoint(activateSound, transform.position);
            if (lockIcon != null) lockIcon.SetActive(true);
            Debug.Log("Защитный барьер АКТИВИРОВАН.");
        }
    }
    
    public void DeactivateBarrier()
    {
        if (barrierCollider != null)
        {
            barrierCollider.enabled = false;
            if (deactivateSound != null) AudioSource.PlayClipAtPoint(deactivateSound, transform.position);
            if (lockIcon != null) lockIcon.SetActive(false);
            Debug.Log("Защитный барьер ДЕАКТИВИРОВАН.");
			AchievementManager.Instance?.UnlockAchievement("OPEN_FIRST_DOOR");
        }
    }
    
    public bool IsActive()
    {
        return barrierCollider != null && barrierCollider.enabled;
    }

    public void ToggleBarrier()
    {
        if (IsActive()) DeactivateBarrier();
        else ActivateBarrier();
    }
}