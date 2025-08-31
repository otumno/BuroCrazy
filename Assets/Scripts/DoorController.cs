// Файл: DoorController.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class DoorController : MonoBehaviour
{
    [Header("Спрайты Двери")]
    [Tooltip("Спрайт, который будет отображаться, когда дверь открыта.")]
    public Sprite openSprite;

    [Tooltip("Спрайт, который будет отображаться, когда дверь закрыта.")]
    public Sprite closedSprite;
    
    // --- НОВЫЕ ПОЛЯ ДЛЯ ЗВУКА ---
    [Header("Звуки")]
    [Tooltip("Звук, который проигрывается при открытии двери.")]
    public AudioClip openSound;

    [Tooltip("Звук, который проигрывается при закрытии двери.")]
    public AudioClip closeSound;
    // ----------------------------

    private SpriteRenderer spriteRenderer;
    private int charactersInTrigger = 0;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = closedSprite;
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Client") || other.CompareTag("Clerk") || other.CompareTag("Guard") || other.CompareTag("Service"))
        {
            charactersInTrigger++;
            if (charactersInTrigger == 1)
            {
                spriteRenderer.sprite = openSprite;
                // --- НОВАЯ СТРОКА: Проигрываем звук открытия ---
                if (openSound != null)
                {
                    AudioSource.PlayClipAtPoint(openSound, transform.position);
                }
                // ------------------------------------------
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Client") || other.CompareTag("Clerk") || other.CompareTag("Guard") || other.CompareTag("Service"))
        {
            charactersInTrigger--;
            if (charactersInTrigger == 0)
            {
                spriteRenderer.sprite = closedSprite;
                // --- НОВАЯ СТРОКА: Проигрываем звук закрытия ---
                if (closeSound != null)
                {
                    AudioSource.PlayClipAtPoint(closeSound, transform.position);
                }
                // -------------------------------------------
            }
        }
    }
}