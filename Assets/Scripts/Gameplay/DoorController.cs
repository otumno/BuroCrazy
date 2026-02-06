// Файл: DoorController.cs
using UnityEngine;
using System.Collections.Generic;
using Scriptables.Audio;
using Managers;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class DoorController : MonoBehaviour
{
    [Header("Спрайты Двери")]
    [Tooltip("Спрайт, который будет отображаться, когда дверь открыта.")]
    public Sprite openSprite;

    [Tooltip("Спрайт, который будет отображаться, когда дверь закрыта.")]
    public Sprite closedSprite;

    [Header("Звуки")]
    [Tooltip("Звук открытия двери")]
    public AudioClip openSound;

    [Tooltip("Звук закрытия двери")]
    public AudioClip closeSound;

    [Header("Sound ID (альтернатива)")]
    public SoundID openSoundID = SoundID.Door_Open;
    public SoundID closeSoundID = SoundID.Door_Close;

    private SpriteRenderer spriteRenderer;
    private int charactersInTrigger = 0;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = closedSprite;
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void PlayOpenSound()
    {
        if (openSound != null)
        {
            AudioSource.PlayClipAtPoint(openSound, transform.position);
        }
        else if (AudioManager.Instance != null && openSoundID != SoundID.None)
        {
            AudioManager.Instance.PlaySound(openSoundID, transform.position);
        }
    }

    private void PlayCloseSound()
    {
        if (closeSound != null)
        {
            AudioSource.PlayClipAtPoint(closeSound, transform.position);
        }
        else if (AudioManager.Instance != null && closeSoundID != SoundID.None)
        {
            AudioManager.Instance.PlaySound(closeSoundID, transform.position);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Client") || other.CompareTag("Clerk") || other.CompareTag("Guard") ||
            other.CompareTag("Service") || other.CompareTag("Director") || other.CompareTag("Staff"))
        {
            charactersInTrigger++;
            if (charactersInTrigger == 1)
            {
                spriteRenderer.sprite = openSprite;
                PlayOpenSound();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Client") || other.CompareTag("Clerk") || other.CompareTag("Guard") ||
            other.CompareTag("Service") || other.CompareTag("Director") || other.CompareTag("Staff"))
        {
            charactersInTrigger--;
            if (charactersInTrigger == 0)
            {
                spriteRenderer.sprite = closedSprite;
                PlayCloseSound();
            }
        }
    }
}